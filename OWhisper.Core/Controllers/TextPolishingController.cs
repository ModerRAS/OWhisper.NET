using System;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using Newtonsoft.Json;
using OWhisper.Core.Models;
using OWhisper.Core.Services;
using Serilog;

namespace OWhisper.Core.Controllers
{
    /// <summary>
    /// 文本润色控制器 - EmbedIO实现
    /// </summary>
    public class TextPolishingController : WebApiController
    {
        private readonly ISlidingWindowPolishingService _polishingService;

        public TextPolishingController()
        {
            // 使用默认实现
            _polishingService = new SlidingWindowPolishingService();
        }

        /// <summary>
        /// 润色 SRT 文本
        /// </summary>
        [Route(HttpVerbs.Post, "/api/textpolishing/polish")]
        public async Task<ApiResponse<TextPolishingHttpResponse>> PolishSrtAsync()
        {
            try
            {
                // 从请求体读取JSON
                var requestBody = await HttpContext.GetRequestBodyAsStringAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    HttpContext.Response.StatusCode = 400;
                    return ApiResponse<TextPolishingHttpResponse>.CreateError("INVALID_REQUEST", "请求体不能为空");
                }

                var request = JsonConvert.DeserializeObject<TextPolishingHttpRequest>(requestBody);
                if (request == null)
                {
                    HttpContext.Response.StatusCode = 400;
                    return ApiResponse<TextPolishingHttpResponse>.CreateError("INVALID_JSON", "无效的JSON格式");
                }

                Log.Information("接收到文本润色请求，提供商: {Provider}, 模型: {Model}", 
                    request.ApiProvider, request.Model);

                var response = await _polishingService.PolishSrtAsync(request, HttpContext.CancellationToken);

                if (response.Success)
                {
                    Log.Information("文本润色成功完成，耗时: {Duration}ms", response.ProcessingTimeMs);
                }
                else
                {
                    Log.Warning("文本润色失败: {Error}", response.ErrorMessage);
                }

                return ApiResponse<TextPolishingHttpResponse>.Success(response);
            }
            catch (OperationCanceledException)
            {
                Log.Information("文本润色请求被取消");
                HttpContext.Response.StatusCode = 499;
                return ApiResponse<TextPolishingHttpResponse>.CreateError("REQUEST_CANCELLED", "请求被取消");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "文本润色控制器异常");
                HttpContext.Response.StatusCode = 500;
                return ApiResponse<TextPolishingHttpResponse>.CreateError("INTERNAL_ERROR", $"服务器内部错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 API 连接
        /// </summary>
        [Route(HttpVerbs.Post, "/api/textpolishing/test-connection")]
        public async Task<ApiResponse<ApiConnectionTestResponse>> TestApiConnectionAsync()
        {
            try
            {
                var requestBody = await HttpContext.GetRequestBodyAsStringAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    HttpContext.Response.StatusCode = 400;
                    return ApiResponse<ApiConnectionTestResponse>.CreateError("INVALID_REQUEST", "请求体不能为空");
                }

                var request = JsonConvert.DeserializeObject<ApiConnectionTestRequest>(requestBody);
                if (request == null)
                {
                    HttpContext.Response.StatusCode = 400;
                    return ApiResponse<ApiConnectionTestResponse>.CreateError("INVALID_JSON", "无效的JSON格式");
                }

                Log.Information("测试 API 连接: {Provider}, 模型: {Model}", 
                    request.ApiProvider, request.Model);

                var response = await _polishingService.TestApiConnectionAsync(request, HttpContext.CancellationToken);

                if (response.Success)
                {
                    Log.Information("API 连接测试成功");
                }
                else
                {
                    Log.Warning("API 连接测试失败: {Error}", response.Message);
                }

                return ApiResponse<ApiConnectionTestResponse>.Success(response);
            }
            catch (OperationCanceledException)
            {
                Log.Information("API 连接测试被取消");
                HttpContext.Response.StatusCode = 499;
                return ApiResponse<ApiConnectionTestResponse>.CreateError("REQUEST_CANCELLED", "请求被取消");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "API 连接测试异常");
                HttpContext.Response.StatusCode = 500;
                return ApiResponse<ApiConnectionTestResponse>.CreateError("INTERNAL_ERROR", $"服务器内部错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取支持的 API 提供商列表
        /// </summary>
        [Route(HttpVerbs.Get, "/api/textpolishing/providers")]
        public ApiResponse<string[]> GetSupportedProviders()
        {
            try
            {
                var providers = _polishingService.GetSupportedProviders();
                Log.Debug("返回支持的提供商列表，数量: {Count}", providers.Length);
                return ApiResponse<string[]>.Success(providers);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取支持的提供商列表异常");
                HttpContext.Response.StatusCode = 500;
                return ApiResponse<string[]>.CreateError("INTERNAL_ERROR", $"服务器内部错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定提供商支持的模型列表
        /// </summary>
        [Route(HttpVerbs.Get, "/api/textpolishing/providers/{provider}/models")]
        public ApiResponse<string[]> GetSupportedModels(string provider)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(provider))
                {
                    HttpContext.Response.StatusCode = 400;
                    return ApiResponse<string[]>.CreateError("INVALID_PARAMETER", "提供商参数不能为空");
                }

                var models = _polishingService.GetSupportedModels(provider);
                Log.Debug("返回提供商 {Provider} 支持的模型列表，数量: {Count}", provider, models.Length);
                return ApiResponse<string[]>.Success(models);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取支持的模型列表异常");
                HttpContext.Response.StatusCode = 500;
                return ApiResponse<string[]>.CreateError("INTERNAL_ERROR", $"服务器内部错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 健康检查
        /// </summary>
        [Route(HttpVerbs.Get, "/api/textpolishing/health")]
        public ApiResponse<object> GetHealth()
        {
            return ApiResponse<object>.Success(new 
            { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                service = "TextPolishingService"
            });
        }
    }
} 