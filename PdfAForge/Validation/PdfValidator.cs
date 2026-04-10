using System;
using PdfAForge.Config;

namespace PdfAForge.Validation
{
    public class ValidationError
    {
        public int HttpStatusCode { get; set; }
        public string Message { get; set; }
    }

    public static class PdfValidator
    {
        // PDF magic bytes: %PDF
        private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46 };

        /// <summary>
        /// Validates the input file bytes.
        /// Returns null if valid, or a ValidationError with HTTP status and message.
        /// </summary>
        public static ValidationError Validate(byte[] fileBytes, string fileName)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                return new ValidationError
                {
                    HttpStatusCode = 400,
                    Message = "File is empty."
                };

            if (fileBytes.Length > AppSettings.Current.MaxFileSizeBytes)
                return new ValidationError
                {
                    HttpStatusCode = 413,
                    Message = $"File '{fileName}' exceeds maximum allowed size of {AppSettings.Current.MaxFileSizeMb}MB."
                };

            if (!IsPdf(fileBytes))
                return new ValidationError
                {
                    HttpStatusCode = 415,
                    Message = $"File '{fileName}' is not a valid PDF (invalid magic bytes)."
                };

            return null;
        }

        private static bool IsPdf(byte[] bytes)
        {
            if (bytes.Length < PdfMagicBytes.Length) return false;

            for (int i = 0; i < PdfMagicBytes.Length; i++)
            {
                if (bytes[i] != PdfMagicBytes[i]) return false;
            }

            return true;
        }
    }
}