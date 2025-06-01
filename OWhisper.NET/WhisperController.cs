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
        private readonly TranscriptionQueueService _queueService = TranscriptionQueueService.Instance;

        public WhisperController() {
        }

        [Route(HttpVerbs.Get, "/")]
        public async Task<ApiResponse<object>> GetApiInfo() {
            return ApiResponse<object>.Success(new {
                endpoints = new[] {
                    "/api/model/status",
                    "/api/transcribe",
                    "/api/tasks",
                    "/api/tasks/{taskId}",
                    "/api/tasks/{taskId}/cancel",
                    "/api/tasks/{taskId}/progress",
                    "/api/queue/progress"
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
        /// 提交音频文件转录任务
        /// </summary>
        /// <returns>
        /// 成功返回:
        /// {
        ///     status: "success",
        ///     data: {
        ///         taskId: "任务ID",
        ///         queuePosition: "队列位置"
        ///     }
        /// }
        /// 失败返回:
        /// {
        ///     status: "error",
        ///     error: "错误信息"
        /// }
        /// </returns>
        [Route(HttpVerbs.Post, "/api/transcribe")]
        public async Task<ApiResponse<TaskCreationResponse>> Transcribe() {
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

                Log.Information("接收音频文件: {FileName}, 长度: {Length}字节", fileName, audioData.Length);

                // 处理音频数据(采样率转换等)
                audioData = AudioProcessor.ProcessAudio(audioData, fileName);

                // 将任务加入队列
                var taskId = _queueService.EnqueueTask(audioData, fileName);
                var task = _queueService.GetTask(taskId);

                Log.Information("任务已入队: {TaskId}, 队列位置: {QueuePosition}", taskId, task.QueuePosition);

                return ApiResponse<TaskCreationResponse>.Success(new TaskCreationResponse {
                    TaskId = taskId,
                    QueuePosition = task.QueuePosition
                });
            } catch (AudioProcessingException ex) {
                HttpContext.Response.StatusCode = 400;
                Log.Error(ex, "音频处理错误: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                return ApiResponse<TaskCreationResponse>.CreateError(ex.ErrorCode, ex.Message);
            } catch (Exception ex) {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "提交转录任务失败");
                return ApiResponse<TaskCreationResponse>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取所有任务列表
        /// </summary>
        [Route(HttpVerbs.Get, "/api/tasks")]
        public async Task<ApiResponse<object>> GetTasks() {
            try {
                var tasks = _queueService.GetAllTasks();
                return ApiResponse<object>.Success(new {
                    tasks = tasks.Select(t => new {
                        id = t.Id,
                        fileName = t.FileName,
                        status = t.Status.ToString(),
                        progress = t.Progress,
                        queuePosition = t.QueuePosition,
                        createdAt = t.CreatedAt,
                        startedAt = t.StartedAt,
                        completedAt = t.CompletedAt,
                        errorMessage = t.ErrorMessage
                    }),
                    queueLength = _queueService.GetQueueLength()
                });
            } catch (Exception ex) {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "获取任务列表失败");
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取指定任务详情
        /// </summary>
        [Route(HttpVerbs.Get, "/api/tasks/{taskId}")]
        public async Task<ApiResponse<object>> GetTask(string taskId) {
            try {
                var task = _queueService.GetTask(taskId);
                if (task == null) {
                    HttpContext.Response.StatusCode = 404;
                    return ApiResponse<object>.CreateError("TASK_NOT_FOUND", "任务不存在");
                }

                return ApiResponse<object>.Success(new {
                    id = task.Id,
                    fileName = task.FileName,
                    status = task.Status.ToString(),
                    progress = task.Progress,
                    queuePosition = task.QueuePosition,
                    createdAt = task.CreatedAt,
                    startedAt = task.StartedAt,
                    completedAt = task.CompletedAt,
                    result = task.Result,
                    errorMessage = task.ErrorMessage
                });
            } catch (Exception ex) {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "获取任务详情失败: {TaskId}", taskId);
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 取消指定任务
        /// </summary>
        [Route(HttpVerbs.Post, "/api/tasks/{taskId}/cancel")]
        public async Task<ApiResponse<object>> CancelTask(string taskId) {
            try {
                var success = _queueService.CancelTask(taskId);
                if (!success) {
                    HttpContext.Response.StatusCode = 400;
                    return ApiResponse<object>.CreateError("CANNOT_CANCEL", "无法取消任务（任务不存在或已在处理中）");
                }

                return ApiResponse<object>.Success(new { message = "任务已取消" });
            } catch (Exception ex) {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "取消任务失败: {TaskId}", taskId);
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }
    }
}