using System.Data;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace Punisher.Tables;

public class DeathsTable
{
    private readonly IDbConnection _db;
    
    private SqlTable deathsTable;
    
    public DeathsTable(IDbConnection db)
    {
        _db = db;

        var sqlCreator = new SqlTableCreator(db,
            db.GetSqlType() == SqlType.Sqlite
                ? (IQueryBuilder)new SqliteQueryCreator()
                : new MysqlQueryCreator());

        deathsTable = new SqlTable("Punisher_Deaths",
            new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
            new SqlColumn("UserID", MySqlDbType.Int32),
            new SqlColumn("DateOccurred", MySqlDbType.DateTime),
            new SqlColumn("DeathReasonJson", MySqlDbType.Text));
        
        sqlCreator.EnsureTableStructure(deathsTable);
    }
    
    public void InsertDeath(int userId, DateTime dateOccurred, string deathReason)
    {
        _db.Query("INSERT INTO Punisher_Deaths (UserID, DateOccurred, DeathReason) VALUES (@0, @1, @2)", userId, dateOccurred, deathReason);
    }
    
    public void DeleteDeath(int id)
    {
        _db.Query("DELETE FROM Punisher_Deaths WHERE ID = @0", id);
    }
    
    public void DeleteDeaths(int userId)
    {
        _db.Query("DELETE FROM Punisher_Deaths WHERE UserID = @0", userId);
    }
    
    public void DeleteAllDeaths()
    {
        _db.Query("DELETE FROM Punisher_Deaths");
    }
    
    
}