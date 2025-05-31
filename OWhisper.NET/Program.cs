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
                
                // 优雅关闭所有子进程
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var children = GetChildProcesses(currentProcess.Id);
                Console.WriteLine($"找到 {children.Count} 个子进程需要关闭");
                
                foreach (var child in children)
                {
                    try
                    {
                        Console.WriteLine($"尝试关闭子进程: {child.ProcessName} (PID: {child.Id})");
                        
                        // 先尝试优雅关闭
                        if (!child.CloseMainWindow())
                        {
                            Console.WriteLine("优雅关闭失败，尝试终止进程");
                            child.Kill();
                        }
                        
                        // 等待进程退出
                        if (!child.WaitForExit(5000))
                        {
                            Console.WriteLine("警告: 子进程未在5秒内退出");
                        }
                        else
                        {
                            Console.WriteLine("子进程已成功退出");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"关闭子进程时出错: {ex.Message}");
                    }
                    finally
                    {
                        child.Dispose();
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
            
            try
            {
                // 使用更高效的WMI查询方式
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentId}");
                
                foreach (var obj in searcher.Get())
                {
                    var childId = Convert.ToInt32(obj["ProcessId"]);
                    try
                    {
                        var childProcess = System.Diagnostics.Process.GetProcessById(childId);
                        if (childProcess != null && !childProcess.HasExited)
                        {
                            children.Add(childProcess);
                        }
                    }
                    catch (ArgumentException) { /* 进程可能已退出 */ }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询子进程时出错: {ex.Message}");
            }
            
            return children;
        }
    }
}