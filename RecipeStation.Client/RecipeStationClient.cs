using System.Net;
using System.Net.Sockets;
using RecipeStation.Protocol.Ber;
using RecipeStation.Protocol.Udp;

namespace RecipeStation.Client;

public sealed class RecipeStationClient : IRecipeStationClient
{
    #region Events
    public event EventHandler<string>?                    LogMessage;
    public event EventHandler<ushort>?                    AcknowledgeReceived;
    public event EventHandler<ushort>?                    RecipeCopiedReceived;
    public event EventHandler<ushort>?                    RecipeDeletedReceived;
    public event EventHandler<(ushort seq, int quantity)>? RecipeQuantityReceived;
    public event EventHandler<ushort>?                    CommandFailedReceived;
    #endregion

    private enum WaitState { NewMessage, WaitingForAck }

    private const int AckTimeoutMs  = 20_000;
    private const int MaxResendCount = 5;

    private UdpClient?  _sender;
    private UdpClient?  _receiver;
    private readonly IPEndPoint _senderRemote;
    private IPEndPoint  _receiverRemote;

    private Thread? _sendThread;
    private Thread? _recvThread;

    private readonly Queue<BerTag>  _outQueue = new();
    private readonly Queue<UdpMsg>  _inQueue  = new();
    private readonly ManualResetEventSlim _outReady = new(false);
    private readonly ManualResetEventSlim _inReady  = new(false);
    private readonly object _inLock   = new();
    private readonly object _stateLock = new();

    private volatile bool _online;
    private ushort _seq;
    private bool   _disposed;

    public string StationName { get; }
    public bool   IsOnline    { get { lock (_stateLock) return _online; } }

    public RecipeStationClient(RecipeStationConfig cfg)
    {
        StationName     = cfg.StationName;
        _sender         = new UdpClient(new IPEndPoint(IPAddress.Any, cfg.LocalSendPort));
        _receiver       = new UdpClient(new IPEndPoint(IPAddress.Any, cfg.LocalRecvPort));
        _senderRemote   = new IPEndPoint(IPAddress.Parse(cfg.RemoteIP), cfg.RemoteSendPort);
        _receiverRemote = new IPEndPoint(IPAddress.Parse(cfg.RemoteIP), cfg.RemoteRecvPort);
    }

    public void GoOnline()
    {
        lock (_stateLock)
        {
            if (_online) return;
            _seq    = 0;
            _online = true;
            _recvThread = new Thread(ReceiveLoop) { IsBackground = true, Priority = ThreadPriority.Highest, Name = $"{StationName}.Recv" };
            _sendThread = new Thread(SendLoop)    { IsBackground = true, Priority = ThreadPriority.Highest, Name = $"{StationName}.Send" };
            _recvThread.Start();
            _sendThread.Start();
            Log("Online");
        }
    }

    public void GoOffline()
    {
        lock (_stateLock)
        {
            if (!_online) return;
            _online = false;
        }
        try { _receiver?.Close(); } catch { }
        Log("Offline");
    }

    public void SendPing()
    {
        lock (_stateLock)
        {

            if (!_online) return;
            lock (_outQueue)
            {
                _seq = 0;
                _outQueue.Clear();
                _outReady.Reset();
                Enqueue(new BerAppPrimitive(UdpMsgTagNumbers.Ping));
            }
        }
    }

    public void SendCopyRecipe(string src, string dst)
    {
        var tag  = new BerAppConstructed(StationMsgTagNumbers.CopyRecipe);
        var srcN = new BerAppConstructed(StationMsgTagNumbers.RecipeName);
        var dstN = new BerAppConstructed(StationMsgTagNumbers.RecipeName);
        srcN.Add(new BerVisibleString(src)); tag.Add(srcN);
        dstN.Add(new BerVisibleString(dst)); tag.Add(dstN);
        Enqueue(tag);
    }

    public void SendDeleteRecipe(string name)
    {
        var tag  = new BerAppConstructed(StationMsgTagNumbers.DeleteRecipe);
        var nameTag = new BerAppConstructed(StationMsgTagNumbers.RecipeName);
        nameTag.Add(new BerVisibleString(name));
        tag.Add(nameTag);
        Enqueue(tag);
    }

