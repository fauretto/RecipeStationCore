namespace RecipeStation.Client;

public class PcConfig
{
    public string PcName { get; set; } = string.Empty;
    public string IP     { get; set; } = string.Empty;
    public List<StationPortConfig> Stations { get; set; } = [];
}

public class StationPortConfig
{
    public string StationName   { get; set; } = string.Empty;
    public int LocalRecvPort    { get; set; }
    public int LocalSendPort    { get; set; }
    public int RemoteRecvPort   { get; set; }
    public int RemoteSendPort   { get; set; }

    public RecipeStationConfig ToRecipeStationConfig(string ip) => new()
    {
        StationName   = StationName,
        RemoteIP      = ip,
        LocalRecvPort  = LocalRecvPort,
        LocalSendPort  = LocalSendPort,
        RemoteRecvPort = RemoteRecvPort,
        RemoteSendPort = RemoteSendPort,
    };
}
