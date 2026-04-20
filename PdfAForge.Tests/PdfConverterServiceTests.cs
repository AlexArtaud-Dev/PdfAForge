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
            Assert.AreEqual(0x25, output[0]); // %
            Assert.AreEqual(0x50, output[1]); // P
            Assert.AreEqual(0x44, output[2]); // D
            Assert.AreEqual(0x46, output[3]); // F
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_AlreadyPdfA3_PassesThrough()
        {
            var pdf = MinimalPdfFactory.Create();

            // First conversion produces a PDF/A-3B with XMP metadata
            var (firstResult, pdfA3Bytes) = await PdfConverterService.Current
                .ConvertToPdfA3B(pdf, "first.pdf", "corr-003a");
            Assert.IsTrue(firstResult.Success, firstResult.Message);

            // Second conversion on the PDF/A-3B output: pass-through should fire,
            // returning the input bytes unchanged (same length)
            var (secondResult, output) = await PdfConverterService.Current
                .ConvertToPdfA3B(pdfA3Bytes, "second.pdf", "corr-003b");
            Assert.IsTrue(secondResult.Success, secondResult.Message);
            Assert.AreEqual(pdfA3Bytes.Length, output.Length);
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
