using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfAForge.Tests.Helpers;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace PdfAForge.Tests
{
    [TestClass]
    public class ConvertControllerTests
    {
        private static HttpMessageInvoker _invoker;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            var config = new HttpConfiguration();
            WebApiConfig.Register(config);
            config.EnsureInitialized();
            _invoker = new HttpMessageInvoker(new HttpServer(config));
        }

        [ClassCleanup]
        public static void ClassCleanup() => _invoker?.Dispose();

        [TestMethod]
        public async Task Health_ReturnsOk()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "http://test/api/convert/health");
            var resp = await _invoker.SendAsync(req, CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_WrongContentType_Returns415()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "http://test/api/convert/pdfa3b");
            req.Content = new StringContent("not multipart", Encoding.UTF8, "text/plain");

            var resp = await _invoker.SendAsync(req, CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_MissingPdfFilePart_Returns400()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "http://test/api/convert/pdfa3b");
            var body = new MultipartFormDataContent();
            body.Add(new StringContent("irrelevant"), "other_field", "other.txt");
            req.Content = body;

            var resp = await _invoker.SendAsync(req, CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [TestMethod]
        public async Task ConvertToPdfA3B_ValidPdf_Returns200WithPdfBody()
        {
            var pdf = MinimalPdfFactory.Create();
            var req = new HttpRequestMessage(HttpMethod.Post, "http://test/api/convert/pdfa3b");
            var body = new MultipartFormDataContent();
            body.Add(new ByteArrayContent(pdf), "pdf_file", "test.pdf");
            req.Content = body;

            var resp = await _invoker.SendAsync(req, CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            Assert.AreEqual("application/pdf", resp.Content.Headers.ContentType.MediaType);
        }
    }
}
