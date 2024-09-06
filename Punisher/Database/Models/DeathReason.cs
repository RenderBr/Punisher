using Newtonsoft.Json;
using Terraria;
using Terraria.DataStructures;
using TShockAPI;

namespace Punisher.Database.Models;

[JsonObject(MemberSerialization.OptIn)]
public class DeathReason
{
    [JsonProperty("Type")]
    public DeathReasonType DeathReasonType { get; set; }
    
    [JsonProperty("AccountId")]
    public int? AccountId { get; set; }
    
    [JsonProperty("ItemKilledWithId")]
    public int? ItemKilledWithId { get; set; }
    
    [JsonProperty("DamageTaken")]
    public int DamageTaken { get; set; }
    
    [JsonProperty("MobId")]
    public int? MobId { get; set; }

    public DeathReason(PlayerDeathReason reason, int damageTaken)
    {
        if (reason._sourcePlayerIndex > -1)
        {
            DamageTaken = damageTaken;
            DeathReasonType = DeathReasonType.Pvp;

            var player = TShock.Players.ElementAtOrDefault(reason._sourcePlayerIndex);

            if (player is null) return;
            
            if (player.IsLoggedIn)
            {
                AccountId = player.Account.ID;
            }
            
            ItemKilledWithId = player.SelectedItem.netID;

        }
        else if (reason._sourceNPCIndex > -1)
        {
            DamageTaken = damageTaken;
            DeathReasonType = DeathReasonType.Npc;

            var npc = Main.npc.ElementAtOrDefault(reason._sourceNPCIndex);
            
            if (npc is null) return;
            
            MobId = npc.netID;
        }
        else
        {
            DamageTaken = damageTaken;
            DeathReasonType = DeathReasonType.Suicide;
        }
    }
}