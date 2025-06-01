using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using Serilog;
using OWhisper.Core.Models;
using OWhisper.Core.Services;
using System.Threading;
using Newtonsoft.Json;
using TaskStatus = OWhisper.Core.Models.TaskStatus;

namespace OWhisper.Core.Controllers
{
    public class SseController : WebApiController
    {
        private readonly ITranscriptionQueueService _queueService;

        public SseController()
        {
            _queueService = TranscriptionQueueService.Instance;
        }

        /// <summary>
        /// SSE接口 - 获取指定任务的实时进度
        /// </summary>
        /// <param name="taskId">任务ID</param>
        [Route(HttpVerbs.Get, "/api/tasks/{taskId}/progress")]
        public async System.Threading.Tasks.Task GetTaskProgress(string taskId)
        {
            // 设置SSE响应头
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = "text/event-stream";
            HttpContext.Response.Headers["Cache-Control"] = "no-cache";
            HttpContext.Response.Headers["Connection"] = "keep-alive";
            HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
            HttpContext.Response.Headers["Access-Control-Allow-Headers"] = "Cache-Control";

            var responseStream = HttpContext.Response.OutputStream;
            var cancellationToken = HttpContext.CancellationToken;

            try
            {
                Log.Information("SSE连接建立: TaskId={TaskId}", taskId);

                var task = _queueService.GetTask(taskId);
                if (task == null)
                {
                    await SendSseMessage(responseStream, "error", new { error = "任务不存在" });
                    return;
                }

                // 发送初始状态
                await SendTaskProgress(responseStream, task);

                // 如果任务已完成，直接返回
                if (task.Status == TaskStatus.Completed ||
                    task.Status == TaskStatus.Failed ||
                    task.Status == TaskStatus.Cancelled)
                {
                    return;
                }

                // 订阅进度更新事件
                var progressReceived = false;
                EventHandler<TranscriptionTask> progressHandler = async (sender, progress) =>
                {
                    if (progress.Id == taskId && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await SendTaskProgress(responseStream, progress);
                            progressReceived = true;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "发送SSE消息失败");
                        }
                    }
                };

                _queueService.ProgressUpdated += progressHandler;

                try
                {
                    // 保持连接直到任务完成或客户端断开
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var currentTask = _queueService.GetTask(taskId);
                        if (currentTask == null ||
                            currentTask.Status == TaskStatus.Completed ||
                            currentTask.Status == TaskStatus.Failed ||
                            currentTask.Status == TaskStatus.Cancelled)
                        {
                            break;
                        }

                        // 每2秒发送一次心跳
                        await System.Threading.Tasks.Task.Delay(2000, cancellationToken);
                        await SendSseMessage(responseStream, "heartbeat", new { timestamp = DateTime.UtcNow });
                    }
                }
                finally
                {
                    _queueService.ProgressUpdated -= progressHandler;
                }

                Log.Information("SSE连接关闭: TaskId={TaskId}", taskId);
            }
            catch (OperationCanceledException)
            {
                Log.Information("SSE连接被客户端取消: TaskId={TaskId}", taskId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SSE连接错误: TaskId={TaskId}", taskId);
            }
        }

        /// <summary>
        /// SSE接口 - 获取队列状态
        /// </summary>
        [Route(HttpVerbs.Get, "/api/queue/progress")]
        public async System.Threading.Tasks.Task GetQueueProgress()
        {
            // 设置SSE响应头
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = "text/event-stream";
            HttpContext.Response.Headers["Cache-Control"] = "no-cache";
            HttpContext.Response.Headers["Connection"] = "keep-alive";
            HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
            HttpContext.Response.Headers["Access-Control-Allow-Headers"] = "Cache-Control";

            var responseStream = HttpContext.Response.OutputStream;
            var cancellationToken = HttpContext.CancellationToken;

            try
            {
                Log.Information("队列SSE连接建立");

                // 发送初始队列状态
                var allTasks = _queueService.GetAllTasks();
                await SendSseMessage(responseStream, "queue-status", new
                {
                    tasks = allTasks.Count,
                    queueLength = _queueService.GetQueueLength()
                });

                // 订阅进度更新事件
                EventHandler<TranscriptionTask> progressHandler = async (sender, progress) =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await SendTaskProgress(responseStream, progress);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "发送队列SSE消息失败");
                        }
                    }
                };

                _queueService.ProgressUpdated += progressHandler;

                try
                {
                    // 保持连接
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await System.Threading.Tasks.Task.Delay(5000, cancellationToken);
                        var tasks = _queueService.GetAllTasks();
                        await SendSseMessage(responseStream, "queue-status", new
                        {
                            tasks = tasks.Count,
                            queueLength = _queueService.GetQueueLength()
                        });
                    }
                }
                finally
                {
                    _queueService.ProgressUpdated -= progressHandler;
                }

                Log.Information("队列SSE连接关闭");
            }
            catch (OperationCanceledException)
            {
                Log.Information("队列SSE连接被客户端取消");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "队列SSE连接错误");
            }
        }

        private async System.Threading.Tasks.Task SendTaskProgress(Stream responseStream, TranscriptionTask task)
        {
            var progress = new TranscriptionProgress
            {
                TaskId = task.Id,
                Status = task.Status,
                Progress = task.Progress,
                QueuePosition = task.QueuePosition,
                Message = GetStatusMessage(task),
                Result = task.Result,
                ErrorMessage = task.ErrorMessage
            };

            await SendProgressUpdate(responseStream, progress);
        }

        private async System.Threading.Tasks.Task SendProgressUpdate(Stream responseStream, TranscriptionProgress progress)
        {
            await SendSseMessage(responseStream, "progress", progress);
        }

        private async System.Threading.Tasks.Task SendSseMessage(Stream responseStream, string eventType, object data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data);
                var message = $"event: {eventType}\ndata: {json}\n\n";
                var bytes = Encoding.UTF8.GetBytes(message);
                await responseStream.WriteAsync(bytes, 0, bytes.Length);
                await responseStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "发送SSE消息失败");
                throw;
            }
        }

        private string GetStatusMessage(TranscriptionTask task)
        {
            return task.Status switch
            {
                TaskStatus.Queued => $"队列中等待，位置: {task.QueuePosition}",
                TaskStatus.Processing => $"正在处理... ({task.Progress:F1}%)",
                TaskStatus.Completed => "转录完成",
                TaskStatus.Failed => $"处理失败: {task.ErrorMessage}",
                TaskStatus.Cancelled => "已取消",
                _ => "未知状态"
            };
        }
    }
} 