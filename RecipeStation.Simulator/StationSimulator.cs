using System.Net;
using System.Net.Sockets;
using RecipeStation.Protocol.Ber;
using RecipeStation.Protocol.Udp;
using RecipeStation.Client;

namespace RecipeStation.Simulator;

/// <summary>
/// Simulates the station side of the UDP protocol.
/// Listens on RemoteSendPort, sends responses from RemoteRecvPort to (RemoteIP, LocalRecvPort).
/// </summary>
public sealed class StationSimulator : IDisposable
{
    private readonly string _name;
    private readonly UdpClient _receiver;
    private readonly UdpClient _sender;
    private readonly IPEndPoint _clientEndPoint;

    private readonly List<string> _productionRecipes;
    private readonly List<string> _calibrationRecipes;

    private readonly Queue<BerTag> _outQueue = new();
    private readonly ManualResetEventSlim _outReady = new(false);
    private readonly ManualResetEventSlim _ackReady  = new(false);
    private readonly object _outLock = new();

    private ushort _sendSeq;
    private volatile bool _running;
    private bool _disposed;

    public string StationName => _name;

    public StationSimulator(RecipeStationConfig cfg,
        List<string>? productionRecipes   = null,
        List<string>? calibrationRecipes  = null)
    {
        _name            = cfg.StationName;
        _receiver        = new UdpClient(new IPEndPoint(IPAddress.Any, cfg.RemoteSendPort));
        _sender          = new UdpClient(new IPEndPoint(IPAddress.Any, cfg.RemoteRecvPort));
        _clientEndPoint  = new IPEndPoint(IPAddress.Parse(cfg.RemoteIP), cfg.LocalRecvPort);
        _productionRecipes  = productionRecipes  ?? ["RecipeProd_A", "RecipeProd_B", "RecipeProd_C"];
        _calibrationRecipes = calibrationRecipes ?? ["RecipeCal_A", "RecipeCal_B"];
    }

    public void Start()
    {
        _running = true;
        new Thread(ReceiveLoop) { IsBackground = true, Name = $"{_name}.Sim.Recv" }.Start();
        new Thread(SendLoop)    { IsBackground = true, Name = $"{_name}.Sim.Send" }.Start();
        Log($"started — listening on {((IPEndPoint)_receiver.Client.LocalEndPoint!).Port}, " +
            $"sending to {_clientEndPoint}");
    }

    public void Stop()
    {
        _running = false;
        try { _receiver.Close(); } catch { }
    }

    // ── Receive loop ─────────────────────────────────────────────────────────

