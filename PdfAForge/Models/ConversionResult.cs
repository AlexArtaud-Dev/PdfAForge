namespace PdfAForge.Models
{
    public class ConversionResult
    {
        public bool Success { get; set; }
        public bool IsBusy { get; set; }
        public string Message { get; set; }
        public string CorrelationId { get; set; }
        public string OriginalName { get; set; }
        public long InputSizeKb { get; set; }
        public long OutputSizeKb { get; set; }
        public long DurationMs { get; set; }
    }
}