using iText.Kernel.Pdf;
using iText.Pdfa;
using PdfAForge.Config;
using System.IO;

namespace PdfAForge.Tests.Helpers
{
    /// <summary>
    /// Generates minimal in-memory PDFs for use in tests via iText.
    /// </summary>
    public static class MinimalPdfFactory
    {
        /// <summary>Creates a minimal valid PDF (not PDF/A compliant).</summary>
        public static byte[] Create()
        {
            using (var ms = new MemoryStream())
            {
                using (var doc = new PdfDocument(new PdfWriter(ms)))
                    doc.AddNewPage();
                return ms.ToArray();
            }
        }

        /// <summary>Creates a minimal PDF/A-3B compliant document.</summary>
        public static byte[] CreatePdfA3B()
        {
            using (var ms = new MemoryStream())
            {
                using (var iccStream = new FileStream(
                    AppSettings.Current.IccProfilePath, FileMode.Open, FileAccess.Read))
                {
                    var intent = new PdfOutputIntent(
                        "Custom", "", "http://www.color.org", "sRGB IEC61966-2.1", iccStream);

                    using (var doc = new PdfADocument(
                        new PdfWriter(ms), PdfAConformance.PDF_A_3B, intent))
                        doc.AddNewPage();
                }
                return ms.ToArray();
            }
        }

        /// <summary>Returns bytes that look like a PDF (valid magic) but are structurally corrupt.</summary>
        public static byte[] CreateCorrupt()
        {
            return new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0xFF, 0x00, 0x01, 0x02, 0x03 };
        }
    }
}
