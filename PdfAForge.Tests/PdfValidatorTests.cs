using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfAForge.Config;
using PdfAForge.Validation;

namespace PdfAForge.Tests
{
    [TestClass]
    public class PdfValidatorTests
    {
        private static readonly byte[] ValidMagic = { 0x25, 0x50, 0x44, 0x46 }; // %PDF

        private static byte[] MakePdf(int extraBytes = 100)
        {
            var buf = new byte[ValidMagic.Length + extraBytes];
            ValidMagic.CopyTo(buf, 0);
            return buf;
        }

        [TestMethod]
        public void Validate_NullBytes_Returns400()
        {
            var err = PdfValidator.Validate(null, "test.pdf");
            Assert.IsNotNull(err);
            Assert.AreEqual(400, err.HttpStatusCode);
        }

        [TestMethod]
        public void Validate_EmptyBytes_Returns400()
        {
            var err = PdfValidator.Validate(new byte[0], "test.pdf");
            Assert.IsNotNull(err);
            Assert.AreEqual(400, err.HttpStatusCode);
        }

        [TestMethod]
        public void Validate_ExceedsMaxSize_Returns413()
        {
            // MaxFileSizeMb=1 in app.config — create 1MB+1 bytes with valid magic
            var bytes = MakePdf(AppSettings.Current.MaxFileSizeMb * 1024 * 1024);
            var err = PdfValidator.Validate(bytes, "big.pdf");
            Assert.IsNotNull(err);
            Assert.AreEqual(413, err.HttpStatusCode);
        }

        [TestMethod]
        public void Validate_InvalidMagicBytes_Returns415()
        {
            var err = PdfValidator.Validate(new byte[] { 0x00, 0x01, 0x02, 0x03 }, "fake.pdf");
            Assert.IsNotNull(err);
            Assert.AreEqual(415, err.HttpStatusCode);
        }

        [TestMethod]
        public void Validate_TooShortForMagicCheck_Returns415()
        {
            var err = PdfValidator.Validate(new byte[] { 0x25, 0x50 }, "short.pdf");
            Assert.IsNotNull(err);
            Assert.AreEqual(415, err.HttpStatusCode);
        }

        [TestMethod]
        public void Validate_ValidPdf_ReturnsNull()
        {
            var err = PdfValidator.Validate(MakePdf(), "valid.pdf");
            Assert.IsNull(err);
        }

        [TestMethod]
        public void Validate_ExactlyAtLimit_ReturnsNull()
        {
            // Exactly at MaxFileSizeMb — should pass
            var bytes = MakePdf(AppSettings.Current.MaxFileSizeMb * 1024 * 1024 - ValidMagic.Length);
            var err = PdfValidator.Validate(bytes, "edge.pdf");
            Assert.IsNull(err);
        }
    }
}
