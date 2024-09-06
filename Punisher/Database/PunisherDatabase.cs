using System.Data;
using MySql.Data.MySqlClient;
using Punisher.Tables;
using TShockAPI.DB;

namespace Punisher.Database;

public class PunisherDatabase
{
    private readonly IDbConnection _db;
    
    public DeathsTable Deaths { get; set; }
    public SavedInventoryTable SavedInventory { get; set; }
    
    public PunisherDatabase(IDbConnection db)
    {
        _db = db;

        var sqlCreator = new SqlTableCreator(db,
            db.GetSqlType() == SqlType.Sqlite
                ? (IQueryBuilder)new SqliteQueryCreator()
                : new MysqlQueryCreator());

        Deaths = new DeathsTable(db);
        SavedInventory = new SavedInventoryTable(db);
        
    }
}