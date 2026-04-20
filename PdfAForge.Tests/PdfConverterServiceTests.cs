using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfAForge.Services;
using PdfAForge.Tests.Helpers;
using System.Threading.Tasks;

namespace PdfAForge.Tests
{
    [TestClass]
    public class PdfConverterServiceTests
    {
        [TestMethod]
        public async Task ConvertToPdfA3B_ValidPdf_ReturnsSuccess()
        {
            var pdf = MinimalPdfFactory.Create();
            var (result, output) = await PdfConverterService.Current
                .ConvertToPdfA3B(pdf, "test.pdf", "corr-001");

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsFalse(result.IsBusy);
            Assert.IsNotNull(output);
            Assert.IsTrue(output.Length > 0);
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_ValidPdf_OutputStartsWithPdfMagicBytes()
        {
            var pdf = MinimalPdfFactory.Create();
            var (_, output) = await PdfConverterService.Current
                .ConvertToPdfA3B(pdf, "test.pdf", "corr-002");

            Assert.IsNotNull(output);
            // %PDF
            Assert.AreEqual(0x25, output[0]);
            Assert.AreEqual(0x50, output[1]);
            Assert.AreEqual(0x44, output[2]);
            Assert.AreEqual(0x46, output[3]);
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_AlreadyPdfA3_PassesThrough()
        {
            var pdfA3 = MinimalPdfFactory.CreatePdfA3B();
            var (result, output) = await PdfConverterService.Current
                .ConvertToPdfA3B(pdfA3, "already.pdf", "corr-003");

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsNotNull(output);
            // Pass-through returns the original bytes unchanged
            Assert.AreEqual(pdfA3.Length, output.Length);
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_CorruptBytes_ReturnsFailure()
        {
            var corrupt = MinimalPdfFactory.CreateCorrupt();
            var (result, output) = await PdfConverterService.Current
                .ConvertToPdfA3B(corrupt, "corrupt.pdf", "corr-004");

            Assert.IsFalse(result.Success);
            Assert.IsNull(output);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_SlotsRestoredAfterConversion()
        {
            var slotsBefore = PdfConverterService.Current.SlotsAvailable;
            var pdf = MinimalPdfFactory.Create();

            await PdfConverterService.Current.ConvertToPdfA3B(pdf, "test.pdf", "corr-005");

            Assert.AreEqual(slotsBefore, PdfConverterService.Current.SlotsAvailable);
        }
    }
}
