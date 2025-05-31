using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebApi;
using System.Management;
using Serilog;
using System.Text; // 添加StringBuilder支持
using Velopack;
using Velopack.Sources;

namespace OWhisper.NET {
    internal static class Program {
        public static WhisperService _whisperService;
        public static WebServer _webServer;
        public static UpdateManager _updateManager;
        public static UpdateInfo _updateInfo; // 改为public访问

        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        [STAThread]
        static async Task Main() {
            // Velopack自动更新初始化
            VelopackApp.Build().Run();

            // 配置Serilog日志
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: "Logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Log.Information("应用程序启动");

                // 初始化Velopack更新管理器
                _updateManager = new UpdateManager(new GithubSource("https://github.com/ModerRAS/OWhisper.NET", null, false));

                // 初始化核心服务
                _whisperService = WhisperService.Instance;

                // 启动WebAPI服务器
                StartWebApiServer();

                // 启动托盘应用(使用ApplicationContext)，传入_updateManager
                Application.Run(new TrayApp(_whisperService, _updateManager));
            } catch (Exception ex) {
                Log.Fatal(ex, "应用程序启动失败");
                MessageBox.Show($"启动失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
            public static string GetVersionInfo() {
            var sb = new StringBuilder();
            sb.AppendLine($"Velopack 版本: {VelopackRuntimeInfo.VelopackNugetVersion}");
            sb.AppendLine($"当前应用版本: {( _updateManager.IsInstalled ? _updateManager.CurrentVersion.ToFullString() : "(未安装)" )}");

            if (_updateInfo != null) {
                sb.AppendLine($"可用更新: {_updateInfo.TargetFullRelease.Version}");
            }

            if (_updateManager.UpdatePendingRestart != null) {
                sb.AppendLine("更新已就绪，等待重启安装");
            }

            return sb.ToString();
        }

        private static void StartWebApiServer() {
            _webServer = new WebServer(o => o
                .WithUrlPrefix("http://localhost:9000")
                .WithMode(HttpListenerMode.EmbedIO))
                .WithWebApi("/", m => m.WithController<WhisperController>())
                .WithWebApi("/api", m => m.WithController<WhisperController>());

            _webServer.Start();
        }

        public static async Task CheckForUpdatesAsync() {
            try {
                if (_updateManager == null) return;
                
                Log.Information("开始检查更新...");
                _updateInfo = await _updateManager.CheckForUpdatesAsync(); // 保存更新信息
                if (_updateInfo == null) {
                    Log.Information("没有可用更新");
                    MessageBox.Show("当前已是最新版本", "检查更新",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Log.Information("发现新版本: {Version}", _updateInfo.TargetFullRelease.Version);
                var result = MessageBox.Show($"发现新版本 {_updateInfo.TargetFullRelease.Version}，是否立即更新？",
                    "发现更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes) {
                    Log.Information("开始下载更新...");
                    await _updateManager.DownloadUpdatesAsync(_updateInfo);
                    Log.Information("更新下载完成，准备安装...");
                    _updateManager.ApplyUpdatesAndRestart(_updateInfo);
            }
        } catch (Exception ex) {
            Log.Error(ex, "检查更新时出错");
            MessageBox.Show($"检查更新失败: {ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // 添加下载更新方法
    public static async Task DownloadUpdatesAsync() {
        try {
            if (_updateInfo == null) return;
            
            Log.Information("开始下载更新...");
            await _updateManager.DownloadUpdatesAsync(_updateInfo);
            Log.Information("更新下载完成，准备安装...");
            _updateManager.ApplyUpdatesAndRestart(_updateInfo);
        } catch (Exception ex) {
            Log.Error(ex, "下载更新时出错");
            MessageBox.Show($"下载更新失败: {ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

        public static void ExitApplication() {
            try {
                Log.Information("开始关闭应用程序...");

                // 释放Web服务器资源
                _webServer?.Dispose();
                Log.Information("Web服务器已释放");

                // 释放Whisper服务资源
                _whisperService?.Dispose();
                Log.Information("Whisper服务已释放");
                
                // 移除更新管理器释放（Velopack自动管理）
                // _updateManager?.Dispose();
                Log.Information("跳过更新管理器释放（Velopack自动管理）");

                // 优雅关闭所有子进程
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var children = GetChildProcesses(currentProcess.Id);
                Log.Information("找到 {ChildProcessCount} 个子进程需要关闭", children.Count);

                foreach (var child in children) {
                    try {
                        Log.Information("尝试关闭子进程: {ProcessName} (PID: {ProcessId})", child.ProcessName, child.Id);

                        // 先尝试优雅关闭
                        if (!child.CloseMainWindow()) {
                            Log.Warning("优雅关闭失败，尝试终止进程");
                            child.Kill();
                        }

                        // 等待进程退出
                        if (!child.WaitForExit(5000)) {
                            Log.Warning("子进程未在5秒内退出");
                        } else {
                            Log.Information("子进程已成功退出");
                        }
                    } catch (Exception ex) {
                        Log.Error(ex, "关闭子进程时出错");
                    } finally {
                        child.Dispose();
                    }
                }

                Log.Information("正在退出应用程序...");
                Application.Exit();
                Log.CloseAndFlush();
            } catch (Exception ex) {
                Log.Error(ex, "退出时发生错误");
            }
        }

        private static List<System.Diagnostics.Process> GetChildProcesses(int parentId) {
            var children = new List<System.Diagnostics.Process>();

            try {
                // 使用更高效的WMI查询方式
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentId}");

                foreach (var obj in searcher.Get()) {
                    var childId = Convert.ToInt32(obj["ProcessId"]);
                    try {
                        var childProcess = System.Diagnostics.Process.GetProcessById(childId);
                        if (childProcess != null && !childProcess.HasExited) {
                            children.Add(childProcess);
                        }
                    } catch (ArgumentException) { /* 进程可能已退出 */ }
                }
            } catch (Exception ex) {
                Log.Error(ex, "查询子进程时出错");
            }

            return children;
        }
    }
}