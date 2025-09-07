namespace Certio.Domain.Documents
{
    public class StorageInfo
    {
        public double UsedGB { get; set; }
        public double TotalGB { get; set; }
        public int DocumentCount { get; set; }
        public int SharedCount { get; set; }
        public int RecentCount { get; set; }
        
        public double UsagePercentage => TotalGB > 0 ? (UsedGB / TotalGB) * 100 : 0;
    }
}
