using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfAForge.Config;

namespace PdfAForge.Tests
{
    [TestClass]
    public class TestSetup
    {
        [AssemblyInitialize]
        public static void Init(TestContext _)
        {
            // Ensures log directory exists and ICC profile path is valid before any test runs.
            AppSettings.Current.Validate();
        }
    }
}
