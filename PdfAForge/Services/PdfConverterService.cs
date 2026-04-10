using System;
using System.Diagnostics;
using System.IO;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Pdfa;
using PdfAForge.Config;
using PdfAForge.Logging;
using PdfAForge.Models;

namespace PdfAForge.Services
{
    public class PdfConverterService
    {
        private static readonly PdfConverterService _instance = new PdfConverterService();
        public static PdfConverterService Current => _instance;

        private PdfConverterService() { }

        /// <summary>
        /// Converts a PDF byte array to PDF/A-3B.
        /// Returns a ConversionResult with success flag, metrics, and output bytes.
        /// Output bytes are set only on success.
        /// </summary>
        public (ConversionResult result, byte[] outputBytes) ConvertToPdfA3B(
            byte[] inputPdf, string fileName, string correlationId)
        {
            var result = new ConversionResult
            {
                CorrelationId = correlationId,
                OriginalName = fileName,
                InputSizeKb = inputPdf.Length / 1024
            };

            var sw = Stopwatch.StartNew();

            ConversionLogger.Current.Info(correlationId,
                $"START conversion | file={fileName} | size={result.InputSizeKb}kb");

            try
            {
                var outputBytes = Convert(inputPdf, fileName, correlationId);

                sw.Stop();
                result.Success = true;
                result.OutputSizeKb = outputBytes.Length / 1024;
                result.DurationMs = sw.ElapsedMilliseconds;
                result.Message = "Conversion successful.";

                ConversionLogger.Current.ConversionSuccess(
                    correlationId, fileName,
                    result.InputSizeKb, result.OutputSizeKb, result.DurationMs);

                return (result, outputBytes);
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success = false;
                result.DurationMs = sw.ElapsedMilliseconds;
                result.Message = $"Conversion failed: {ex.Message}";

                ConversionLogger.Current.ConversionError(
                    correlationId, fileName, ex.Message, ex);

                return (result, null);
            }
        }

        // --- Private ---

        private byte[] Convert(byte[] inputPdf, string fileName, string correlationId)
        {
            var iccPath = AppSettings.Current.IccProfilePath;

            using (var inputStream = new MemoryStream(inputPdf))
            using (var outputStream = new MemoryStream())
            {
                var reader = new PdfReader(inputStream);
                var writer = new PdfWriter(outputStream);

                PdfOutputIntent outputIntent;
                using (var iccStream = new FileStream(iccPath, FileMode.Open, FileAccess.Read))
                {
                    outputIntent = new PdfOutputIntent(
                        "Custom", "",
                        "http://www.color.org",
                        "sRGB IEC61966-2.1",
                        iccStream);
                }

                using (var pdfDoc = new PdfADocument(writer, PdfAConformance.PDF_A_3B, outputIntent))
                {
                    var srcDoc = new PdfDocument(reader);

                    FixAnnotations(srcDoc, correlationId);

                    srcDoc.CopyPagesTo(1, srcDoc.GetNumberOfPages(), pdfDoc);
                    srcDoc.Close();

                    pdfDoc.GetDocumentInfo().SetProducer(AppSettings.Current.ServiceName);
                    pdfDoc.GetDocumentInfo().SetCreator(AppSettings.Current.ServiceName);
                }

                reader.Close();
                return outputStream.ToArray();
            }
        }

        /// <summary>
        /// Fixes annotations to be PDF/A compliant.
        /// PDF/A-3B requires all annotations to have the F (flags) key set.
        /// </summary>
        private void FixAnnotations(PdfDocument doc, string correlationId)
        {
            int fixedCount = 0;

            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            {
                var annotations = doc.GetPage(i).GetAnnotations();
                if (annotations == null) continue;

                foreach (var annotation in annotations)
                {
                    if (annotation.GetFlags() == 0)
                    {
                        annotation.SetFlags(PdfAnnotation.PRINT);
                        fixedCount++;
                    }
                }
            }

            if (fixedCount > 0)
                ConversionLogger.Current.Info(correlationId,
                    $"Fixed {fixedCount} annotation(s) missing F flag.");
        }
    }
}