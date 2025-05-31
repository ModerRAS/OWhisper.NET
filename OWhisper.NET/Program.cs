using System;
using System.Windows.Forms;
using System.Collections.Generic;
using EmbedIO;
using EmbedIO.WebApi;
using System.Management;
using Serilog;

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
            // 配置Serilog日志
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: "Logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                Log.Information("应用程序启动");
                // 初始化核心服务
                _whisperService = WhisperService.Instance;
                
                // 启动WebAPI服务器
                StartWebApiServer();
                
                // 启动托盘应用
                Application.Run(new TrayApp(_whisperService));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "应用程序启动失败");
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
                Log.Information("开始关闭应用程序...");
                
                // 释放Web服务器资源
                _webServer?.Dispose();
                Log.Information("Web服务器已释放");
                
                // 释放Whisper服务资源
                _whisperService?.Dispose();
                Log.Information("Whisper服务已释放");
                
                // 优雅关闭所有子进程
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var children = GetChildProcesses(currentProcess.Id);
                Log.Information("找到 {ChildProcessCount} 个子进程需要关闭", children.Count);
                
                foreach (var child in children)
                {
                    try
                    {
                        Log.Information("尝试关闭子进程: {ProcessName} (PID: {ProcessId})", child.ProcessName, child.Id);
                        
                        // 先尝试优雅关闭
                        if (!child.CloseMainWindow())
                        {
                            Log.Warning("优雅关闭失败，尝试终止进程");
                            child.Kill();
                        }
                        
                        // 等待进程退出
                        if (!child.WaitForExit(5000))
                        {
                            Log.Warning("子进程未在5秒内退出");
                        }
                        else
                        {
                            Log.Information("子进程已成功退出");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "关闭子进程时出错");
                    }
                    finally
                    {
                        child.Dispose();
                    }
                }
                
                Log.Information("正在退出应用程序...");
                Application.Exit();
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "退出时发生错误");
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
                Log.Error(ex, "查询子进程时出错");
            }
            
            return children;
        }
    }
}