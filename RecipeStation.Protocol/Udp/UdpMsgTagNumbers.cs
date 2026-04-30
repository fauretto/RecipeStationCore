namespace RecipeStation.Protocol.Udp;

public static class UdpMsgTagNumbers
{
    public const ushort Acknowledge  = 0;
    public const ushort Ping         = 1;
    public const ushort GetLastError = 3;
    public const ushort SendLastError = 5;
    public const ushort CommandFailed = 6;
}
