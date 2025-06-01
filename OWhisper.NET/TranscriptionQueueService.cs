using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OWhisper.NET.Models;
using Serilog;

namespace OWhisper.NET {
    public sealed class TranscriptionQueueService : IDisposable {
        private static readonly Lazy<TranscriptionQueueService> _instance =
            new Lazy<TranscriptionQueueService>(() => new TranscriptionQueueService());

        public static TranscriptionQueueService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, TranscriptionTask> _tasks = new ConcurrentDictionary<string, TranscriptionTask>();
        private readonly Queue<string> _taskQueue = new Queue<string>();
        private readonly object _queueLock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isProcessing = false;

        public event EventHandler<TranscriptionProgress> ProgressUpdated;

        private TranscriptionQueueService() {
            // 启动队列处理循环
            Task.Run(ProcessQueueAsync);
        }

        public string EnqueueTask(byte[] audioData, string fileName) {
            var taskId = Guid.NewGuid().ToString("N");
            var task = new TranscriptionTask {
                Id = taskId,
                Status = Models.TaskStatus.Queued,
                Progress = 0,
                FileName = fileName,
                CreatedAt = DateTime.UtcNow
            };

            _tasks[taskId] = task;

            lock (_queueLock) {
                _taskQueue.Enqueue(taskId);
                task.QueuePosition = _taskQueue.Count;
                
                // 更新所有队列中任务的位置
                UpdateQueuePositions();
            }

            // 存储音频数据 (在实际应用中可能需要存储到临时文件)
            _audioDataCache[taskId] = audioData;

            Log.Information("任务入队: {TaskId}, 文件名: {FileName}, 队列位置: {Position}", 
                taskId, fileName, task.QueuePosition);

            // 通知进度更新
            NotifyProgress(taskId);

            return taskId;
        }

        private readonly ConcurrentDictionary<string, byte[]> _audioDataCache = new ConcurrentDictionary<string, byte[]>();

        public TranscriptionTask GetTask(string taskId) {
            return _tasks.TryGetValue(taskId, out var task) ? task : null;
        }

        public List<TranscriptionTask> GetAllTasks() {
            return _tasks.Values.OrderBy(t => t.CreatedAt).ToList();
        }

        public bool CancelTask(string taskId) {
            if (_tasks.TryGetValue(taskId, out var task)) {
                if (task.Status == Models.TaskStatus.Queued) {
                    task.Status = Models.TaskStatus.Cancelled;
                    lock (_queueLock) {
                        var tempQueue = new Queue<string>();
                        while (_taskQueue.Count > 0) {
                            var id = _taskQueue.Dequeue();
                            if (id != taskId) {
                                tempQueue.Enqueue(id);
                            }
                        }
                        while (tempQueue.Count > 0) {
                            _taskQueue.Enqueue(tempQueue.Dequeue());
                        }
                        UpdateQueuePositions();
                    }
                    
                    _audioDataCache.TryRemove(taskId, out _);
                    NotifyProgress(taskId);
                    Log.Information("任务已取消: {TaskId}", taskId);
                    return true;
                }
            }
            return false;
        }

        private void UpdateQueuePositions() {
            var position = 1;
            foreach (var taskId in _taskQueue) {
                if (_tasks.TryGetValue(taskId, out var task)) {
                    task.QueuePosition = position++;
                }
            }
        }

        private async Task ProcessQueueAsync() {
            while (!_cancellationTokenSource.Token.IsCancellationRequested) {
                try {
                    string taskId = null;
                    
                    lock (_queueLock) {
                        if (_taskQueue.Count > 0 && !_isProcessing) {
                            taskId = _taskQueue.Dequeue();
                            _isProcessing = true;
                            UpdateQueuePositions();
                        }
                    }

                    if (taskId != null && _tasks.TryGetValue(taskId, out var task)) {
                        await ProcessTask(task);
                        _isProcessing = false;
                    } else {
                        // 如果没有任务，等待一会儿
                        await Task.Delay(500, _cancellationTokenSource.Token);
                    }
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    Log.Error(ex, "队列处理过程中发生错误");
                    _isProcessing = false;
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
        }

        private async Task ProcessTask(TranscriptionTask task) {
            try {
                Log.Information("开始处理任务: {TaskId}", task.Id);
                
                task.Status = Models.TaskStatus.Processing;
                task.StartedAt = DateTime.UtcNow;
                task.Progress = 0;
                NotifyProgress(task.Id);

                if (!_audioDataCache.TryGetValue(task.Id, out var audioData)) {
                    throw new Exception("音频数据不存在");
                }

                var whisperService = WhisperService.Instance;
                
                // 订阅进度事件
                EventHandler<float> progressHandler = (sender, progress) => {
                    task.Progress = progress;
                    NotifyProgress(task.Id);
                };
                
                whisperService.ProgressChanged += progressHandler;
                
                try {
                    var result = await whisperService.Transcribe(audioData);
                    task.Result = result;
                    task.Status = Models.TaskStatus.Completed;
                    task.CompletedAt = DateTime.UtcNow;
                    task.Progress = 100;

                    Log.Information("任务完成: {TaskId}, 耗时: {ProcessingTime}秒", 
                        task.Id, result.ProcessingTime);
                } finally {
                    whisperService.ProgressChanged -= progressHandler;
                }

            } catch (Exception ex) {
                Log.Error(ex, "处理任务失败: {TaskId}", task.Id);
                task.Status = Models.TaskStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.CompletedAt = DateTime.UtcNow;
            } finally {
                // 清理音频数据缓存
                _audioDataCache.TryRemove(task.Id, out _);
                NotifyProgress(task.Id);
            }
        }

        private void NotifyProgress(string taskId) {
            if (_tasks.TryGetValue(taskId, out var task)) {
                var progress = new TranscriptionProgress {
                    TaskId = task.Id,
                    Status = task.Status,
                    Progress = task.Progress,
                    QueuePosition = task.QueuePosition,
                    Message = GetStatusMessage(task),
                    Result = task.Result,
                    ErrorMessage = task.ErrorMessage
                };

                ProgressUpdated?.Invoke(this, progress);
            }
        }

        private string GetStatusMessage(TranscriptionTask task) {
            return task.Status switch {
                Models.TaskStatus.Queued => $"队列中等待，位置: {task.QueuePosition}",
                Models.TaskStatus.Processing => $"正在处理... ({task.Progress:F1}%)",
                Models.TaskStatus.Completed => "转录完成",
                Models.TaskStatus.Failed => $"处理失败: {task.ErrorMessage}",
                Models.TaskStatus.Cancelled => "已取消",
                _ => "未知状态"
            };
        }

        public int GetQueueLength() {
            lock (_queueLock) {
                return _taskQueue.Count + (_isProcessing ? 1 : 0);
            }
        }

        public void Dispose() {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _audioDataCache.Clear();
            _tasks.Clear();
        }
    }
} 