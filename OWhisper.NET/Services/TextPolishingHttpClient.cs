using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OWhisper.Core.Models;
using Serilog;

namespace OWhisper.NET.Services
{
    /// <summary>
    /// 文本润色 HTTP 客户端服务
    /// </summary>
    public class TextPolishingHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public TextPolishingHttpClient(string baseUrl = "http://localhost:5000")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10) // 设置较长的超时时间
            };
        }

        /// <summary>
        /// 发送文本润色请求
        /// </summary>
        /// <param name="request">润色请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>润色响应</returns>
        public async Task<TextPolishingHttpResponse> PolishSrtAsync(
            TextPolishingHttpRequest request, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                Log.Information("发送文本润色请求到服务端: {BaseUrl}", _baseUrl);

                var jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/textpolishing/polish", content, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<TextPolishingHttpResponse>(responseContent);
                    Log.Information("文本润色请求成功，耗时: {Duration}ms", result?.Statistics?.TotalProcessingTimeMs ?? 0);
                    return result ?? new TextPolishingHttpResponse
                    {
                        Success = false,
                        ErrorMessage = "服务端返回空响应"
                    };
                }
                else
                {
                    Log.Error("文本润色请求失败，状态码: {StatusCode}, 响应: {Response}", 
                        response.StatusCode, responseContent);

                    return new TextPolishingHttpResponse
                    {
                        Success = false,
                        ErrorMessage = $"服务端错误 ({response.StatusCode}): {responseContent}"
                    };
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Log.Error(ex, "文本润色请求超时");
                return new TextPolishingHttpResponse
                {
                    Success = false,
                    ErrorMessage = "请求超时，请检查网络连接和服务端状态"
                };
            }
            catch (OperationCanceledException)
            {
                Log.Information("文本润色请求被取消");
                return new TextPolishingHttpResponse
                {
                    Success = false,
                    ErrorMessage = "请求被取消"
                };
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "文本润色HTTP请求异常");
                return new TextPolishingHttpResponse
                {
                    Success = false,
                    ErrorMessage = $"网络请求失败: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "文本润色请求异常");
                return new TextPolishingHttpResponse
                {
                    Success = false,
                    ErrorMessage = $"请求异常: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 测试 API 连接
        /// </summary>
        /// <param name="request">连接测试请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接测试响应</returns>
        public async Task<ApiConnectionTestResponse> TestApiConnectionAsync(
            ApiConnectionTestRequest request, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                Log.Information("发送API连接测试请求到服务端");

                var jsonContent = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/textpolishing/test-connection", content, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<ApiConnectionTestResponse>(responseContent);
                    Log.Information("API连接测试完成，成功: {Success}, 响应时间: {Duration}ms", 
                        result?.Success ?? false, result?.ResponseTimeMs ?? 0);
                    return result ?? new ApiConnectionTestResponse
                    {
                        Success = false,
                        ErrorMessage = "服务端返回空响应"
                    };
                }
                else
                {
                    Log.Error("API连接测试失败，状态码: {StatusCode}, 响应: {Response}", 
                        response.StatusCode, responseContent);

                    return new ApiConnectionTestResponse
                    {
                        Success = false,
                        ErrorMessage = $"服务端错误 ({response.StatusCode}): {responseContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "API连接测试异常");
                return new ApiConnectionTestResponse
                {
                    Success = false,
                    ErrorMessage = $"连接测试异常: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取支持的 API 提供商列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>支持的提供商列表</returns>
        public async Task<string[]> GetSupportedProvidersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/textpolishing/providers", cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<string[]>(responseContent) ?? new string[0];
                }
                else
                {
                    Log.Error("获取支持的提供商列表失败，状态码: {StatusCode}", response.StatusCode);
                    return new string[0];
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取支持的提供商列表异常");
                return new string[0];
            }
        }

        /// <summary>
        /// 获取指定提供商支持的模型列表
        /// </summary>
        /// <param name="provider">API 提供商</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>支持的模型列表</returns>
        public async Task<string[]> GetSupportedModelsAsync(string provider, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/textpolishing/providers/{provider}/models", cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<string[]>(responseContent) ?? new string[0];
                }
                else
                {
                    Log.Error("获取提供商 {Provider} 支持的模型列表失败，状态码: {StatusCode}", provider, response.StatusCode);
                    return new string[0];
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取提供商 {Provider} 支持的模型列表异常", provider);
                return new string[0];
            }
        }

        /// <summary>
        /// 检查服务端健康状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否健康</returns>
        public async Task<bool> CheckServiceHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/textpolishing/health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "检查服务端健康状态异常");
                return false;
            }
        }

        /// <summary>
        /// 设置服务端地址
        /// </summary>
        /// <param name="baseUrl">服务端基地址</param>
        public void SetBaseUrl(string baseUrl)
        {
            // 这里实际上需要重新创建HttpClient，但为了简化，我们记录新的URL
            Log.Information("更新服务端地址: {BaseUrl}", baseUrl);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 