using System;
using System.Threading;

namespace PdfAForge.Services
{
    public class ConversionMetrics
    {
        private static readonly ConversionMetrics _instance = new ConversionMetrics();
        public static ConversionMetrics Current => _instance;

        private long _totalRequests;
        private long _successes;
        private long _failures;
        private long _busy;
        private long _totalDurationMs;
        private readonly DateTime _startedAt = DateTime.Now;

        private ConversionMetrics() { }

        public void RecordSuccess(long durationMs)
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Increment(ref _successes);
            Interlocked.Add(ref _totalDurationMs, durationMs);
        }

        public void RecordFailure(long durationMs)
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Increment(ref _failures);
            Interlocked.Add(ref _totalDurationMs, durationMs);
        }

        public void RecordBusy()
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Increment(ref _busy);
        }

        public long TotalRequests => Interlocked.Read(ref _totalRequests);
        public long Successes    => Interlocked.Read(ref _successes);
        public long Failures     => Interlocked.Read(ref _failures);
        public long Busy         => Interlocked.Read(ref _busy);

        public double AverageDurationMs
        {
            get
            {
                var s = Interlocked.Read(ref _successes);
                return s > 0 ? Math.Round((double)Interlocked.Read(ref _totalDurationMs) / s, 1) : 0;
            }
        }

        public string UptimeSince => _startedAt.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
