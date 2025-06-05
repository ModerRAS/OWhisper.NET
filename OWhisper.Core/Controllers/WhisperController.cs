using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System;
using System.Threading.Tasks;
using Serilog;
using HttpMultipartParser;
using System.IO;
using System.Linq;
using OWhisper.Core.Models;
using OWhisper.Core.Services;

namespace OWhisper.Core.Controllers
{
    public class WhisperController : WebApiController
    {
        private readonly ITranscriptionQueueService _queueService;
        private readonly IPlatformPathService _pathService;

        public WhisperController()
        {
            _queueService = TranscriptionQueueService.Instance;
            _pathService = new PlatformPathService();
        }

        [Route(HttpVerbs.Get, "/")]
        public async Task<ApiResponse<object>> GetApiInfo()
        {
            return ApiResponse<object>.Success(new
            {
                endpoints = new[]
                {
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
        [Route(HttpVerbs.Get, "/api/model/status")]
        public async Task<ApiResponse<object>> GetModelStatus()
        {
            try
            {
                var whisperManager = new WhisperManager(_pathService);
                var (exists, valid, size, path) = whisperManager.CheckModelStatus();

                return ApiResponse<object>.Success(new
                {
                    exists,
                    valid,
                    size,
                    path
                });
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "获取模型状态失败");
                return ApiResponse<object>.CreateError("MODEL_STATUS_ERROR", "获取模型状态失败");
            }
        }

        /// <summary>
        /// 提交音频文件转录任务
        /// </summary>
        [Route(HttpVerbs.Post, "/api/transcribe")]
        public async Task<ApiResponse<TaskCreationResponse>> Transcribe()
        {
            try
            {
                // 解析multipart/form-data请求
                if (HttpContext.Request.InputStream == null)
                {
                    Log.Error("请求输入流为空");
                    throw new AudioProcessingException("INVALID_REQUEST", "无效的请求格式");
                }

                var parser = await MultipartFormDataParser.ParseAsync(HttpContext.Request.InputStream);
                var filePart = parser.Files.FirstOrDefault();

                if (filePart == null)
                {
                    Log.Error("请求中未包含文件");
                    throw new AudioProcessingException("NO_FILE_UPLOADED", "请上传音频文件");
                }

                // 验证文件格式
                var fileName = filePart.FileName?.ToLower() ?? string.Empty;
                if (!fileName.EndsWith(".mp3") && !fileName.EndsWith(".wav") && !fileName.EndsWith(".aac") && !fileName.EndsWith(".m4a"))
                {
                    Log.Error("不支持的文件格式: {FileName}", fileName);
                    throw new AudioProcessingException("UNSUPPORTED_FILE_FORMAT", "仅支持.mp3、.wav、.aac和.m4a格式");
                }

                // 检查VAD参数
                bool enableVad = true;
                try 
                {
                    // 使用更安全的方式访问VAD参数
                    var vadParam = parser.Parameters.FirstOrDefault(p => p.Name == "enable_vad");
                    if (vadParam != null)
                    {
                        try
                        {
                            // vadParam.Data 是字符串类型，直接使用
                            var value = vadParam.Data;
                            
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                if (bool.TryParse(value.Trim(), out var parsedVad))
                                {
                                    enableVad = parsedVad;
                                    Log.Information("成功解析VAD参数: {EnableVad}", enableVad);
                                }
                                else
                                {
                                    Log.Warning("无法解析VAD参数值: {Value}, 使用默认值", value);
                                }
                            }
                            else
                            {
                                Log.Warning("VAD参数值为空，使用默认值: {DefaultValue}", enableVad);
                            }
                        }
                        catch (Exception paramEx)
                        {
                            Log.Warning(paramEx, "解析单个VAD参数失败");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "解析VAD参数时发生未知错误，使用默认值: {DefaultValue}", enableVad);
                }
                
                Log.Information("VAD设置: 启用={EnableVad}", enableVad);

                // 读取文件数据
                byte[] audioData;
                using (var memoryStream = new MemoryStream())
                {
                    await filePart.Data.CopyToAsync(memoryStream);
                    audioData = memoryStream.ToArray();
                }

                // 验证音频数据
                if (audioData.Length == 0)
                {
                    Log.Error("无效的音频数据: 零长度");
                    throw new AudioProcessingException("INVALID_AUDIO_DATA", "音频数据不能为空");
                }

                Log.Information("接收音频文件: {FileName}, 长度: {Length}字节", fileName, audioData.Length);

                // 处理音频数据(采样率转换等)
                audioData = AudioProcessor.ProcessAudio(audioData, fileName);

                // 将任务加入队列
                var taskId = _queueService.EnqueueTask(audioData, fileName, null, null, enableVad);
                var task = _queueService.GetTask(taskId);

                Log.Information("任务已入队: {TaskId}", taskId);

                return ApiResponse<TaskCreationResponse>.Success(new TaskCreationResponse
                {
                    TaskId = taskId,
                    QueuePosition = task.QueuePosition // 使用正确的QueuePosition字段
                });
            }
            catch (AudioProcessingException ex)
            {
                HttpContext.Response.StatusCode = 400;
                Log.Error(ex, "音频处理错误: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                return ApiResponse<TaskCreationResponse>.CreateError(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "提交转录任务失败");
                return ApiResponse<TaskCreationResponse>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取所有任务列表
        /// </summary>
        [Route(HttpVerbs.Get, "/api/tasks")]
        public async Task<ApiResponse<object>> GetTasks()
        {
            try
            {
                var tasks = _queueService.GetAllTasks();
                return ApiResponse<object>.Success(new
                {
                    tasks = tasks.Select(t => new
                    {
                        id = t.Id,
                        fileName = t.FileName,
                        status = t.Status.ToString(),
                        progress = t.Progress,
                        queuePosition = t.QueuePosition, // 使用正确的QueuePosition字段
                        createdAt = t.CreatedAt,
                        startedAt = t.StartedAt,
                        completedAt = t.CompletedAt,
                        errorMessage = t.ErrorMessage
                    }),
                    queueLength = _queueService.GetQueueLength()
                });
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "获取任务列表失败");
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取指定任务详情
        /// </summary>
        [Route(HttpVerbs.Get, "/api/tasks/{taskId}")]
        public async Task<ApiResponse<object>> GetTask(string taskId)
        {
            try
            {
                var task = _queueService.GetTask(taskId);
                if (task == null)
                {
                    HttpContext.Response.StatusCode = 404;
                    return ApiResponse<object>.CreateError("TASK_NOT_FOUND", "任务不存在");
                }

                return ApiResponse<object>.Success(new
                {
                    id = task.Id,
                    fileName = task.FileName,
                    status = task.Status.ToString(),
                    progress = task.Progress,
                    queuePosition = task.QueuePosition, // 使用正确的QueuePosition字段
                    createdAt = task.CreatedAt,
                    startedAt = task.StartedAt,
                    completedAt = task.CompletedAt,
                    result = task.Result,
                    errorMessage = task.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "获取任务详情失败: {TaskId}", taskId);
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }

        /// <summary>
        /// 取消指定任务
        /// </summary>
        [Route(HttpVerbs.Post, "/api/tasks/{taskId}/cancel")]
        public async Task<ApiResponse<object>> CancelTask(string taskId)
        {
            try
            {
                var success = _queueService.CancelTask(taskId);
                if (!success)
                {
                    HttpContext.Response.StatusCode = 400;
                    return ApiResponse<object>.CreateError("CANNOT_CANCEL", "无法取消任务（任务不存在或已在处理中）");
                }

                return ApiResponse<object>.Success(new { message = "任务已取消" });
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = 500;
                Log.Error(ex, "取消任务失败: {TaskId}", taskId);
                return ApiResponse<object>.CreateError("INTERNAL_ERROR", "内部服务器错误");
            }
        }
    }
} 