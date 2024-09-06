using System.Data;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Punisher.Configuration;
using Punisher.Database;
using Punisher.Database.Models;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace Punisher;

[ApiVersion(2, 1)]
public class Punisher : TerrariaPlugin
{
    public override string Name => "Punisher";
    public override string Author => "Average";
    public override string Description => "Enforces hardcore ban logic and implements simple anti-cheat.";
    public override Version Version => new Version(1, 0, 0);

    public static PunisherConfiguration? Configuration { get; set; }
    public static PunisherDatabase Database { get; set; }

    public Punisher(Main game) : base(game)
    {
    }

    public override void Initialize()
    {
        Configuration = PunisherConfiguration.Read();
        IDbConnection db;

        switch (TShock.Config.Settings.StorageType.ToLower())
        {
            case "mysql":
            {
                try
                {
                    var host = TShock.Config.Settings.MySqlHost.Split(':');
                    db = new MySqlConnection
                    {
                        ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4}",
                            host[0],
                            host.Length == 1 ? "3306" : host[1],
                            TShock.Config.Settings.MySqlDbName,
                            TShock.Config.Settings.MySqlUsername,
                            TShock.Config.Settings.MySqlPassword
                        )
                    };
                }
                catch (MySqlException ex)
                {
                    TShock.Log.ConsoleError(ex.ToString());
                    throw new Exception("MySql not setup correctly.");
                }

                break;
            }
            default: // sqlite
            {
                db = new SqliteConnection("Data Source=" + Path.Combine(TShock.SavePath, "RankSystem.sqlite"));
                break;
            }
        }

        Database = new PunisherDatabase(db);
        
        
        GetDataHandlers.KillMe += OnKillMe;
        
        ServerApi.Hooks.NpcStrike.Register(this, OnNpcStrike);
    }
    
    private void OnNpcStrike(NpcStrikeEventArgs e)
    {
        if (Configuration is null)
        {
            return;
        }
        
        // get the player who struck the NPC
        var tsPlayer = TShock.Players[e.Player.whoAmI];

        if(tsPlayer is null) return;
        
        // get the player's account
        var account = tsPlayer.Account;
        
        if(account is null) return;
        
        var weapon = tsPlayer.SelectedItem;

        if(weapon is null) return;
        
        // init weapon information
        WeaponInformation weaponInformation = new WeaponInformation(weapon.netID, tsPlayer.TPlayer);
                
        // add up the modifiers of the armor & accessories
        foreach (var item in tsPlayer.TPlayer.armor)
        {
            if (item.netID == 0) continue;
                    
            Item item1 = new Item();
            item1.SetDefaults(item.netID);
                    
            weaponInformation.ApplyDamageModifier();
        }
                
        // is it over the threshold?
        if (weaponInformation.OverThreshold(e.Damage))
        {
            TShock.Log.ConsoleInfo($"{tsPlayer.Name} has struck an NPC with a weapon over the threshold. Base damage: {weaponInformation.GetBaseDamage()}, Modified damage: {weaponInformation.GetModifiedDamage()}, Dealt damage: {e.Damage}");

            if (Configuration.BanOnCheatDetection)
            {
                tsPlayer.Ban(Configuration.BanReason, "Punisher Anti-Cheat");
            }
            else
            {
                tsPlayer.Kick("Detected by Punisher Anti-Cheat.", true, true);
            }

            e.Damage = weaponInformation.GetModifiedDamage();
        }
        else
        {
            TShock.Log.ConsoleInfo($"{tsPlayer.Name} has struck an NPC with a weapon under the threshold. Base damage: {weaponInformation.GetBaseDamage()}, Modified damage: {weaponInformation.GetModifiedDamage()}, Dealt damage: {e.Damage}");
        }
        
        
    }

    private void OnKillMe(object? sender, GetDataHandlers.KillMeEventArgs e)
    {
        if (Configuration is null)
        {
            return;
        }
        
        // get the player who died
        var player = TShock.Players[e.PlayerId];
        
        // get the player's account
        var account = player?.Account;
        
        if(account is null) return;
        
        // get the player's death reason
        var deathReason = new DeathReason(e.PlayerDeathReason, e.Damage);
        
        // insert the death into the database
        Database.Deaths.InsertDeath(account.ID, DateTime.Now, JsonConvert.SerializeObject(deathReason));

        if (deathReason.DeathReasonType == DeathReasonType.Pvp)
        {
            if (deathReason.AccountId is not null)
            {
                // get the killer's account
                var killerAccount = TShock.UserAccounts.GetUserAccountByID((int) deathReason.AccountId);
                
                if (killerAccount is null) return;
                
                // get the killer's player
                var killerPlayer = TShock.Players.FirstOrDefault(p => p?.Account.ID == killerAccount.ID);
                
                if (killerPlayer is null) return;
                
                // what weapon is the killer using?
                var weapon = killerPlayer.TPlayer.inventory[killerPlayer.TPlayer.selectedItem];
                
                // init weapon information
                WeaponInformation weaponInformation = new WeaponInformation(weapon.netID, killerPlayer.TPlayer);
                
                // add up the modifiers of the armor & accessories
                foreach (var item in killerPlayer.TPlayer.armor)
                {
                    if (item.netID == 0) continue;
                    
                    Item item1 = new Item();
                    item1.SetDefaults(item.netID);
                    
                    weaponInformation.ApplyDamageModifier();
                }
                
                // is it over the threshold?
                if (weaponInformation.OverThreshold(e.Damage))
                {
                    // ban the killer
                    if (Configuration.BanOnCheatDetection)
                    {
                        killerPlayer.Ban(Configuration.BanReason, "Punisher Anti-Cheat");
                    }
                    else
                    {
                        killerPlayer.Kick("Detected by Punisher Anti-Cheat.", true, true);
                    }
                    e.Damage = (short)weaponInformation.GetModifiedDamage();

                }
                else
                {
                    // ban the victim for hardcore
                    if (Configuration.EnableBan)
                    {
                        // if ssc, save the inventory to OUR database, then wipe on tshock-side
                        Database.SavedInventory.InsertSpecificPlayerData(player, player.PlayerData);
                        TShock.CharacterDB.RemovePlayer(player.Account.ID);
                        
                        player.Ban(string.Format(Configuration.BanReason, Configuration.BanDurationInHours), "Punisher Hardcore");
                    }
                }
            }
        }
    }
}