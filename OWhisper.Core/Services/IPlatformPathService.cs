namespace OWhisper.Core.Services
{
    public interface IPlatformPathService
    {
        string GetApplicationDataPath();
        string GetModelsPath();
        string GetLogsPath();
        string GetTempPath();
        void EnsureDirectoriesExist();
    }
} 