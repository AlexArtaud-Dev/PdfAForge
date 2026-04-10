using System;
using System.Web;
using System.Web.Http;
using PdfAForge.Config;
using PdfAForge.Logging;

namespace PdfAForge
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            try
            {
                // Validate all settings at startup — fail fast
                AppSettings.Current.Validate();

                GlobalConfiguration.Configure(WebApiConfig.Register);

                // Cleanup old logs on startup
                ConversionLogger.Current.CleanOldLogs();

                ConversionLogger.Current.StartupInfo(
                    $"Service started | version={AppSettings.Current.ServiceVersion}");
                ConversionLogger.Current.StartupInfo(
                    $"LogPath={AppSettings.Current.LogPath} | " +
                    $"Retention={AppSettings.Current.LogRetentionDays}d | " +
                    $"MaxFile={AppSettings.Current.MaxFileSizeMb}MB");
                ConversionLogger.Current.StartupInfo(
                    $"IccProfile={AppSettings.Current.IccProfilePath}");
            }
            catch (Exception ex)
            {
                // Log to Windows Event Log if our logger is not yet available
                System.Diagnostics.EventLog.WriteEntry(
                    "Application",
                    $"PdfAForge startup failed: {ex.Message}",
                    System.Diagnostics.EventLogEntryType.Error);

                throw; // Let IIS surface the error
            }
        }

        protected void Application_End()
        {
            ConversionLogger.Current.StartupInfo("Service stopped.");
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex != null)
                ConversionLogger.Current.Error("GLOBAL", "Unhandled application error", ex);
        }
    }
}