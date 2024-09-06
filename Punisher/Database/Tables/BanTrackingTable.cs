using System.Data;
using MySql.Data.MySqlClient;
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
            new SqlColumn("IsCheater", MySqlDbType.Int32));
        
        sqlCreator.EnsureTableStructure(banTrackingTable);
    }
    
    public void InsertBan(int userId, DateTime dateOccurred, int banDuration, bool isCheater)
    {
        _db.Query("INSERT INTO Punisher_BanTracking (UserID, DateOccurred, BanDuration, IsCheater) VALUES (@0, @1, @2, @3)", userId, dateOccurred, banDuration, isCheater ? 1 : 0);
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