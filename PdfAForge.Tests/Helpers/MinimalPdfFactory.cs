using System.Text;

namespace PdfAForge.Tests.Helpers
{
    /// <summary>
    /// Generates minimal in-memory PDFs without any external dependency.
    /// Offsets in the xref table are computed at runtime so they are always exact.
    /// </summary>
    public static class MinimalPdfFactory
    {
        /// <summary>Creates a minimal structurally valid PDF (not PDF/A compliant).</summary>
        public static byte[] Create()
        {
            var sb = new StringBuilder();
            var off = new int[4];

            sb.Append("%PDF-1.4\n");

            off[1] = sb.Length;
            sb.Append("1 0 obj\n<</Type /Catalog /Pages 2 0 R>>\nendobj\n");

            off[2] = sb.Length;
            sb.Append("2 0 obj\n<</Type /Pages /Kids [3 0 R] /Count 1>>\nendobj\n");

            off[3] = sb.Length;
            sb.Append("3 0 obj\n<</Type /Page /Parent 2 0 R /MediaBox [0 0 3 3]>>\nendobj\n");

            int xrefAt = sb.Length;
            sb.Append("xref\n0 4\n");
            sb.AppendFormat("{0:D10} 65535 f \n", 0);
            sb.AppendFormat("{0:D10} 00000 n \n", off[1]);
            sb.AppendFormat("{0:D10} 00000 n \n", off[2]);
            sb.AppendFormat("{0:D10} 00000 n \n", off[3]);
            sb.Append("trailer\n<</Size 4 /Root 1 0 R>>\nstartxref\n");
            sb.Append(xrefAt);
            sb.Append("\n%%EOF\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        /// <summary>Returns bytes with valid PDF magic but corrupt structure (unparseable by iText).</summary>
        public static byte[] CreateCorrupt()
            => new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0xFF, 0x00, 0x01, 0x02, 0x03 };
    }
}
