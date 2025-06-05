using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OWhisper.Core.Models;
using Serilog;
using TaskStatus = OWhisper.Core.Models.TaskStatus;

namespace OWhisper.Core.Services
{
    public sealed class TranscriptionQueueService : ITranscriptionQueueService, IDisposable
    {
        private static readonly Lazy<TranscriptionQueueService> _instance =
            new Lazy<TranscriptionQueueService>(() => new TranscriptionQueueService());

        public static TranscriptionQueueService Instance => _instance.Value;

        public event EventHandler<TranscriptionTask> ProgressUpdated;

        private readonly ConcurrentDictionary<string, TranscriptionTask> _tasks = new ConcurrentDictionary<string, TranscriptionTask>();
        private readonly ConcurrentQueue<string> _taskQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, byte[]> _audioDataCache = new ConcurrentDictionary<string, byte[]>();
        private readonly ConcurrentDictionary<string, bool> _vadSettingsCache = new ConcurrentDictionary<string, bool>();
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);
        private Task _processingTask;
        private bool _isRunning = false;

        private TranscriptionQueueService()
        {
        }

        public string EnqueueTask(byte[] audioData, string fileName, string language = null, string model = null, bool enableVad = true)
        {
            // 参数验证
            if (audioData == null)
                throw new ArgumentNullException(nameof(audioData));
            
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("文件名不能为空", nameof(fileName));

            var task = new TranscriptionTask
            {
                Id = Guid.NewGuid().ToString(),
                FileName = fileName,
                Language = language,
                Model = model,
                CreatedAt = DateTime.Now,
                Status = TaskStatus.Queued
            };

            // 存储音频数据和VAD设置到缓存
            _audioDataCache[task.Id] = audioData;
            _vadSettingsCache[task.Id] = enableVad;

            _tasks[task.Id] = task;
            _taskQueue.Enqueue(task.Id);

            // 更新队列位置
            UpdateQueuePositions();

            Log.Information("任务已加入队列: {TaskId}, 文件: {FileName}", task.Id, fileName);

            return task.Id;
        }

        public TranscriptionTask GetTask(string taskId)
        {
            _tasks.TryGetValue(taskId, out var task);
            return task;
        }

        public List<TranscriptionTask> GetAllTasks()
        {
            return _tasks.Values.OrderBy(t => t.CreatedAt).ToList();
        }

        public bool CancelTask(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                if (task.Status == TaskStatus.Queued)
                {
                    task.Status = TaskStatus.Cancelled;
                    task.CompletedAt = DateTime.Now;
                    OnProgressUpdated(task);
                    return true;
                }
            }
            return false;
        }

        public int GetQueueLength()
        {
            return _taskQueue.Count;
        }

        public void Start()
        {
            if (_isRunning) return;

            // 始终重新创建CancellationTokenSource以避免任何状态问题
            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // 忽略已释放的异常
            }
            
            _cancellationTokenSource = new CancellationTokenSource();

            _isRunning = true;
            _processingTask = Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);
            Log.Information("转录队列服务已启动");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            
            try
            {
                _cancellationTokenSource?.Cancel();
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // 忽略取消异常
            }
            catch (ObjectDisposedException)
            {
                // 忽略已释放的异常
            }

            Log.Information("转录队列服务已停止");
        }

        private async Task ProcessQueueAsync()
        {
            Log.Information("开始处理转录队列");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_taskQueue.TryDequeue(out var taskId))
                    {
                        if (_tasks.TryGetValue(taskId, out var task) && task.Status == TaskStatus.Queued)
                        {
                            await ProcessTaskAsync(task);
                        }
                    }
                    else
                    {
                        // 队列为空，等待一段时间
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "处理队列时发生错误");
                    await Task.Delay(5000, _cancellationTokenSource.Token);
                }
            }

            Log.Information("转录队列处理已停止");
        }

        private async Task ProcessTaskAsync(TranscriptionTask task)
        {
            try
            {
                task.Status = TaskStatus.Processing;
                task.StartedAt = DateTime.Now;
                task.Progress = 0;
                OnProgressUpdated(task);

                Log.Information("开始处理任务: {TaskId}", task.Id);

                // 获取缓存的音频数据和VAD设置
                if (!_audioDataCache.TryGetValue(task.Id, out var audioData))
                {
                    throw new Exception("未找到音频数据");
                }
                
                var enableVad = _vadSettingsCache.TryGetValue(task.Id, out var vadSetting) ? vadSetting : true;

                // 使用实际的Whisper转录服务
                var whisperService = WhisperService.Instance;
                
                // 订阅进度事件
                EventHandler<float> progressHandler = (sender, progress) =>
                {
                    task.Progress = progress;
                    OnProgressUpdated(task);
                };

                whisperService.ProgressChanged += progressHandler;

                try
                {
                    Log.Information("开始转录音频，大小: {Size}字节, VAD启用: {EnableVad}", audioData.Length, enableVad);
                    var result = await whisperService.Transcribe(audioData, enableVad);
                    
                    if (result == null)
                    {
                        throw new Exception("转录结果为空");
                    }
                    
                    task.Result = result;
                    task.Status = TaskStatus.Completed;
                    task.Progress = 100;
                    task.CompletedAt = DateTime.Now;

                    Log.Information("任务处理完成: {TaskId}, 转录文本长度: {TextLength}", task.Id, result.Text?.Length ?? 0);
                }
                catch (AudioProcessingException ex)
                {
                    Log.Error(ex, "音频处理失败: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                    throw new Exception($"音频处理失败: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Whisper转录失败");
                    throw new Exception($"转录失败: {ex.Message}", ex);
                }
                finally
                {
                    whisperService.ProgressChanged -= progressHandler;
                    // 清理音频数据和VAD设置缓存
                    _audioDataCache.TryRemove(task.Id, out _);
                    _vadSettingsCache.TryRemove(task.Id, out _);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理任务失败: {TaskId}", task.Id);
                
                task.Status = TaskStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.CompletedAt = DateTime.Now;
                
                // 清理音频数据和VAD设置缓存
                _audioDataCache.TryRemove(task.Id, out _);
                _vadSettingsCache.TryRemove(task.Id, out _);
            }
            finally
            {
                OnProgressUpdated(task);
                UpdateQueuePositions();
            }
        }

        private void UpdateQueuePositions()
        {
            var pendingTasks = _tasks.Values.Where(t => t.Status == TaskStatus.Queued).OrderBy(t => t.CreatedAt).ToList();
            for (int i = 0; i < pendingTasks.Count; i++)
            {
                pendingTasks[i].QueuePosition = i + 1; // 使用正确的QueuePosition字段
            }
        }

        private void OnProgressUpdated(TranscriptionTask task)
        {
            ProgressUpdated?.Invoke(this, task);
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _processingLock?.Dispose();
        }
    }
} 