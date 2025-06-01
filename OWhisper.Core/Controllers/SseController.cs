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

            // 添加状态缓存以避免重复发送
            TranscriptionProgress lastSentProgress = null;

            try
            {
                Log.Information("SSE连接建立: TaskId={TaskId}", taskId);

                var task = _queueService.GetTask(taskId);
                if (task == null)
                {
                    await SendSseMessage(responseStream, "error", new { error = "任务不存在" });
                    return;
                }

                // 发送初始状态并缓存
                lastSentProgress = await SendTaskProgressWithCache(responseStream, task, lastSentProgress);

                // 如果任务已完成，直接返回
                if (task.Status == TaskStatus.Completed ||
                    task.Status == TaskStatus.Failed ||
                    task.Status == TaskStatus.Cancelled)
                {
                    return;
                }

                // 订阅进度更新事件
                EventHandler<TranscriptionTask> progressHandler = async (sender, progress) =>
                {
                    if (progress.Id == taskId && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            lastSentProgress = await SendTaskProgressWithCache(responseStream, progress, lastSentProgress);
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

                        // 每5秒发送一次心跳（增加间隔减少干扰）
                        await System.Threading.Tasks.Task.Delay(5000, cancellationToken);
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
        /// 带缓存的发送任务进度，避免重复发送相同状态
        /// </summary>
        private async Task<TranscriptionProgress> SendTaskProgressWithCache(Stream responseStream, TranscriptionTask task, TranscriptionProgress lastProgress)
        {
            // 添加调试日志
            if (task.Status == TaskStatus.Unknown)
            {
                Log.Warning("检测到任务状态为Unknown: TaskId={TaskId}, CreatedAt={CreatedAt}", task.Id, task.CreatedAt);
            }
            
            var currentProgress = new TranscriptionProgress
            {
                TaskId = task.Id,
                Status = task.Status,
                Progress = task.Progress,
                QueuePosition = task.QueuePosition,
                Message = GetStatusMessage(task),
                Result = task.Result,
                ErrorMessage = task.ErrorMessage
            };

            // 检查是否需要发送（避免重复发送相同状态）
            if (ShouldSendProgress(currentProgress, lastProgress))
            {
                await SendProgressUpdate(responseStream, currentProgress);
                Log.Debug("发送进度更新: TaskId={TaskId}, Status={Status}, Progress={Progress}%", 
                    task.Id, task.Status, task.Progress);
                return currentProgress;
            }
            else
            {
                Log.Debug("跳过重复的进度更新: TaskId={TaskId}, Status={Status}", task.Id, task.Status);
                return lastProgress; // 返回上次发送的进度
            }
        }

        /// <summary>
        /// 判断是否需要发送进度更新
        /// </summary>
        private bool ShouldSendProgress(TranscriptionProgress current, TranscriptionProgress last)
        {
            // 如果没有上次发送的记录，肯定要发送
            if (last == null)
                return true;

            // 状态改变了，必须发送
            if (current.Status != last.Status)
                return true;

            // 队列位置改变了，需要发送
            if (current.QueuePosition != last.QueuePosition)
                return true;

            // 对于Processing状态，只在进度有明显变化时发送（至少1%的变化）
            if (current.Status == TaskStatus.Processing)
            {
                var progressDiff = Math.Abs(current.Progress - last.Progress);
                return progressDiff >= 1.0f; // 至少1%的变化才发送
            }

            // 其他情况不需要重复发送
            return false;
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