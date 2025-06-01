using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using NUnit.Framework;

namespace IntegrationTests {
    [TestFixture]
    public class ApplicationTestBase : IDisposable {
        protected HttpClient Client;
        protected string TestResourcesDir;
        protected string BaseUrl => TestBaseUrl;
        private Process _appProcess;
        private readonly List<int> _processIds = new List<int>();
        
        // 使用环境变量支持，但为测试提供明确的端口
        private static readonly string TestHost = Environment.GetEnvironmentVariable("OWHISPER_TEST_HOST") ?? "localhost";
        private static readonly int TestPort = int.TryParse(Environment.GetEnvironmentVariable("OWHISPER_TEST_PORT"), out int port) ? port : 11899;
        private static readonly string TestBaseUrl = $"http://{TestHost}:{TestPort}";
        
        [OneTimeSetUp]
        public void SetUp() {
            Console.WriteLine("初始化测试环境...");

            // 配置测试资源路径
            TestResourcesDir = Path.Combine(
                Path.GetDirectoryName(typeof(ApplicationTestBase).Assembly.Location),
                "TestResources");

            Console.WriteLine($"测试资源目录: {TestResourcesDir}");
            Console.WriteLine($"测试URL: {TestBaseUrl}");

            if (!Directory.Exists(TestResourcesDir)) {
                Console.WriteLine("测试资源目录不存在，尝试创建...");
                Directory.CreateDirectory(TestResourcesDir);

                if (!Directory.Exists(TestResourcesDir)) {
                    throw new DirectoryNotFoundException($"Test resources directory not found: {TestResourcesDir}");
                }
                Console.WriteLine("测试资源目录创建成功");
            }

            // 设置测试环境变量
            Environment.SetEnvironmentVariable("OWHISPER_HOST", TestHost);
            Environment.SetEnvironmentVariable("OWHISPER_PORT", TestPort.ToString());

            // 启动应用程序进程
            Console.WriteLine("启动OWhisper.NET进程...");
            _appProcess = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "OWhisper.NET.exe",
                    Arguments = "--debug", // 使用调试模式，不再使用--urls参数
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            // 在.NET Framework中设置环境变量
            _appProcess.StartInfo.EnvironmentVariables["OWHISPER_HOST"] = TestHost;
            _appProcess.StartInfo.EnvironmentVariables["OWHISPER_PORT"] = TestPort.ToString();

            try {
                if (!_appProcess.Start()) {
                    throw new Exception("进程启动失败");
                }
                Console.WriteLine($"主进程已启动，PID: {_appProcess.Id}");
                _processIds.Add(_appProcess.Id);

                // 捕获初始进程树
                Console.WriteLine("扫描初始进程树...");
                var initialProcesses = Process.GetProcessesByName("OWhisper.NET");
                foreach (var p in initialProcesses) {
                    if (!_processIds.Contains(p.Id)) {
                        Console.WriteLine($"发现子进程，PID: {p.Id}");
                        _processIds.Add(p.Id);
                        p.Dispose();
                    }
                }

                // 等待应用启动 - 使用正确的状态检查端点
                Console.WriteLine("等待应用初始化...");
                for (int i = 0; i < 15; i++) // 增加到15秒超时，因为第一次启动需要下载模型
                {
                    try {
                        using (var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) }) {
                            // 使用正确的状态检查端点
                            var response = testClient.GetAsync($"{TestBaseUrl}/api/status").Result;
                            if (response.IsSuccessStatusCode) {
                                Console.WriteLine("应用启动成功");
                                break;
                            }
                        }
                    } catch {
                        if (i == 14) throw new TimeoutException("应用启动超时");
                    }
                    Thread.Sleep(1000);
                }

                Client = new HttpClient {
                    BaseAddress = new Uri(TestBaseUrl),
                    Timeout = TimeSpan.FromSeconds(30)
                };
                Console.WriteLine("HTTP客户端已初始化");
            } catch (Exception ex) {
                Console.WriteLine($"初始化失败: {ex}");
                KillAllProcesses();
                throw;
            }
        }

        public void Dispose() {
            Client?.Dispose();

            try {
                KillAllProcesses();
                _appProcess?.Dispose();
            } catch (Exception ex) {
                Console.WriteLine($"清理过程中发生错误: {ex.Message}");
            }
        }

        private static void KillProcessTree(int parentId) {
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries) {
                try {
                    using (var process = Process.GetProcessById(parentId)) {
                        if (!process.HasExited) {
                            Console.WriteLine($"终止进程树 {parentId} (尝试 {retryCount + 1}/{maxRetries})");
                            process.Kill();

                            if (!process.WaitForExit(2000)) // 缩短等待时间，增加重试
                            {
                                Console.WriteLine($"进程 {parentId} 未及时终止，将重试");
                                retryCount++;
                                continue;
                            }
                        }
                    }
                    Console.WriteLine($"成功终止进程树 {parentId}");
                    return;
                } catch (Exception ex) {
                    Console.WriteLine($"终止进程树 {parentId} 失败 (尝试 {retryCount + 1}/{maxRetries}): {ex.Message}");
                    retryCount++;
                    if (retryCount >= maxRetries) {
                        Console.WriteLine($"无法终止进程树 {parentId}，放弃尝试");
                        return;
                    }
                    Thread.Sleep(500); // 重试前等待
                }
            }
        }

        private static void KillProcessAndChildren(int pid) {
            try {
                var process = Process.GetProcessById(pid);
                if (!process.HasExited) {
                    // 先尝试优雅关闭
                    if (process.CloseMainWindow()) {
                        if (!process.WaitForExit(3000)) {
                            KillProcessTree(process.Id);
                        }
                    } else {
                        KillProcessTree(process.Id);
                    }
                }
                process.Dispose();
            } catch (Exception ex) {
                Console.WriteLine($"终止进程 {pid} 失败: {ex.Message}");
            }
        }

        private void KillAllProcesses() {
            // 终止所有已知进程
            foreach (var pid in _processIds.ToList()) {
                KillProcessAndChildren(pid);
            }

            // 终止所有同名进程
            var processes = Process.GetProcessesByName("OWhisper.NET");
            foreach (var process in processes) {
                try {
                    if (!_processIds.Contains(process.Id) && !process.HasExited) {
                        KillProcessAndChildren(process.Id);
                    }
                    process.Dispose();
                } catch (Exception ex) {
                    Console.WriteLine($"终止遗漏进程 {process.Id} 失败: {ex.Message}");
                }
            }

            // 强制GC回收
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 最终验证
            var remaining = Process.GetProcessesByName("OWhisper.NET");
            if (remaining.Length > 0) {
                Console.WriteLine($"警告: 仍有 {remaining.Length} 个进程未终止");
                foreach (var p in remaining) {
                    p.Dispose();
                }
            }
        }
    }
}