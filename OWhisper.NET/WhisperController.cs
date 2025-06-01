using EmbedIO;
using OWhisper.NET.Models;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System;
using System.Threading.Tasks;
using Serilog;
using HttpMultipartParser;
using System.IO;
using System.Linq;

namespace OWhisper.NET {
    public class WhisperController : WebApiController {
        private readonly WhisperService _whisperService = WhisperService.Instance;

        public WhisperController() {
        }

        [Route(HttpVerbs.Get, "/")]
        public async Task<ApiResponse<object>> GetApiInfo() {
            return ApiResponse<object>.Success(new {
                endpoints = new[] {
                    "/api/status",
                    "/api/start",
                    "/api/stop",
                    "/api/transcribe",
                    "/api/model/status"
                },
                service = "OWhisper.NET API"
            });
        }

        /// <summary>
        /// 获取模型文件状态
        /// </summary>
        /// <returns>
        /// 返回:
        /// {
        ///     status: "success",
        ///     data: {
        ///         exists: true|false,
        ///         valid: true|false,
        ///         size: 文件大小(字节),
        ///         path: "模型文件路径"
        ///     }
        /// }
        /// </returns>
        [Route(HttpVerbs.Get, "/api/model/status")]
        public async Task<ApiResponse<object>> GetModelStatus() {
            try {
                var whisperManager = new WhisperManager();
                var (exists, valid, size, path) = whisperManager.CheckModelStatus();

                return ApiResponse<object>.Success(new {
                    exists,
                    valid,
                    size,
                    path
                });
            } catch (Exception ex) {
                HttpContext.Response.StatusCode = 500;
                Console.WriteLine($"获取模型状态失败: {ex}");
                return ApiResponse<object>.CreateError("MODEL_STATUS_ERROR", "获取模型状态失败");
            }
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
        public async Task<ApiResponse<object>> GetStatus() {
            try {
                var status = _whisperService.GetStatus();
                return ApiResponse<object>.Success(new {
                    serviceStatus = status.ToString()
                });
            } catch (AudioProcessingException ex) {
                HttpContext.Response.StatusCode = 400;
                return ApiResponse<object>.CreateError(ex.ErrorCode, ex.Message);
            } catch (Exception ex) {
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
        public async Task<ApiResponse<object>> StartService() {
            try {
                _whisperService.Start();
                return ApiResponse<object>.Success(null);
            } catch (AudioProcessingException ex) {
                HttpContext.Response.StatusCode = 400;
                return ApiResponse<object>.CreateError(ex.ErrorCode, ex.Message);
            } catch (Exception ex) {
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
        public async Task<ApiResponse<object>> StopService() {
            try {
                _whisperService.Stop();
                return ApiResponse<object>.Success(null);
            } catch (AudioProcessingException ex) {
                HttpContext.Response.StatusCode = 400;
                return ApiResponse<object>.CreateError(ex.ErrorCode, ex.Message);
            } catch (Exception ex) {
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
        public async Task<ApiResponse<TranscriptionResult>> Transcribe() {
            try {
                // 解析multipart/form-data请求
                if (HttpContext.Request.InputStream == null) {
                    Log.Error("请求输入流为空");
                    throw new AudioProcessingException("INVALID_REQUEST", "无效的请求格式");
                }

                var parser = await MultipartFormDataParser.ParseAsync(HttpContext.Request.InputStream);
                var filePart = parser.Files.FirstOrDefault();

                if (filePart == null) {
                    Log.Error("请求中未包含文件");
                    throw new AudioProcessingException("NO_FILE_UPLOADED", "请上传音频文件");
                }

                // 验证文件格式
                var fileName = filePart.FileName?.ToLower() ?? string.Empty;
                if (!fileName.EndsWith(".mp3") && !fileName.EndsWith(".wav") && !fileName.EndsWith(".aac")) {
                    Log.Error("不支持的文件格式: {FileName}", fileName);
                    throw new AudioProcessingException("UNSUPPORTED_FILE_FORMAT", "仅支持.mp3、.wav和.aac格式");
                }

                // 读取文件数据
                byte[] audioData;
                using (var memoryStream = new MemoryStream()) {
                    await filePart.Data.CopyToAsync(memoryStream);
                    audioData = memoryStream.ToArray();
                }

                // 验证音频数据
                if (audioData.Length == 0) {
                    Log.Error("无效的音频数据: 零长度");
                    throw new AudioProcessingException("INVALID_AUDIO_DATA", "音频数据不能为空");
                }

                Log.Information("开始处理音频文件: {FileName}, 长度: {Length}字节", fileName, audioData.Length);

                // 处理音频数据(采样率转换等)
                audioData = AudioProcessor.ProcessAudio(audioData, fileName);

                var result = await _whisperService.Transcribe(audioData);
                Log.Information("音频转写完成");

                return ApiResponse<TranscriptionResult>.Success(result);
            } catch (AudioProcessingException ex) {
                HttpContext.Response.StatusCode = 400;
                Log.Error(ex, "音频处理错误: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                return ApiResponse<TranscriptionResult>.CreateError(ex.ErrorCode, ex.Message);
            } catch (Exception ex) {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "转写失败");
                return ApiResponse<TranscriptionResult>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }
    }
}