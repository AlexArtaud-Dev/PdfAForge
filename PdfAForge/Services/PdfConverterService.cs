using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Pdfa;
using PdfAForge.Config;
using PdfAForge.Logging;
using PdfAForge.Models;

namespace PdfAForge.Services
{
    /// <summary>
    /// Singleton service responsible for converting PDF files to PDF/A-3B.
    /// Applies a series of pre-conversion fixes to maximise compliance rate.
    /// </summary>
    public class PdfConverterService
    {
        private static readonly PdfConverterService _instance = new PdfConverterService();
        public static PdfConverterService Current => _instance;

        private readonly SemaphoreSlim _semaphore;
        private readonly byte[] _iccProfileBytes;
        public int SlotsAvailable => _semaphore.CurrentCount;

        #region Constants

        // PDF spec table 166 — annotation flag bits
        private const int FLAG_INVISIBLE = 1 << 0; // must be 0
        private const int FLAG_HIDDEN = 1 << 1; // must be 0
        private const int FLAG_PRINT = 1 << 2; // must be 1
        private const int FLAG_NO_VIEW = 1 << 5; // must be 0
        private const int FLAG_TOGGLE_NO_VIEW = 1 << 8; // must be 0

        private const int FLAGS_MUST_BE_ZERO =
            FLAG_INVISIBLE | FLAG_HIDDEN | FLAG_NO_VIEW | FLAG_TOGGLE_NO_VIEW;

        private static readonly HashSet<string> ValidRenderingIntents = new HashSet<string>
        {
            "RelativeColorimetric", "AbsoluteColorimetric", "Perceptual", "Saturation"
        };

        private static readonly HashSet<string> ForbiddenAnnotationTypes = new HashSet<string>
        {
            "3D", "Screen", "Movie", "Sound", "FileAttachment", "Watermark"
        };

        private static readonly HashSet<string> ValidAFRelationships = new HashSet<string>
        {
            "Source", "Data", "Alternative", "Supplement", "EncryptedPayload",
            "FormData", "Schema", "Unspecified"
        };

        #endregion

        private PdfConverterService()
        {
            var max = AppSettings.Current.MaxConcurrentConversions;
            _semaphore = new SemaphoreSlim(max, max);
            _iccProfileBytes = File.ReadAllBytes(AppSettings.Current.IccProfilePath);
        }

