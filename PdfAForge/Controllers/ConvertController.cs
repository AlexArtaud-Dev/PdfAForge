using PdfAForge.Config;
using PdfAForge.Logging;
using PdfAForge.Models;
using PdfAForge.Services;
using PdfAForge.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace PdfAForge.Controllers
{
    [RoutePrefix("api/convert")]
    public class ConvertController : ApiController
    {
        /// <summary>
        /// Converts a PDF to PDF/A-3B.
        /// POST /api/convert/pdfa3b
        /// Content-Type: multipart/form-data
        /// Body: pdf_file (binary)
        /// Headers: X-Correlation-Id (optional, generated if absent)
        /// Response: PDF/A-3B binary (application/pdf)
        /// </summary>
        [HttpPost]
        [Route("pdfa3b")]
        public async Task<HttpResponseMessage> ConvertToPdfA3B()
        {
            var correlationId = GetOrCreateCorrelationId();

            ConversionLogger.Current.Info(correlationId,
                $"REQUEST received | ip={GetClientIp()}");

            if (!Request.Content.IsMimeMultipartContent())
            {
                ConversionLogger.Current.Warn(correlationId,
                    "Rejected: content is not multipart/form-data");

                return ErrorResponse(HttpStatusCode.UnsupportedMediaType,
                    "Request must be multipart/form-data.", correlationId);
            }

            // --- Content-Length guard (best-effort: header may be absent) ---
            var contentLength = Request.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > AppSettings.Current.MaxFileSizeBytes)
            {
                ConversionLogger.Current.Warn(correlationId,
                    $"Rejected: Content-Length {contentLength.Value / 1024}kb exceeds {AppSettings.Current.MaxFileSizeMb}MB limit");
                return ErrorResponse(HttpStatusCode.RequestEntityTooLarge,
                    $"Request exceeds the {AppSettings.Current.MaxFileSizeMb} MB limit.", correlationId);
            }

            // --- Read multipart ---
            MultipartMemoryStreamProvider provider;
            try
            {
                provider = await Request.Content.ReadAsMultipartAsync();
            }
            catch (Exception ex)
            {
                ConversionLogger.Current.Error(correlationId,
                    "Failed to read multipart content", ex);

                return ErrorResponse(HttpStatusCode.BadRequest,
                    $"Failed to read multipart content: {ex.Message}", correlationId);
            }

            // --- Find pdf_file part ---
            HttpContent filePart = null;
            string fileName = "document.pdf";

            foreach (var part in provider.Contents)
            {
                if (part.Headers.ContentDisposition?.Name?.Trim('"') == "pdf_file")
                {
                    filePart = part;
                    fileName = part.Headers.ContentDisposition?.FileName?.Trim('"') ?? fileName;
                    break;
                }
            }

            if (filePart == null)
            {
                ConversionLogger.Current.Warn(correlationId,
                    "Rejected: missing 'pdf_file' part");

                return ErrorResponse(HttpStatusCode.BadRequest,
                    "Missing 'pdf_file' part in multipart body.", correlationId);
            }

            // --- Read bytes ---
            var inputBytes = await filePart.ReadAsByteArrayAsync();

            // --- Validate ---
            var validationError = PdfValidator.Validate(inputBytes, fileName);
            if (validationError != null)
            {
                ConversionLogger.Current.Warn(correlationId,
                    $"Validation failed: {validationError.Message}");

                return ErrorResponse(
                    (HttpStatusCode)validationError.HttpStatusCode,
                    validationError.Message, correlationId);
            }

            // --- Convert ---
            var (result, outputBytes) = await PdfConverterService.Current
                .ConvertToPdfA3B(inputBytes, fileName, correlationId);

            if (result.IsBusy)
            {
                var busy = ErrorResponse(HttpStatusCode.ServiceUnavailable,
                    result.Message, correlationId);
                busy.Headers.Add("Retry-After", "10");
                return busy;
            }

            if (!result.Success)
                return ErrorResponse(HttpStatusCode.InternalServerError,
                    result.Message, correlationId);

            // --- Return PDF/A-3B ---
            var outputName = Path.GetFileNameWithoutExtension(fileName) + "_pdfa3b.pdf";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(outputBytes)
            };

            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");

            response.Content.Headers.ContentDisposition =
                new ContentDispositionHeaderValue("attachment")
                {
                    FileName = outputName
                };

            response.Headers.Add("X-Correlation-Id", correlationId);
            response.Headers.Add("X-Input-Size-Kb", result.InputSizeKb.ToString());
            response.Headers.Add("X-Output-Size-Kb", result.OutputSizeKb.ToString());
            response.Headers.Add("X-Duration-Ms", result.DurationMs.ToString());

            return response;
        }

        /// <summary>
        /// Enriched health check.
        /// GET /api/convert/health
        /// </summary>
        [HttpGet]
        [Route("health")]
        public IHttpActionResult Health()
        {
            var settings = AppSettings.Current;

            long diskFreeMb = 0;
            try
            {
                var drive = new System.IO.DriveInfo(
                    Path.GetPathRoot(settings.LogPath));
                diskFreeMb = drive.AvailableFreeSpace / 1024 / 1024;
            }
            catch { }

            var metrics = ConversionMetrics.Current;

            var status = new HealthStatus
            {
                Status = "ok",
                Service = settings.ServiceName,
                Version = settings.ServiceVersion,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                IccProfileOk = File.Exists(settings.IccProfilePath),
                LogPathOk = Directory.Exists(settings.LogPath),
                LogDiskFreeMb = diskFreeMb,
                MaxFileSizeMb = settings.MaxFileSizeMb,
                LogRetentionDays = settings.LogRetentionDays,
                MaxConcurrentConversions = settings.MaxConcurrentConversions,
                QueueTimeoutSeconds = settings.QueueTimeoutSeconds,
                ConversionSlotsAvailable = PdfConverterService.Current.SlotsAvailable,
                TotalRequests = metrics.TotalRequests,
                TotalSuccesses = metrics.Successes,
                TotalFailures = metrics.Failures,
                TotalBusy = metrics.Busy,
                AverageDurationMs = metrics.AverageDurationMs,
                UptimeSince = metrics.UptimeSince
            };

            if (!status.IccProfileOk || !status.LogPathOk)
                status.Status = "degraded";

            return Ok(status);
        }

        // --- Helpers ---

        private HttpResponseMessage ErrorResponse(
            HttpStatusCode code, string message, string correlationId)
        {
            var response = Request.CreateErrorResponse(code, message);
            response.Headers.Add("X-Correlation-Id", correlationId);
            return response;
        }

        private string GetOrCreateCorrelationId()
        {
            IEnumerable<string> values;
            if (Request.Headers.TryGetValues("X-Correlation-Id", out values))
            {
                var existing = string.Join("", values).Trim();
                if (!string.IsNullOrEmpty(existing)) return existing;
            }
            return Guid.NewGuid().ToString();
        }

        private string GetClientIp()
        {
            try
            {
                if (Request.Properties.ContainsKey("MS_HttpContext"))
                {
                    var ctx = Request.Properties["MS_HttpContext"]
                        as System.Web.HttpContextWrapper;
                    return ctx?.Request.UserHostAddress ?? "unknown";
                }
            }
            catch { }
            return "unknown";
        }
    }
}