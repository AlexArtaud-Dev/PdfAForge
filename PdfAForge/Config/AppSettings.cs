using System;
using System.Configuration;
using System.IO;

namespace PdfAForge.Config
{
    /// <summary>
    /// Typed access to Web.config appSettings.
    /// Validates required settings at startup.
    /// </summary>
    public class AppSettings
    {
        private static readonly Lazy<AppSettings> _instance =
            new Lazy<AppSettings>(() => new AppSettings());

        public static AppSettings Current => _instance.Value;

        // --- Logging ---
        public string LogPath { get; private set; }
        public int LogRetentionDays { get; private set; }

        // --- File limits ---
        public int MaxFileSizeMb { get; private set; }
        public long MaxFileSizeBytes => (long)MaxFileSizeMb * 1024 * 1024;

        // --- iText ---
        public string IccProfilePath { get; private set; }

        // --- Service metadata ---
        public string ServiceVersion { get; private set; }
        public string ServiceName { get; private set; }

        private AppSettings()
        {
            LogPath = Require("LogPath");
            LogRetentionDays = RequireInt("LogRetentionDays", 1, 365);
            MaxFileSizeMb = RequireInt("MaxFileSizeMb", 1, 500);
            IccProfilePath = ResolveRelativePath(Require("IccProfilePath"));
            ServiceVersion = Get("ServiceVersion", "1.0.0");
            ServiceName = Get("ServiceName", "PdfAForge");
        }

        /// <summary>
        /// Validates all settings and throws if something is wrong.
        /// Called at application startup.
        /// </summary>
        public void Validate()
        {
            if (!Directory.Exists(LogPath))
            {
                try { Directory.CreateDirectory(LogPath); }
                catch { throw new InvalidOperationException($"Cannot create log directory: {LogPath}"); }
            }

            if (!File.Exists(IccProfilePath))
                throw new InvalidOperationException(
                    $"ICC profile not found: {IccProfilePath}");
        }

        // --- Helpers ---

        private static string Require(string key)
        {
            var val = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(val))
                throw new ConfigurationErrorsException(
                    $"Missing required appSetting: '{key}'");
            return val.Trim();
        }

        private static int RequireInt(string key, int min, int max)
        {
            var raw = Require(key);
            if (!int.TryParse(raw, out int val) || val < min || val > max)
                throw new ConfigurationErrorsException(
                    $"appSetting '{key}' must be an integer between {min} and {max}. Got: '{raw}'");
            return val;
        }

        private static string Get(string key, string defaultValue)
        {
            var val = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(val) ? defaultValue : val.Trim();
        }

        private static string ResolveRelativePath(string path)
        {
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }
    }
}