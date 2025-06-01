using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;
using Serilog;

namespace OWhisper.NET.Services
{
    /// <summary>
    /// URL ACL 权限管理助手类
    /// </summary>
    public static class UrlAclHelper
    {
        /// <summary>
        /// 检查是否具有管理员权限
        /// </summary>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检查管理员权限时出错");
                return false;
            }
        }

        /// <summary>
        /// 检查URL ACL是否已设置
        /// </summary>
        public static async Task<bool> CheckUrlAclAsync(string url)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "http show urlacl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    Log.Error("无法启动netsh进程");
                    return false;
                }

                var output = await Task.Run(() => {
                    var result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return result;
                });

                // 检查输出中是否包含指定的URL
                return output.Contains(url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检查URL ACL时出错: {Url}", url);
                return false;
            }
        }

        /// <summary>
        /// 添加URL ACL权限
        /// </summary>
        public static async Task<(bool Success, string Message)> AddUrlAclAsync(string url, string user = "Everyone")
        {
            try
            {
                if (!IsRunningAsAdministrator())
                {
                    return (false, "需要管理员权限才能设置URL ACL");
                }

                var arguments = $"http add urlacl url={url} user={user}";
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Log.Information("执行netsh命令: {Command}", $"netsh {arguments}");

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    return (false, "无法启动netsh进程");
                }

                var (output, error, exitCode) = await Task.Run(() => {
                    var stdOut = process.StandardOutput.ReadToEnd();
                    var stdErr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return (stdOut, stdErr, process.ExitCode);
                });

                if (exitCode == 0)
                {
                    Log.Information("URL ACL设置成功: {Url}", url);
                    return (true, "URL ACL权限设置成功");
                }
                else
                {
                    var message = $"设置URL ACL失败。退出代码: {exitCode}";
                    if (!string.IsNullOrEmpty(error))
                    {
                        message += $", 错误信息: {error}";
                    }
                    if (!string.IsNullOrEmpty(output))
                    {
                        message += $", 输出信息: {output}";
                    }
                    
                    Log.Error("URL ACL设置失败: {Message}", message);
                    return (false, message);
                }
            }
            catch (Exception ex)
            {
                var message = $"设置URL ACL时出现异常: {ex.Message}";
                Log.Error(ex, "设置URL ACL时出现异常: {Url}", url);
                return (false, message);
            }
        }

        /// <summary>
        /// 删除URL ACL权限
        /// </summary>
        public static async Task<(bool Success, string Message)> RemoveUrlAclAsync(string url)
        {
            try
            {
                if (!IsRunningAsAdministrator())
                {
                    return (false, "需要管理员权限才能删除URL ACL");
                }

                var arguments = $"http delete urlacl url={url}";
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Log.Information("执行netsh命令: {Command}", $"netsh {arguments}");

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    return (false, "无法启动netsh进程");
                }

                var (output, error, exitCode) = await Task.Run(() => {
                    var stdOut = process.StandardOutput.ReadToEnd();
                    var stdErr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return (stdOut, stdErr, process.ExitCode);
                });

                if (exitCode == 0)
                {
                    Log.Information("URL ACL删除成功: {Url}", url);
                    return (true, "URL ACL权限删除成功");
                }
                else
                {
                    var message = $"删除URL ACL失败。退出代码: {exitCode}";
                    if (!string.IsNullOrEmpty(error))
                    {
                        message += $", 错误信息: {error}";
                    }
                    if (!string.IsNullOrEmpty(output))
                    {
                        message += $", 输出信息: {output}";
                    }
                    
                    Log.Error("URL ACL删除失败: {Message}", message);
                    return (false, message);
                }
            }
            catch (Exception ex)
            {
                var message = $"删除URL ACL时出现异常: {ex.Message}";
                Log.Error(ex, "删除URL ACL时出现异常: {Url}", url);
                return (false, message);
            }
        }

        /// <summary>
        /// 获取当前用户名
        /// </summary>
        public static string GetCurrentUserName()
        {
            try
            {
                return Environment.UserName;
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 格式化URL用于ACL设置
        /// </summary>
        public static string FormatUrlForAcl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            // 确保URL以斜杠结尾
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return url;
        }

        /// <summary>
        /// 获取推荐的URL ACL设置命令
        /// </summary>
        public static string GetRecommendedCommand(string url)
        {
            var formattedUrl = FormatUrlForAcl(url);
            var currentUser = GetCurrentUserName();
            
            return $"netsh http add urlacl url={formattedUrl} user={currentUser}";
        }
    }
} 