        /// <summary>
        /// Converts a PDF byte array to PDF/A-3B.
        /// Waits up to <see cref="AppSettings.QueueTimeoutSeconds"/> for a free conversion slot.
        /// Returns <c>IsBusy=true</c> if no slot became available in time.
        /// </summary>
        public async Task<(ConversionResult result, byte[] outputBytes)> ConvertToPdfA3B(
            byte[] inputPdf, string fileName, string correlationId)
        {
            var result = new ConversionResult
            {
                CorrelationId = correlationId,
                OriginalName = fileName,
                InputSizeKb = inputPdf.Length / 1024
            };

            var settings = AppSettings.Current;
            var slotsAvailable = _semaphore.CurrentCount;

            if (slotsAvailable == 0)
                ConversionLogger.Current.Info(correlationId,
                    $"QUEUED | file={fileName} | all {settings.MaxConcurrentConversions} slot(s) busy, waiting up to {settings.QueueTimeoutSeconds}s");

            var acquired = await _semaphore.WaitAsync(
                TimeSpan.FromSeconds(settings.QueueTimeoutSeconds));

            if (!acquired)
            {
                result.IsBusy = true;
                result.Message = $"Service busy: no conversion slot available after {settings.QueueTimeoutSeconds}s.";
                ConversionLogger.Current.Warn(correlationId,
                    $"QUEUE TIMEOUT | file={fileName} | waited {settings.QueueTimeoutSeconds}s");
                ConversionMetrics.Current.RecordBusy();
                return (result, null);
            }

            var sw = Stopwatch.StartNew();
            ConversionLogger.Current.Info(correlationId,
                $"START conversion | file={fileName} | size={result.InputSizeKb}kb | slots_remaining={_semaphore.CurrentCount}/{settings.MaxConcurrentConversions}");

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
                ConversionMetrics.Current.RecordSuccess(result.DurationMs);

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
                ConversionMetrics.Current.RecordFailure(sw.ElapsedMilliseconds);

                return (result, null);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        #region Core

        private byte[] Convert(byte[] inputPdf, string fileName, string correlationId)
        {
            PdfReader reader;
            try
            {
                reader = new PdfReader(new MemoryStream(inputPdf));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse PDF '{fileName}': {ex.Message} | Type: {ex.GetType().Name}", ex);
            }

            // Pass-through: if already PDF/A-3, return as-is without re-converting
            try
            {
                using (var checkDoc = new PdfDocument(new PdfReader(new MemoryStream(inputPdf))))
                {
                    var xmpMeta = checkDoc.GetXmpMetadata();
                    if (xmpMeta != null)
                    {
                        var xmpStr = iText.Kernel.XMP.XMPMetaFactory.SerializeToString(
                            xmpMeta, new iText.Kernel.XMP.Options.SerializeOptions());

                        if (xmpStr.Contains("<pdfa:part>3</pdfa:part>") ||
                            xmpStr.Contains("pdfa:part>3<") ||
                            xmpStr.Contains("pdfa:part=\"3\"") ||
                            xmpStr.Contains("<pdfaid:part>3</pdfaid:part>") ||
                            xmpStr.Contains("pdfaid:part>3<") ||
                            xmpStr.Contains("pdfaid:part=\"3\""))
                        {
                            reader.Close();
                            ConversionLogger.Current.Info(correlationId,
                                $"File '{fileName}' is already PDF/A-3 compliant - returning as-is.");
                            return inputPdf;
                        }
                    }
                }
            }
            catch
            {
                // No XMP or unreadable — proceed with full conversion
            }

            using (var outputStream = new MemoryStream())
            {
                var writer = new PdfWriter(outputStream);

                PdfOutputIntent outputIntent;
                using (var iccStream = new MemoryStream(_iccProfileBytes))
                {
                    outputIntent = new PdfOutputIntent(
                        "Custom", "",
                        "http://www.color.org",
                        "sRGB IEC61966-2.1",
                        iccStream);
                }

                using (var pdfDoc = new PdfADocument(writer, PdfAConformance.PDF_A_3B, outputIntent))
                {
                    PdfDocument srcDoc;
                    try
                    {
                        srcDoc = new PdfDocument(reader);
                    }
                    catch (OverflowException ex)
                    {
                        throw new InvalidOperationException(
                            $"PDF '{fileName}' contains malformed numeric data: {ex.Message}", ex);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"PDF '{fileName}' could not be opened: {ex.Message} | Type: {ex.GetType().Name}", ex);
                    }

                    // Apply all pre-copy compliance fixes
                    FixAnnotations(srcDoc, correlationId);
                    FixPages(srcDoc, correlationId);
                    FixImages(srcDoc, correlationId);
                    FixFormFields(srcDoc, correlationId);
                    FixFileSpecs(srcDoc, correlationId);
                    FixRenderingIntents(srcDoc, correlationId);

                    try
                    {
                        srcDoc.CopyPagesTo(1, srcDoc.GetNumberOfPages(), pdfDoc);
                    }
                    catch (OverflowException ex)
                    {
                        throw new InvalidOperationException(
                            $"PDF '{fileName}' contains malformed numeric data during page copy: {ex.Message}", ex);
                    }

                    srcDoc.Close();

                    pdfDoc.GetDocumentInfo().SetProducer(AppSettings.Current.ServiceName);
                    pdfDoc.GetDocumentInfo().SetCreator(AppSettings.Current.ServiceName);
                }

                reader.Close();
                return outputStream.ToArray();
            }
        }

        #endregion

        #region Fixers

