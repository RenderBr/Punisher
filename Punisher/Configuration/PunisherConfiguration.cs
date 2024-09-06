using Newtonsoft.Json;
using Terraria.DataStructures;
using TShockAPI;

namespace Punisher.Configuration;

[JsonObject(MemberSerialization.OptIn)]
public class PunisherConfiguration
{
    public static PunisherConfiguration Instance { get; set; } = new();
    
    private string _configPath => Path.Combine(TShock.SavePath,"punisher.json");
    
    [JsonProperty]
    public int BanDurationInHours { get; set; } = 5;
    
    [JsonProperty]
    public string BanReason { get; set; } = "You have died in hard-core mode. You are banned for {0} hours.";
    
    [JsonProperty]
    public bool EnableAntiCheat { get; set; } = true;
    
    [JsonProperty]
    public bool EnableBan { get; set; } = true;

    [JsonProperty] 
    public int Threshold { get; set; } = 150;

    [JsonProperty] public bool BanOnCheatDetection { get; set; } = false;
    public static PunisherConfiguration Read()
    {
        if (!File.Exists(Instance._configPath))
        {
            return Write();
        }
        
        var punisherConfig = JsonConvert.DeserializeObject<PunisherConfiguration>(File.ReadAllText(Instance._configPath));
        
        if (punisherConfig != null)
        {
            Instance = punisherConfig;
        }
        
        return Instance;
    }

    public static PunisherConfiguration Write()
    {
        File.WriteAllText(Instance._configPath, JsonConvert.SerializeObject(Instance, Formatting.Indented));
        return Instance;
    }
    
}