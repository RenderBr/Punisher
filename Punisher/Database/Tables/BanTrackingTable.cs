using System.Data;
using MySql.Data.MySqlClient;
using Punisher.Database.Models;
using TShockAPI;
using TShockAPI.DB;

namespace Punisher.Tables;

public class BanTrackingTable
{
    private readonly IDbConnection _db;

    private SqlTable banTrackingTable;
    
    public BanTrackingTable(IDbConnection db)
    {
        _db = db;

        var sqlCreator = new SqlTableCreator(db,
            db.GetSqlType() == SqlType.Sqlite
                ? (IQueryBuilder)new SqliteQueryCreator()
                : new MysqlQueryCreator());

        banTrackingTable = new SqlTable("Punisher_BanTracking",
            new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
            new SqlColumn("UserID", MySqlDbType.Int32),
            new SqlColumn("DateOccurred", MySqlDbType.DateTime),
            new SqlColumn("BanDuration", MySqlDbType.Int32),
            new SqlColumn("BanIds", MySqlDbType.Text),
            new SqlColumn("IsCheater", MySqlDbType.Int32));
        
        sqlCreator.EnsureTableStructure(banTrackingTable);
    }
    
    public void InsertBan(int userId, DateTime dateOccurred, int banDuration, bool isCheater, string banIds)
    {
        _db.Query("INSERT INTO Punisher_BanTracking (UserID, DateOccurred, BanDuration, IsCheater, BanIds) VALUES (@0, @1, @2, @3, @4)", userId, dateOccurred, banDuration, isCheater ? 1 : 0, banIds);
    }
    
    public List<BanTracking> GetAllLegitBans()
    {
        var result = _db.QueryReader("SELECT * FROM Punisher_BanTracking WHERE IsCheater = 0");
        
        var bans = new List<BanTracking>();
        while (result.Read())
        {
            bans.Add(new BanTracking(result.Get<int>("UserID"), result.Get<DateTime>("DateOccurred"), result.Get<int>("BanDuration"), false, result.Get<string>("BanIds")));
        }
        
        return bans;
    }
    
    public void UnbanLegitBans()
    {
        var bans = GetAllLegitBans();
        
        foreach (var ban in bans)
        {
            if(string.IsNullOrWhiteSpace(ban.BanIds))
            {
                continue;
            }
            var ids = ban.BanIds.Split(',');
            foreach (var id in ids)
            {
                TShock.Bans.RemoveBan(Convert.ToInt32(id));
            }
            
            DeleteBans(ban.UserID);
        }
    }
    
    public void DeleteBan(int id)
    {
        _db.Query("DELETE FROM Punisher_BanTracking WHERE ID = @0", id);
    }
    
    public void DeleteBans(int userId)
    {
        _db.Query("DELETE FROM Punisher_BanTracking WHERE UserID = @0", userId);
    }
    
    public void DeleteAllBans()
    {
        _db.Query("DELETE FROM Punisher_BanTracking");
    }
}