        /// <summary>
        /// Fixes annotation F flags (Print=1, Invisible/Hidden/NoView/ToggleNoView=0),
        /// strips D and R appearance keys, removes AA/A action entries,
        /// and deletes forbidden annotation types (3D, Screen, Movie, Sound...).
        /// </summary>
        private void FixAnnotations(PdfDocument doc, string correlationId)
        {
            int flagFixed = 0;

            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            {
                var page = doc.GetPage(i);
                var annotations = page.GetAnnotations();
                if (annotations == null) continue;

                var toRemove = new List<PdfAnnotation>();

                foreach (var annotation in annotations)
                {
                    var annotObj = annotation.GetPdfObject();
                    var subtype = annotObj.GetAsName(PdfName.Subtype)?.GetValue();

                    if (subtype != null && ForbiddenAnnotationTypes.Contains(subtype))
                    {
                        toRemove.Add(annotation);
                        continue;
                    }

                    // Fix F flags
                    var flags = annotation.GetFlags();
                    var newFlags = (flags | FLAG_PRINT) & ~FLAGS_MUST_BE_ZERO;
                    if (newFlags != flags)
                    {
                        annotation.SetFlags(newFlags);
                        flagFixed++;
                    }

                    // Strip D and R from appearance dict — only N is allowed
                    var ap = annotation.GetAppearanceDictionary();
                    if (ap != null)
                    {
                        ap.Remove(PdfName.D);
                        ap.Remove(PdfName.R);
                    }

                    // Strip action entries not permitted in PDF/A
                    annotObj.Remove(new PdfName("AA"));
                    annotObj.Remove(new PdfName("A"));
                }

                foreach (var ann in toRemove)
                    page.RemoveAnnotation(ann);

                if (toRemove.Count > 0)
                    ConversionLogger.Current.Info(correlationId,
                        $"Removed {toRemove.Count} forbidden annotation(s) on page {i}.");
            }

            if (flagFixed > 0)
                ConversionLogger.Current.Info(correlationId,
                    $"Fixed F flags on {flagFixed} annotation(s).");
        }

