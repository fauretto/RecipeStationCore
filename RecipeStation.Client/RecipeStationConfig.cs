namespace RecipeStation.Client;

public class RecipeStationConfig
{
    public string StationName  { get; set; } = string.Empty;
    public string RemoteIP     { get; set; } = string.Empty;
    public int LocalRecvPort   { get; set; }
    public int LocalSendPort   { get; set; }
    public int RemoteRecvPort  { get; set; }
    public int RemoteSendPort  { get; set; }
}
