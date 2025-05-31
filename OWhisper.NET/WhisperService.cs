using System;
using OWhisper.NET.Models;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Whisper.net;
using Whisper.net.Ggml;

namespace OWhisper.NET
{
    public sealed class WhisperService : IDisposable
    {
        private static readonly Lazy<WhisperService> _instance = 
            new Lazy<WhisperService>(() => new WhisperService());
        
        public static WhisperService Instance => _instance.Value;

        public enum ServiceStatus
        {
            Stopped,
            Starting,
            Running,
            Stopping
        }

        private ServiceStatus _status = ServiceStatus.Stopped;
        private Thread _serviceThread;
        private readonly object _lock = new object();

        public event EventHandler<ServiceStatus> StatusChanged;

        private WhisperService() { }

        public ServiceStatus GetStatus() => _status;

        private bool IsValidMp3(byte[] audioData)
        {
            try
            {
                // MP3文件头检查: 0xFF 0xFB或0xFF 0xF3
                return audioData != null &&
                       audioData.Length > 2 &&
                      ((audioData[0] == 0xFF && audioData[1] == 0xFB) ||
                       (audioData[0] == 0xFF && audioData[1] == 0xF3));
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidWav(byte[] audioData)
        {
            try
            {
                // WAV文件头检查: "RIFF"标记
                return audioData != null &&
                       audioData.Length > 12 &&
                       audioData[0] == 'R' &&
                       audioData[1] == 'I' &&
                       audioData[2] == 'F' &&
                       audioData[3] == 'F' &&
                       audioData[8] == 'W' &&
                       audioData[9] == 'A' &&
                       audioData[10] == 'V' &&
                       audioData[11] == 'E';
            }
            catch
            {
                return false;
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_status != ServiceStatus.Stopped) return;
                
                _status = ServiceStatus.Starting;
                StatusChanged?.Invoke(this, _status);
                
                _serviceThread = new Thread(ServiceWorker)
                {
                    IsBackground = true
                };
                _serviceThread.Start();
            }
        }

        /// <summary>
        /// 转录音频数据为文本
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <returns>转写文本</returns>
        public async Task<TranscriptionResult> Transcribe(byte[] audioData)
        {
            if (_status != ServiceStatus.Running)
            {
                throw new AudioProcessingException("SERVICE_NOT_RUNNING", "语音识别服务未运行");
            }

            // 前置音频验证
            if (!IsValidMp3(audioData) && !IsValidWav(audioData))
            {
                throw new AudioProcessingException("INVALID_AUDIO_FORMAT", "不支持的音频格式");
            }

            var startTime = DateTime.UtcNow;
            try
            {
                using var whisperManager = new WhisperManager();
                var text = await whisperManager.Transcribe(audioData);
                return new TranscriptionResult
                {
                    Text = text,
                    ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"转写过程中发生错误: {ex}");
                throw;
            }
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private void ServiceWorker()
        {
            _status = ServiceStatus.Running;
            StatusChanged?.Invoke(this, _status);

            // 初始化Whisper功能
            var whisperManager = new WhisperManager();
            
            try
            {
                while (!_cts.IsCancellationRequested && _status == ServiceStatus.Running)
                {
                    // 主服务循环
                    Thread.Sleep(100);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("服务线程收到取消请求，正在优雅退出...");
            }
            finally
            {
                whisperManager.Dispose();
                Console.WriteLine("服务线程资源已释放");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_status != ServiceStatus.Running)
                {
                    Console.WriteLine($"服务状态为{_status}，无需停止");
                    return;
                }
                
                _status = ServiceStatus.Stopping;
                StatusChanged?.Invoke(this, _status);
                Console.WriteLine("正在停止服务线程...");
                
                _cts.Cancel();
                
                if (_serviceThread != null && _serviceThread.IsAlive)
                {
                    if (!_serviceThread.Join(TimeSpan.FromSeconds(5)))
                    {
                        Console.WriteLine($"警告: 线程{_serviceThread.ManagedThreadId}未在5秒内退出");
                    }
                }
                
                _serviceThread = null;
                _status = ServiceStatus.Stopped;
                StatusChanged?.Invoke(this, _status);
                Console.WriteLine("服务已完全停止");
            }
        }

        public void Dispose()
        {
            try
            {
                Stop();
                
                if (_serviceThread != null && _serviceThread.IsAlive)
                {
                    try
                    {
                        _serviceThread.Abort();
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // 忽略平台不支持异常
                    }
                }
                
                _cts?.Dispose();
            }
            catch
            {
                // 记录错误到日志系统
                throw;
            }
            
            GC.SuppressFinalize(this);
        }
    }

    public class WhisperManager : IDisposable
    {
        private WhisperProcessor _processor;
        private const string ModelName = "ggml-large-v3-turbo.bin";
        const GgmlType ggmlType = GgmlType.LargeV3Turbo;
        private readonly string _modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        private readonly string _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", ModelName);
        private readonly object _lock = new object();

        public (bool exists, bool valid, long size, string path) CheckModelStatus()
        {
            try
            {
                var exists = File.Exists(_modelPath);
                var valid = false;
                long size = 0;
                
                if (exists)
                {
                    var fileInfo = new FileInfo(_modelPath);
                    size = fileInfo.Length;
                    // 简单验证模型文件大小(假设有效模型至少10MB)
                    valid = size > 10 * 1024 * 1024;
                }
                
                return (exists, valid, size, _modelPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查模型状态失败: {ex}");
                return (false, false, 0, _modelPath);
            }
        }
        public async Task DownloadModelAsync(GgmlType modelType, string targetModelsDir) {
            if (!Directory.Exists(targetModelsDir)) {
                try {
                    Directory.CreateDirectory(targetModelsDir);
                    Console.WriteLine($"创建模型目录: {targetModelsDir}");
                } catch (Exception ex) {
                    Console.WriteLine($"创建模型目录失败: {ex}");
                    throw new AudioProcessingException("MODEL_DIR_CREATE_FAILED", $"无法创建模型目录: {ex.Message}");
                }
            }

            Console.WriteLine($"开始下载模型: {ModelName}...");
            var downloadStartTime = DateTime.UtcNow;
            
            try {
                using var client = HttpClientHelper.CreateProxyHttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);
                var downloader = new WhisperGgmlDownloader(client);
                
                using var modelStream = await downloader.GetGgmlModelAsync(modelType);
                var tempFilePath = Path.Combine(targetModelsDir, $"{ModelName}.tmp");
                
                try {
                    using var fileWriter = File.OpenWrite(tempFilePath);
                    var buffer = new byte[8192];
                    long totalBytes = 0;
                    int bytesRead;
                    
                    while ((bytesRead = await modelStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                        await fileWriter.WriteAsync(buffer, 0, bytesRead);
                        totalBytes += bytesRead;
                        
                        if (DateTime.UtcNow - downloadStartTime > TimeSpan.FromSeconds(5)) {
                            Console.WriteLine($"已下载: {totalBytes / (1024 * 1024)}MB");
                            downloadStartTime = DateTime.UtcNow;
                        }
                    }
                    
                    var finalPath = Path.Combine(targetModelsDir, ModelName);
                    if (File.Exists(finalPath)) {
                        File.Delete(finalPath);
                    }
                    File.Move(tempFilePath, finalPath);
                    Console.WriteLine($"模型下载完成，保存到: {Path.Combine(targetModelsDir, ModelName)}");
                } catch (IOException ex) {
                    if (File.Exists(tempFilePath)) {
                        File.Delete(tempFilePath);
                    }
                    Console.WriteLine($"文件写入失败: {ex}");
                    throw new AudioProcessingException("MODEL_WRITE_FAILED", $"模型文件写入失败: {ex.Message}");
                }
            } catch (System.Net.Http.HttpRequestException ex) {
                Console.WriteLine($"网络请求失败: {ex}");
                throw new AudioProcessingException("NETWORK_ERROR", $"下载模型失败: {ex.Message}");
            } catch (TaskCanceledException ex) {
                Console.WriteLine($"下载超时: {ex}");
                throw new AudioProcessingException("DOWNLOAD_TIMEOUT", "模型下载超时");
            } catch (Exception ex) {
                Console.WriteLine($"下载过程中发生未知错误: {ex}");
                throw new AudioProcessingException("DOWNLOAD_FAILED", $"模型下载失败: {ex.Message}");
            }
        }

        private bool IsValidMp3(byte[] audioData)
        {
            // MP3文件头检查: 0xFF 0xFB或0xFF 0xF3
            return audioData.Length > 2 &&
                  ((audioData[0] == 0xFF && audioData[1] == 0xFB) ||
                   (audioData[0] == 0xFF && audioData[1] == 0xF3));
        }

        private bool IsValidWav(byte[] audioData)
        {
            // WAV文件头检查: "RIFF"标记
            return audioData.Length > 12 &&
                   audioData[0] == 'R' &&
                   audioData[1] == 'I' &&
                   audioData[2] == 'F' &&
                   audioData[3] == 'F' &&
                   audioData[8] == 'W' &&
                   audioData[9] == 'A' &&
                   audioData[10] == 'V' &&
                   audioData[11] == 'E';
        }

        public async Task<string> Transcribe(byte[] audioData)
        {
            TimeSpan timeTaken;
            var startTime = DateTime.UtcNow;
            
            // 确保模型目录存在
            if (!Directory.Exists(_modelDir))
            {
                Directory.CreateDirectory(_modelDir);
            }
            if (!File.Exists(_modelPath))
            {
                try
                {
                    await DownloadModelAsync(ggmlType, _modelDir);
                    timeTaken = DateTime.UtcNow - startTime;
                    Console.WriteLine($"Time Taken to Download: {timeTaken.TotalSeconds} Seconds");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"模型下载失败: {ex}");
                    throw new Exception("模型下载失败", ex);
                }
            }
            using var whisperFactory = WhisperFactory.FromPath(_modelPath);

            // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
            await using var processor = whisperFactory.CreateBuilder()
                                                      .WithThreads(16)
                                                      //.WithLanguage("zh")
                                                      .WithLanguageDetection()
                                                      //.WithPrompt(prompt)
                                                      .Build();

            timeTaken = DateTime.UtcNow - startTime;
            Console.WriteLine("Time Taken to init Whisper: {0}", timeTaken.ToString());

            using var wavStream = new MemoryStream(audioData);
            wavStream.Seek(0, SeekOrigin.Begin);

            Console.WriteLine("⟫ Starting Whisper processing...");

            startTime = DateTime.UtcNow;

            var ToReturn = new List<string>();

            // This section processes the audio file and prints the results (start time, end time and text) to the console.
            var startId = 1;
            string lastText = string.Empty;
            var repeatCount = 0;
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            try
            {
                await foreach (var result in processor.ProcessAsync(wavStream, token))
                {
                    timeTaken = DateTime.UtcNow - startTime;
                    Console.WriteLine($"{result.Start.ToString()}-->{result.End.ToString()}: {result.Text,-150} [{timeTaken.ToString()}]");
                    ToReturn.Add($"{startId}");
                    ToReturn.Add($"{result.Start.ToString(@"hh\:mm\:ss\,fff")} --> {result.End.ToString(@"hh\:mm\:ss\,fff")}");
                    ToReturn.Add($"{result.Text}\n");
                    startId++;
                    startTime = DateTime.UtcNow;
                    if (lastText.Equals(result.Text.Trim()))
                    {
                        repeatCount++;
                    }
                    else
                    {
                        repeatCount = 0;
                        lastText = result.Text.Trim();
                    }
                    if (repeatCount > 100)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine(ex.ToString());
            }


            Console.WriteLine("⟫ Completed Whisper processing...");

            return string.Join("\n", ToReturn);
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _processor = null;
        }
    }
}