        /// <summary>
        /// Strips the AA (Additional Actions) dictionary from page dictionaries.
        /// JavaScript and other actions are not permitted in PDF/A.
        /// </summary>
        private void FixPages(PdfDocument doc, string correlationId)
        {
            int fixedCount = 0;

            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            {
                var pageDict = doc.GetPage(i).GetPdfObject();
                if (pageDict.ContainsKey(PdfName.AA))
                {
                    pageDict.Remove(PdfName.AA);
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
                ConversionLogger.Current.Info(correlationId,
                    $"Stripped AA from {fixedCount} page(s).");
        }

        /// <summary>
        /// Fixes image and Form XObject dictionaries:
        /// removes Alternates, OPI, PS, Subtype2 keys and sets Interpolate to false.
        /// Deduplicates shared XObjects via indirect reference tracking.
        /// </summary>
        private void FixImages(PdfDocument doc, string correlationId)
        {
            int fixedCount = 0;
            var processed = new HashSet<int>();

            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            {
                var xObjects = doc.GetPage(i).GetResources()?.GetResource(PdfName.XObject);
                if (xObjects == null) continue;

                foreach (var key in xObjects.KeySet())
                {
                    var xObj = xObjects.GetAsStream(key);
                    if (xObj == null) continue;

                    // Skip already-processed shared objects
                    var objNum = xObj.GetIndirectReference()?.GetObjNumber() ?? -1;
                    if (objNum >= 0 && !processed.Add(objNum)) continue;

                    var subtype = xObj.GetAsName(PdfName.Subtype);

                    if (PdfName.Image.Equals(subtype))
                    {
                        if (xObj.ContainsKey(new PdfName("Alternates"))) { xObj.Remove(new PdfName("Alternates")); fixedCount++; }
                        if (xObj.ContainsKey(new PdfName("OPI"))) { xObj.Remove(new PdfName("OPI")); fixedCount++; }

                        var interpolate = xObj.GetAsBoolean(PdfName.Interpolate);
                        if (interpolate != null && interpolate.GetValue())
                        {
                            xObj.Put(PdfName.Interpolate, new PdfBoolean(false));
                            fixedCount++;
                        }
                    }
                    else if (PdfName.Form.Equals(subtype))
                    {
                        if (xObj.ContainsKey(new PdfName("OPI"))) { xObj.Remove(new PdfName("OPI")); fixedCount++; }
                        if (xObj.ContainsKey(new PdfName("PS"))) { xObj.Remove(new PdfName("PS")); fixedCount++; }
                        if (xObj.ContainsKey(new PdfName("Subtype2"))) { xObj.Remove(new PdfName("Subtype2")); fixedCount++; }
                    }
                }
            }

            if (fixedCount > 0)
                ConversionLogger.Current.Info(correlationId,
                    $"Fixed {fixedCount} image/XObject issue(s).");
        }

        /// <summary>
        /// Strips AA and A action entries from AcroForm field dictionaries.
        /// </summary>
        private void FixFormFields(PdfDocument doc, string correlationId)
        {
            int fixedCount = 0;

            var acroForm = doc.GetCatalog().GetPdfObject()
                             .GetAsDictionary(PdfName.AcroForm);
            if (acroForm == null) return;

            var fields = acroForm.GetAsArray(PdfName.Fields);
            if (fields == null) return;

            for (int i = 0; i < fields.Size(); i++)
            {
                var field = fields.GetAsDictionary(i);
                if (field == null) continue;

                if (field.ContainsKey(new PdfName("AA"))) { field.Remove(new PdfName("AA")); fixedCount++; }
                if (field.ContainsKey(new PdfName("A"))) { field.Remove(new PdfName("A")); fixedCount++; }
            }

            if (fixedCount > 0)
                ConversionLogger.Current.Info(correlationId,
                    $"Stripped AA/A from {fixedCount} form field(s).");
        }

        /// <summary>
        /// Ensures every file specification dictionary contains a valid AFRelationship key.
        /// Sets value to "Unspecified" when missing or invalid.
        /// </summary>
        private void FixFileSpecs(PdfDocument doc, string correlationId)
        {
            int fixedCount = 0;

            for (int i = 1; i <= doc.GetNumberOfPdfObjects(); i++)
            {
                var obj = doc.GetPdfObject(i);
                if (obj == null || !obj.IsDictionary()) continue;

                var dict = (PdfDictionary)obj;
                if (!PdfName.Filespec.Equals(dict.GetAsName(PdfName.Type))) continue;

                var afRel = dict.GetAsName(new PdfName("AFRelationship"));
                if (afRel == null || !ValidAFRelationships.Contains(afRel.GetValue()))
                {
                    dict.Put(new PdfName("AFRelationship"), new PdfName("Unspecified"));
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
                ConversionLogger.Current.Info(correlationId,
                    $"Fixed AFRelationship on {fixedCount} file spec dict(s).");
        }

        /// <summary>
        /// Replaces invalid rendering intent values on page dictionaries.
        /// Only RelativeColorimetric, AbsoluteColorimetric, Perceptual and Saturation are valid in PDF/A.
        /// Note: intents embedded in ExtGState or content streams are not handled here.
        /// </summary>
        private void FixRenderingIntents(PdfDocument doc, string correlationId)
        {
            int fixedCount = 0;
            var riKey = new PdfName("RI");

            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            {
                var pageDict = doc.GetPage(i).GetPdfObject();
                var ri = pageDict.GetAsName(riKey);

                if (ri != null && !ValidRenderingIntents.Contains(ri.GetValue()))
                {
                    pageDict.Put(riKey, new PdfName("RelativeColorimetric"));
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
                ConversionLogger.Current.Info(correlationId,
                    $"Fixed {fixedCount} invalid rendering intent(s).");
        }

        #endregion
    }
}