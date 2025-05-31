using System;
using System.Windows.Forms;
using EmbedIO;
using EmbedIO.WebApi;

namespace OWhisper.NET
{
    internal static class Program
    {
        private static WhisperService _whisperService;
        private static WebServer _webServer;

        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                // 初始化核心服务
                _whisperService = WhisperService.Instance;
                
                // 启动WebAPI服务器
                StartWebApiServer();
                
                // 启动托盘应用
                Application.Run(new TrayApp(_whisperService));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void StartWebApiServer()
        {
            _webServer = new WebServer(o => o
                .WithUrlPrefix("http://localhost:9000")
                .WithMode(HttpListenerMode.EmbedIO))
                .WithWebApi("/", m => m.WithController<WhisperController>())
                .WithWebApi("/api", m => m.WithController<WhisperController>());
            
            _webServer.Start();
        }

        public static void ExitApplication()
        {
            _webServer?.Dispose();
            Application.Exit();
        }
    }
}