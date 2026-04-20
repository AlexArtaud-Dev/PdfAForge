namespace PdfAForge.Models
{
    public class HealthStatus
    {
        public string Status { get; set; }
        public string Service { get; set; }
        public string Version { get; set; }
        public string Timestamp { get; set; }
        public bool IccProfileOk { get; set; }
        public bool LogPathOk { get; set; }
        public long LogDiskFreeMb { get; set; }
        public int MaxFileSizeMb { get; set; }
        public int LogRetentionDays { get; set; }
        public int MaxConcurrentConversions { get; set; }
        public int QueueTimeoutSeconds { get; set; }
        public int ConversionSlotsAvailable { get; set; }
        public long TotalRequests { get; set; }
        public long TotalSuccesses { get; set; }
        public long TotalFailures { get; set; }
        public long TotalBusy { get; set; }
        public double AverageDurationMs { get; set; }
        public string UptimeSince { get; set; }
    }
}