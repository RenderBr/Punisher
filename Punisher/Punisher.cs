using System.Data;
using System.Runtime.CompilerServices;
using GetText;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Punisher.Configuration;
using Punisher.Database;
using Punisher.Database.Models;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

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

    public static DateTime LastUnbanTime { get; set; } = DateTime.MinValue; 
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
                db = new SqliteConnection("Data Source=" + Path.Combine(TShock.SavePath, "Punisher.sqlite"));
                break;
            }
        }

        Database = new PunisherDatabase(db);

        GetDataHandlers.KillMe += OnKillMe;

        ServerApi.Hooks.NpcStrike.Register(this, OnNpcStrike);
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

        TShockAPI.Hooks.GeneralHooks.ReloadEvent += (x) =>
        {
            Configuration = PunisherConfiguration.Read();
            x.Player.SendInfoMessage("Punisher configuration reloaded.");
        };
    }

    private void OnGameUpdate(EventArgs args)
    {
        if (Configuration is null)
        {
            return;
        }

        if (DateTime.Now.Subtract(LastUnbanTime).TotalHours >= Configuration.BanDurationInHours)
        {
            Database.BanTracking.UnbanLegitBans();
            LastUnbanTime = DateTime.Now;
        }   
    }

    private void OnNpcStrike(NpcStrikeEventArgs e)
    {
        if (Configuration is null)
        {
            return;
        }

        // get the player who struck the NPC
        var tsPlayer = TShock.Players[e.Player.whoAmI];

        if (tsPlayer is null) return;

        // get the player's account
        var account = tsPlayer.Account;

        if (account is null) return;

        var weapon = tsPlayer.SelectedItem;

        if (weapon is null) return;

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
        if (weaponInformation.OverThreshold(e.Damage) && Configuration.EnableAntiCheat)
        {
            if (Configuration.BanOnCheatDetection)
            {
                BanUser(tsPlayer, Configuration.BanReason, "Punisher Anti-Cheat", true);
            }
            else
            {
                tsPlayer.Kick("Detected by Punisher Anti-Cheat.", true, true);
            }

            e.Damage = weaponInformation.GetModifiedDamage();
        }
    }

    public void BanUser(TSPlayer player, string reason, string adminUserName, bool cheater)
    {
        BanManager bans = TShock.Bans;
        var interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 2);
        DateTime now = DateTime.UtcNow;
        DateTime then = DateTime.MaxValue;

        List<AddBanResult> banList = new();

        if (player.IsLoggedIn && player.HasPermission("punisher.admin"))
        {
            // only ban account if admin
            interpolatedStringHandler.AppendFormatted<Identifier>(Identifier.Account);
            interpolatedStringHandler.AppendFormatted(player.Account.Name);
            string stringAndClear3 = interpolatedStringHandler.ToStringAndClear();
            banList.Add(bans.InsertBan(stringAndClear3, reason, adminUserName, now, then));
        }
        else
        {
            // ban account, ip, and uuid
            interpolatedStringHandler.AppendFormatted<Identifier>(Identifier.Account);
            interpolatedStringHandler.AppendFormatted(player.Account.Name);
            string stringAndClear3 = interpolatedStringHandler.ToStringAndClear();

            interpolatedStringHandler.AppendFormatted<Identifier>(Identifier.IP);
            interpolatedStringHandler.AppendFormatted(player.IP);
            string stringAndClear4 = interpolatedStringHandler.ToStringAndClear();

            interpolatedStringHandler.AppendFormatted<Identifier>(Identifier.UUID);
            interpolatedStringHandler.AppendFormatted(player.UUID);
            string stringAndClear5 = interpolatedStringHandler.ToStringAndClear();

            banList.AddRange(new[]
            {
                bans.InsertBan(stringAndClear3, reason, adminUserName, now, then),
                bans.InsertBan(stringAndClear4, reason, adminUserName, now, then),
                bans.InsertBan(stringAndClear5, reason, adminUserName, now, then)
            });
        }

        if (string.IsNullOrWhiteSpace(adminUserName))
        {
            TSPlayer.All.SendInfoMessage("{0} was banned for '{1}'.", (object)player.Name, (object)reason);
        }
        else
        {
            TSPlayer.All.SendInfoMessage("{0} banned {1} for '{2}'.",
                (object)adminUserName, (object)player.Name, (object)reason);
        }

        Database.SavedInventory.InsertSpecificPlayerData(player, player.PlayerData);
        TShock.CharacterDB.RemovePlayer(player.Account.ID);

        player.Disconnect("Banned: " + reason);

        string banIds = string.Join(", ", banList.Select(b => b.Ban.TicketNumber));

        Database.BanTracking.InsertBan(player.Account.ID, now, now.Subtract(then).Hours,
            cheater, banIds);
    }

    private void OnKillMe(object? sender, GetDataHandlers.KillMeEventArgs e)
    {
        if (Configuration is null)
        {
            return;
        }
        
        // get the player who died
        var player = e.Player;

        // get the player's account
        if (!player.IsLoggedIn)
        {
            return;
        }

        var account = player.Account;

        // get the player's death reason
        var deathReason = new DeathReason(e.PlayerDeathReason, e.Damage);

        // insert the death into the database
        Database.Deaths.InsertDeath(account.ID, DateTime.Now, JsonConvert.SerializeObject(deathReason));

        if (deathReason.DeathReasonType == DeathReasonType.Pvp)
        {
            if (deathReason.AccountId is not null)
            {
                // get the killer's account
                var killerAccount = TShock.UserAccounts.GetUserAccountByID((int)deathReason.AccountId);

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
                if (weaponInformation.OverThreshold(e.Damage) && Configuration.EnableAntiCheat)
                {
                    // ban the killer
                    if (Configuration.BanOnCheatDetection)
                    {
                        BanUser(killerPlayer, Configuration.BanReason, "Punisher Anti-Cheat", true);
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
                        BanUser(player, string.Format(Configuration.BanReason, Configuration.BanDurationInHours),
                            "Punisher Hardcore", false);
                    }
                }
            }
        }
        else
        {
            // ban the victim for hardcore
            if (Configuration.EnableBan)
            {
                BanUser(player, string.Format(Configuration.BanReason, Configuration.BanDurationInHours),
                    "Punisher Hardcore", false);
            }
        }
    }
}