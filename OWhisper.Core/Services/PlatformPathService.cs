using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OWhisper.Core.Services
{
    public class PlatformPathService : IPlatformPathService
    {
        private readonly string _baseDataPath;
        private readonly string _modelsPath;
        private readonly string _logsPath;
        private readonly string _tempPath;

        public PlatformPathService()
        {
            _baseDataPath = GetPlatformSpecificDataPath();
            _modelsPath = Path.Combine(_baseDataPath, "models");
            _logsPath = Path.Combine(_baseDataPath, "logs");
            _tempPath = Path.Combine(_baseDataPath, "temp");
        }

        public string GetApplicationDataPath() => _baseDataPath;

        public string GetModelsPath() => _modelsPath;

        public string GetLogsPath() => _logsPath;

        public string GetTempPath() => _tempPath;

        public void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_baseDataPath);
            Directory.CreateDirectory(_modelsPath);
            Directory.CreateDirectory(_logsPath);
            Directory.CreateDirectory(_tempPath);
        }

        private static string GetPlatformSpecificDataPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OWhisper.NET");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "/var/lib/owhisper";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OWhisper.NET");
            }
            else
            {
                // 回退到用户主目录
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".owhisper");
            }
        }
    }
} 