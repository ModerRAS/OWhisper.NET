using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Management;
using Serilog;
using System.Text; // 添加StringBuilder支持
using Velopack;
using Velopack.Sources;
using OWhisper.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Extensions.Logging;
using OWhisper.NET.Services; // 添加UrlAclHelper的命名空间

namespace OWhisper.NET {
    internal static class Program {
        public static IWebApiService _webApiService;
        public static UpdateManager _updateManager;
        public static UpdateInfo _updateInfo; // 改为public访问
        public static IServiceProvider _serviceProvider;
        
        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        [STAThread]
        static void Main(string[] args) {
            // 配置Serilog日志
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "Logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Log.Information("应用程序启动");

                // 配置依赖注入
                ConfigureServices();

                // 初始化Velopack更新管理器（非调试模式）
                if (args.Length == 0 || args[0] != "--debug") {
                    VelopackApp.Build().Run();
                    _updateManager = new UpdateManager("https://velopack.miaostay.com/");
                }

                // 启动WebAPI服务
                StartWebApiServiceAsync().Wait();

                // 检查是否处于调试模式
                if (args.Length > 0 && args[0] == "--debug") {
                    // 调试模式：显示主窗体
                    var mainForm = new MainForm();
                    mainForm.ShowForDebug(); // 调用自定义调试显示方法
                    Application.Run(mainForm);
                    return;
                }

                // 正常模式：启动托盘应用
                Application.Run(new TrayApp(_webApiService, _updateManager));
            } catch (Exception ex) {
                Log.Fatal(ex, "应用程序启动失败");
                MessageBox.Show($"启动失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 配置依赖注入服务
        /// </summary>
        private static void ConfigureServices()
        {
            var services = new ServiceCollection();
            
            // 添加日志服务
            services.AddLogging(builder => builder.AddSerilog());
            
            // 添加核心服务
            services.AddSingleton<IPlatformPathService, PlatformPathService>();
            services.AddSingleton<IWebApiService, WebApiService>();
            
            _serviceProvider = services.BuildServiceProvider();
            _webApiService = _serviceProvider.GetRequiredService<IWebApiService>();
        }

        /// <summary>
        /// 启动WebAPI服务
        /// </summary>
        private static async Task StartWebApiServiceAsync()
        {
            try
            {
                Log.Information("正在启动WebAPI服务: {Url}", _webApiService.ListenUrl);
                
                // 检查URL ACL权限
                await CheckAndSetupUrlAclAsync();
                
                await _webApiService.StartAsync();
                Log.Information("WebAPI服务启动成功");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WebAPI服务启动失败");
                
                // 如果启动失败，提供URL ACL相关的建议
                var suggestion = GetUrlAclSuggestion();
                throw new Exception($"WebAPI服务启动失败: {ex.Message}{Environment.NewLine}{suggestion}", ex);
            }
        }

        /// <summary>
        /// 检查并设置URL ACL权限
        /// </summary>
        private static async Task CheckAndSetupUrlAclAsync()
        {
            try
            {
                var listenUrl = _webApiService.ListenUrl;
                var formattedUrl = UrlAclHelper.FormatUrlForAcl(listenUrl);
                
                Log.Information("检查URL ACL权限: {Url}", formattedUrl);
                
                // 检查URL ACL是否已设置
                var aclExists = await UrlAclHelper.CheckUrlAclAsync(formattedUrl);
                
                if (aclExists)
                {
                    Log.Information("URL ACL权限已存在");
                    return;
                }
                
                Log.Warning("URL ACL权限不存在，尝试自动设置");
                
                // 检查是否有管理员权限
                if (!UrlAclHelper.IsRunningAsAdministrator())
                {
                    Log.Warning("当前没有管理员权限，无法自动设置URL ACL");
                    //ShowUrlAclDialog(formattedUrl);
                    return;
                }
                
                // 尝试自动设置URL ACL
                var currentUser = UrlAclHelper.GetCurrentUserName();
                var (success, message) = await UrlAclHelper.AddUrlAclAsync(formattedUrl, currentUser);
                
                if (success)
                {
                    Log.Information("URL ACL权限设置成功");
                }
                else
                {
                    Log.Error("URL ACL权限设置失败: {Message}", message);
                    ShowUrlAclDialog(formattedUrl);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检查URL ACL权限时出错");
            }
        }

        /// <summary>
        /// 显示URL ACL设置对话框
        /// </summary>
        private static void ShowUrlAclDialog(string url)
        {
            try
            {
                var isAdmin = UrlAclHelper.IsRunningAsAdministrator();
                var currentUser = UrlAclHelper.GetCurrentUserName();
                
                var message = new StringBuilder();
                message.AppendLine("检测到可能的URL ACL权限问题。");
                message.AppendLine();
                message.AppendLine("为了让Web服务正常运行，您可能需要设置URL ACL权限。");
                message.AppendLine();
                message.AppendLine("推荐的解决方案：");
                message.AppendLine("1. 以管理员权限运行此程序");
                message.AppendLine("2. 或者手动执行以下命令：");
                message.AppendLine();
                message.AppendLine($"netsh http add urlacl url={url} user={currentUser}");
                message.AppendLine();
                message.AppendLine("或者使用Everyone用户：");
                message.AppendLine($"netsh http add urlacl url={url} user=Everyone");
                message.AppendLine();
                
                if (isAdmin)
                {
                    message.AppendLine("您当前具有管理员权限，是否尝试自动设置？");
                    
                    var result = MessageBox.Show(message.ToString(), "URL ACL权限设置", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        Task.Run(async () =>
                        {
                            var (success, msg) = await UrlAclHelper.AddUrlAclAsync(url, currentUser);
                            if (success)
                            {
                                MessageBox.Show("URL ACL权限设置成功！", "成功", 
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show($"URL ACL权限设置失败: {msg}", "失败", 
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        });
                    }
                }
                else
                {
                    message.AppendLine("您当前没有管理员权限，请以管理员身份运行程序或手动执行上述命令。");
                    
                    MessageBox.Show(message.ToString(), "URL ACL权限设置", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "显示URL ACL对话框时出错");
            }
        }

        /// <summary>
        /// 获取URL ACL相关建议
        /// </summary>
        private static string GetUrlAclSuggestion()
        {
            try
            {
                var listenUrl = _webApiService?.ListenUrl ?? "http://+:11899/";
                var formattedUrl = UrlAclHelper.FormatUrlForAcl(listenUrl);
                var recommendedCommand = UrlAclHelper.GetRecommendedCommand(formattedUrl);
                
                var suggestion = new StringBuilder();
                suggestion.AppendLine();
                suggestion.AppendLine("可能的解决方案：");
                suggestion.AppendLine("1. 以管理员权限运行此程序");
                suggestion.AppendLine("2. 或者手动执行以下命令设置URL ACL权限：");
                suggestion.AppendLine($"   {recommendedCommand}");
                
                return suggestion.ToString();
            }
            catch
            {
                return Environment.NewLine + "建议以管理员权限运行此程序。";
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

                // 停止WebAPI服务
                _webApiService?.StopAsync().Wait(5000);
                Log.Information("WebAPI服务已停止");

                // 释放服务容器
                if (_serviceProvider is IDisposable disposableServiceProvider)
                {
                    disposableServiceProvider.Dispose();
                }
                Log.Information("服务容器已释放");
                
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
