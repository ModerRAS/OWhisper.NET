using System;
using System.Windows.Forms;
using System.Collections.Generic;
using EmbedIO;
using EmbedIO.WebApi;
using System.Management;

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
            try
            {
                Console.WriteLine("开始关闭应用程序...");
                
                // 释放Web服务器资源
                _webServer?.Dispose();
                Console.WriteLine("Web服务器已释放");
                
                // 释放Whisper服务资源
                _whisperService?.Dispose();
                Console.WriteLine("Whisper服务已释放");
                
                // 终止所有子进程
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                foreach (var child in GetChildProcesses(currentProcess.Id))
                {
                    try
                    {
                        Console.WriteLine($"终止子进程: {child.ProcessName} (PID: {child.Id})");
                        child.Kill();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"终止子进程失败: {ex.Message}");
                    }
                }
                
                Application.Exit();
                Console.WriteLine("应用程序已退出");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"退出时发生错误: {ex}");
            }
        }
        
        private static List<System.Diagnostics.Process> GetChildProcesses(int parentId)
        {
            var children = new List<System.Diagnostics.Process>();
            var processes = System.Diagnostics.Process.GetProcesses();
            
            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == parentId) continue;
                    
                    using (var query = new System.Management.ManagementObjectSearcher(
                        $"SELECT * FROM Win32_Process WHERE ParentProcessId={parentId}"))
                    {
                        foreach (var mo in query.Get())
                        {
                            if ((uint)mo["ProcessId"] == process.Id)
                            {
                                children.Add(process);
                                break;
                            }
                        }
                    }
                }
                catch { /* 忽略访问错误 */ }
            }
            
            return children;
        }
    }
}