    private void ReceiveLoop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                var data = _receiver.Receive(ref remote);
                var msg  = UdpMsg.Deserialize(data, data.Length);
                if (msg == null) { Log("Unidentified message received"); continue; }
                HandleIncoming(msg);
            }
            catch (Exception) when (!_running) { }
            catch (Exception ex) { Log($"Recv error: {ex.Message}"); }
        }
    }

    private void HandleIncoming(UdpMsg msg)
    {
        var tagNum = msg.Tag.Identifier.TagNumber;

        if (tagNum == UdpMsgTagNumbers.Acknowledge)
        {
            Log($"<< ACK SEQ:{msg.SequenceNumber:X4}");
            _ackReady.Set();
            return;
        }

        SendAck(msg.SequenceNumber);

        if (tagNum == UdpMsgTagNumbers.Ping)
        {
            Log("<< Ping");
            return;
        }

        // Production GetRecipeQuantity arrives as a primitive
        if (msg.Tag is BerAppPrimitive && tagNum == StationMsgTagNumbers.GetRecipeQuantity)
        {
            Log("<< GetRecipeQuantity (production)");
            EnqueueSendRecipeQuantity(0, _productionRecipes.Count);
            return;
        }

        if (msg.Tag is not BerAppConstructed tag) return;

        switch (tagNum)
        {
            case StationMsgTagNumbers.GetRecipeQuantity:
            {
                int recipeType = ExtractRecipeType(tag);
                var list = recipeType == 1 ? _calibrationRecipes : _productionRecipes;
                Log($"<< GetRecipeQuantity type:{recipeType}");
                EnqueueSendRecipeQuantity(recipeType, list.Count);
                break;
            }

            case StationMsgTagNumbers.GetRecipeName:
            {
                int index = 0, recipeType = 0;
                for (int i = 0; i < tag.Count; i++)
                {
                    if (tag[i] is BerInteger b) index = b.Value;
                    else if (IsRecipeTypeTag(tag[i], out int rt)) recipeType = rt;
                }
                var list = recipeType == 1 ? _calibrationRecipes : _productionRecipes;
                var name = index >= 0 && index < list.Count ? list[index] : string.Empty;
                Log($"<< GetRecipeName [{index}] type:{recipeType} -> \"{name}\"");
                EnqueueSendRecipeName(index, name, recipeType);
                break;
            }

            case StationMsgTagNumbers.CopyRecipe:
            {
                string src = string.Empty, dst = string.Empty;
                int nc = 0;
                for (int i = 0; i < tag.Count; i++)
                    if (tag[i].Identifier.TagNumber == StationMsgTagNumbers.RecipeName
                        && tag[i] is BerAppConstructed nt && nt.Count > 0 && nt[0] is BerVisibleString vs)
                    { if (nc++ == 0) src = vs.Value; else dst = vs.Value; }
                Log($"<< CopyRecipe \"{src}\" -> \"{dst}\"");
                if (!string.IsNullOrEmpty(dst) && !_productionRecipes.Contains(dst))
                    _productionRecipes.Add(dst);
                EnqueuePrimitive(StationMsgTagNumbers.RecipeCopied);
                break;
            }

            case StationMsgTagNumbers.DeleteRecipe:
            {
                string name = string.Empty;
                for (int i = 0; i < tag.Count; i++)
                    if (tag[i].Identifier.TagNumber == StationMsgTagNumbers.RecipeName
                        && tag[i] is BerAppConstructed nt && nt.Count > 0 && nt[0] is BerVisibleString vs)
                    { name = vs.Value; break; }
                Log($"<< DeleteRecipe \"{name}\"");
                _productionRecipes.Remove(name);
                EnqueuePrimitive(StationMsgTagNumbers.RecipeDeleted);
                break;
            }

            default:
                Log($"<< Unhandled tag {tagNum}");
                break;
        }
    }

    // ── Send loop ─────────────────────────────────────────────────────────────

    private void SendLoop()
    {
        while (_running)
        {
            if (!_outReady.Wait(500)) continue;

            BerTag tagToSend;
            lock (_outLock)
            {
                if (_outQueue.Count == 0) { _outReady.Reset(); continue; }
                tagToSend = _outQueue.Dequeue();
                if (_outQueue.Count == 0) _outReady.Reset();
            }

            var msg    = new UdpMsg(_sendSeq, tagToSend);
            int resend = 0;

            while (_running)
            {
                _ackReady.Reset();
                SendMsg(msg);
                Log($">> Sent SEQ:{_sendSeq:X4} tag:{tagToSend.Identifier.TagNumber}");

                if (_ackReady.Wait(20_000))
                {
                    _sendSeq++;
                    break;
                }

                if (++resend >= 5)
                {
                    Log($"Max resend reached SEQ:{_sendSeq:X4}, dropping");
                    _sendSeq++;
                    break;
                }
                Log($"Resending SEQ:{_sendSeq:X4} ({resend}/5)");
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnqueueSendRecipeQuantity(int recipeType, int count)
    {
        var tag = new BerAppConstructed(StationMsgTagNumbers.SendRecipeQuantity);
        tag.Add(new BerInteger(count));
        if (recipeType != 0)
        {
            var rt = new BerAppConstructed(StationMsgTagNumbers.RecipeTypeTag);
            rt.Add(new BerInteger(recipeType));
            tag.Add(rt);
        }
        Enqueue(tag);
    }

    private void EnqueueSendRecipeName(int index, string name, int recipeType)
    {
        var tag = new BerAppConstructed(StationMsgTagNumbers.SendRecipeName);
        tag.Add(new BerInteger(index));
        tag.Add(new BerVisibleString(name));
        if (recipeType != 0)
        {
            var rt = new BerAppConstructed(StationMsgTagNumbers.RecipeTypeTag);
            rt.Add(new BerInteger(recipeType));
            tag.Add(rt);
        }
        Enqueue(tag);
    }

    private void EnqueuePrimitive(ushort tagNumber) => Enqueue(new BerAppPrimitive(tagNumber));

    private void Enqueue(BerTag tag)
    {
        lock (_outLock) { _outQueue.Enqueue(tag); _outReady.Set(); }
    }

    private void SendAck(ushort seq)
    {
        var ack = new BerAppConstructed(UdpMsgTagNumbers.Acknowledge);
        ack.Add(new BerInteger((int)AcknowledgeValues.Accepted));
        SendMsg(new UdpMsg(seq, ack));
        Log($">> ACK SEQ:{seq:X4}");
    }

    private void SendMsg(UdpMsg msg)
    {
        var bytes = msg.Serialize();
        _sender.Send(bytes, bytes.Length, _clientEndPoint);
    }

    private static int ExtractRecipeType(BerAppConstructed tag)
    {
        for (int i = 0; i < tag.Count; i++)
            if (IsRecipeTypeTag(tag[i], out int rt)) return rt;
        return 0;
    }

    private static bool IsRecipeTypeTag(BerTag tag, out int value)
    {
        if (tag.Identifier.TagNumber == StationMsgTagNumbers.RecipeTypeTag
            && tag is BerAppConstructed rt && rt.Count > 0 && rt[0] is BerInteger rv)
        {
            value = rv.Value;
            return true;
        }
        value = 0;
        return false;
    }

    private void Log(string msg) => Console.WriteLine($"[{_name}] {msg}");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        try { _sender.Close(); } catch { }
    }
}
