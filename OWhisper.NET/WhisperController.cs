using EmbedIO;
using OWhisper.NET.Models;
using OWhisper.NET.Models;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System;
using System.Threading.Tasks;

namespace OWhisper.NET
{
    public class WhisperController : WebApiController
    {
        private readonly WhisperService _whisperService = WhisperService.Instance;

        public WhisperController()
        {
        }

        [Route(HttpVerbs.Get, "/")]
        public async Task<ApiResponse<object>> GetApiInfo()
        {
            return ApiResponse<object>.Success(new {
                endpoints = new[] {
                    "/api/status",
                    "/api/start",
                    "/api/stop",
                    "/api/transcribe"
                },
                service = "OWhisper.NET API"
            });
        }

        /// <summary>
        /// 获取服务状态
        /// </summary>
        /// <returns>
        /// 返回:
        /// {
        ///     status: "success",
        ///     data: {
        ///         serviceStatus: "Running|Stopped|Starting|Stopping"
        ///     }
        /// }
        /// </returns>
        [Route(HttpVerbs.Get, "/api/status")]
        public async Task<ApiResponse<object>> GetStatus()
        {
            try
            {
                var status = _whisperService.GetStatus();
                return ApiResponse<object>.Success(new
                {
                    serviceStatus = status.ToString()
                });
            }
            catch (AudioProcessingException ex)
            {
                HttpContext.Response.StatusCode = 400;
                return ApiResponse<object>.CreateError(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Console.WriteLine($"获取服务状态失败: {ex}");
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 启动语音识别服务
        /// </summary>
        /// <returns>
        /// 成功返回:
        /// {
        ///     status: "success"
        /// }
        /// 失败返回:
        /// {
        ///     status: "error",
        ///     error: "错误信息"
        /// }
        /// </returns>
        [Route(HttpVerbs.Post, "/api/start")]
        public async Task<ApiResponse<object>> StartService()
        {
            try
            {
                _whisperService.Start();
                return ApiResponse<object>.Success(null);
            }
            catch (AudioProcessingException ex)
            {
                HttpContext.Response.StatusCode = 400;
                return ApiResponse<object>.CreateError(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Console.WriteLine($"启动服务失败: {ex}");
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 停止语音识别服务
        /// </summary>
        /// <returns>
        /// 成功返回:
        /// {
        ///     status: "success"
        /// }
        /// 失败返回:
        /// {
        ///     status: "error",
        ///     error: "错误信息"
        /// }
        /// </returns>
        [Route(HttpVerbs.Post, "/api/stop")]
        public async Task<ApiResponse<object>> StopService()
        {
            try
            {
                _whisperService.Stop();
                return ApiResponse<object>.Success(null);
            }
            catch (AudioProcessingException ex)
            {
                HttpContext.Response.StatusCode = 400;
                return ApiResponse<object>.CreateError(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Console.WriteLine($"停止服务失败: {ex}");
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 接收音频文件并返回转写结果
        /// </summary>
        /// <returns>
        /// 成功返回:
        /// {
        ///     status: "success",
        ///     data: {
        ///         text: "转写文本内容",
        ///         processingTime: "处理耗时(秒)"
        ///     }
        /// }
        /// 失败返回:
        /// {
        ///     status: "error",
        ///     error: "错误信息"
        /// }
        /// </returns>
        [Route(HttpVerbs.Post, "/api/transcribe")]
        public async Task<ApiResponse<TranscriptionResult>> Transcribe()
        {
            try
            {
                var audioData = await HttpContext.GetRequestDataAsync<byte[]>();
                var result = await _whisperService.Transcribe(audioData);
                
                return ApiResponse<TranscriptionResult>.Success(result);
            }
            catch (AudioProcessingException ex)
            {
                HttpContext.Response.StatusCode = 400;
                return ApiResponse<TranscriptionResult>.CreateError(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Console.WriteLine($"转写失败: {ex}");
                return ApiResponse<TranscriptionResult>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }
    }
}