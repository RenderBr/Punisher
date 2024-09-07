namespace Punisher.Database.Models;

public class BanTracking
{
    public int UserID { get; set; }
    public DateTime DateOccurred { get; set; }
    public int BanDuration { get; set; }
    public string BanIds { get; set; }
    public bool IsCheater { get; set; }
    
    public BanTracking(int userId, DateTime dateOccurred, int banDuration, bool isCheater, string banIds)
    {
        UserID = userId;
        DateOccurred = dateOccurred;
        BanDuration = banDuration;
        IsCheater = isCheater;
        BanIds = banIds;
    }
}