    public void SendGetRecipeQuantity()
        => Enqueue(new BerAppPrimitive(StationMsgTagNumbers.GetRecipeQuantity));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GoOffline();
        try { _sender?.Close(); } catch { }
    }

    private void Enqueue(BerTag tag)
    {
        lock (_stateLock)
        {
            if (!_online) return;
            lock (_outQueue) { _outQueue.Enqueue(tag); _outReady.Set(); }
        }
    }

    private void SendLoop()
    {
        var state = WaitState.NewMessage;
        UdpMsg? lastSent = null;
        
        int resend = 0;

        while (_online)
        {
            switch (state)
            {
                case WaitState.NewMessage:
                    bool hasIn  = _inReady.Wait(0);
                    bool hasOut = _outReady.Wait(0);

                    if (!hasIn && !hasOut)
                    {
                        _inReady.Wait(1000);
                        continue;
                    }

                    if (hasIn)
                    {
                        lock (_inLock)
                        {
                            if (_inQueue.Count == 0) continue;
                            var msg = _inQueue.Dequeue();
                            if (_inQueue.Count == 0) _inReady.Reset();
                            if (msg.Tag.Identifier.TagNumber != UdpMsgTagNumbers.Acknowledge)
                                AcknowledgeIncoming(msg);
                        }
                    }
                    else
                    {
                        BerTag tagToSend;
                        lock (_outQueue)
                        {
                            if (_outQueue.Count == 0) continue;
                            tagToSend = _outQueue.Dequeue();
                            if (tagToSend.Identifier.TagNumber == UdpMsgTagNumbers.Ping) _seq = 0;
                            if (_outQueue.Count == 0) _outReady.Reset();
                        }
                        lastSent = new UdpMsg(_seq, tagToSend);
                        Send(lastSent);
                        Log($">> Sent SEQ:{_seq:X4} {tagToSend}");
                        state = WaitState.WaitingForAck;
                    }
                    break;

                case WaitState.WaitingForAck:
                    if (_inReady.Wait(AckTimeoutMs))
                    {
                        lock (_inLock)
                        {
                            if (_inQueue.Count == 0) { state = WaitState.NewMessage; continue; }
                            var msg = _inQueue.Dequeue();
                            if (_inQueue.Count == 0) _inReady.Reset();

                            if (msg.Tag.Identifier.TagNumber == UdpMsgTagNumbers.Acknowledge && msg.Tag is BerAppConstructed)
                            {
                                Log($"<< ACK SEQ:{msg.SequenceNumber:X4}");
                                AcknowledgeReceived?.Invoke(this, msg.SequenceNumber);
                                _seq++;
                                resend = 0;
                                state = WaitState.NewMessage;
                            }
                            else
                            {
                                AcknowledgeIncoming(msg);
                                resend = 0;
                                state = WaitState.NewMessage;
                            }
                        }
                    }
                    else
                    {
                        if (++resend >= MaxResendCount)
                        {
                            Log($"Max resend reached SEQ:{_seq:X4}");
                            _seq++;
                            resend = 0;
                            state = WaitState.NewMessage;
                        }
                        else
                        {
                            Log($"Resending SEQ:{_seq:X4} ({resend}/{MaxResendCount})");
                            if (lastSent != null) Send(lastSent);
                        }
                    }
                    break;
            }
        }
    }

    private void ReceiveLoop()
    {
        while (_online)
        {
            try
            {
                var data = _receiver!.Receive(ref _receiverRemote);
                var msg  = UdpMsg.Deserialize(data, data.Length);
                if (msg != null)
                {
                    lock (_inLock)
                    {
                        TranslateIncoming(msg);
                        _inQueue.Enqueue(msg);
                        _inReady.Set();
                    }
                }
                else
                {
                    Log("<< Unidentified message");
                }
            }
            catch (Exception) when (!_online) { }
            catch (Exception ex)              { Log($"Receive error: {ex.Message}"); }
        }
    }

    private void AcknowledgeIncoming(UdpMsg msg)
    {
        var value = msg.SequenceNumber == _seq ? AcknowledgeValues.Accepted : AcknowledgeValues.OutOfSeq;
        SendAck(msg.SequenceNumber, value);
        if (value == AcknowledgeValues.Accepted) _seq++;
    }

    private void SendAck(ushort seq, AcknowledgeValues value)
    {
        var ack = new BerAppConstructed(UdpMsgTagNumbers.Acknowledge);
        ack.Add(new BerInteger((int)value));
        Send(new UdpMsg(seq, ack));
        Log($">> ACK SEQ:{seq:X4} {value}");
    }

    private void Send(UdpMsg msg)
    {
        var bytes = msg.Serialize();
        _sender!.Send(bytes, bytes.Length, _senderRemote);
    }

    private void TranslateIncoming(UdpMsg msg)
    {
        if (msg.Tag is BerAppConstructed tag)
        {
            if (tag.Identifier.TagNumber == StationMsgTagNumbers.SendRecipeQuantity)
            {
                for (int i = 0; i < tag.Count; i++)
                {
                    if (tag[i] is BerInteger b)
                    {
                        Log($"<< Recipe Quantity: {b.Value}");
                        RecipeQuantityReceived?.Invoke(this, (msg.SequenceNumber, b.Value));
                        return;
                    }
                }
            }
        }
        else if (msg.Tag is BerAppPrimitive)
        {
            switch (msg.Tag.Identifier.TagNumber)
            {
                case UdpMsgTagNumbers.CommandFailed:
                    Log($"<< Command Failed SEQ:{msg.SequenceNumber:X4}");
                    CommandFailedReceived?.Invoke(this, msg.SequenceNumber);
                    break;
                case StationMsgTagNumbers.RecipeCopied:
                    Log($"<< Recipe Copied SEQ:{msg.SequenceNumber:X4}");
                    RecipeCopiedReceived?.Invoke(this, msg.SequenceNumber);
                    break;
                case StationMsgTagNumbers.RecipeDeleted:
                    Log($"<< Recipe Deleted SEQ:{msg.SequenceNumber:X4}");
                    RecipeDeletedReceived?.Invoke(this, msg.SequenceNumber);
                    break;
            }
        }
    }

    private void Log(string msg) => LogMessage?.Invoke(this, msg);
}
