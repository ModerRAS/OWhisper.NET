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

        public void Stop()
        {
            lock (_lock)
            {
                if (_status != ServiceStatus.Running) return;
                
                _status = ServiceStatus.Stopping;
                StatusChanged?.Invoke(this, _status);
                
                _serviceThread?.Join(1000);
                _serviceThread = null;
                _status = ServiceStatus.Stopped;
                StatusChanged?.Invoke(this, _status);
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

        private volatile bool _shouldStop = false;

        private void ServiceWorker()
        {
            _status = ServiceStatus.Running;
            StatusChanged?.Invoke(this, _status);

            // 初始化Whisper功能
            var whisperManager = new WhisperManager();
            
            try
            {
                while (!_shouldStop && _status == ServiceStatus.Running)
                {
                    // 主服务循环
                    Thread.Sleep(100);
                }
            }
            finally
            {
                whisperManager.Dispose();
            }
        }

        public void Dispose()
        {
            Console.WriteLine("开始释放WhisperService资源...");
            _shouldStop = true;
            Stop();
            
            // 确保线程完全终止
            if (_serviceThread != null && _serviceThread.IsAlive)
            {
                Console.WriteLine($"等待服务线程终止(线程ID: {_serviceThread.ManagedThreadId})...");
                if (!_serviceThread.Join(5000)) // 延长到5秒
                {
                    Console.WriteLine("错误: 服务线程未在超时时间内终止，尝试中止线程");
                    try
                    {
                        _serviceThread.Interrupt();
                        if (!_serviceThread.Join(1000))
                        {
                            _serviceThread.Abort();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"线程中止错误: {ex}");
                    }
                }
            }
            
            Console.WriteLine("WhisperService资源释放完成");
        }
    }

    internal class WhisperManager : IDisposable
    {
        private WhisperProcessor _processor;
        private const string ModelName = "ggml-large-v3-turbo.bin";
        const GgmlType ggmlType = GgmlType.LargeV3Turbo;
        private readonly string _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", ModelName);
        private readonly object _lock = new object();
        async Task DownloadModelAsync(GgmlType modelType, string targetModelsDir) {
            if (!Directory.Exists(targetModelsDir)) {
                Directory.CreateDirectory(targetModelsDir);
            }
            Console.WriteLine($"Model {ModelName} not found. Downloading...");
            using var client = HttpClientHelper.CreateProxyHttpClient();
            var downloader = new WhisperGgmlDownloader(client);
            using var modelStream = await downloader.GetGgmlModelAsync(modelType);
            using var fileWriter = File.OpenWrite(Path.Combine(targetModelsDir, ModelName));
            await modelStream.CopyToAsync(fileWriter);
            Console.WriteLine($"Model {ModelName} downloaded to {targetModelsDir}");
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
            var modelDir = Path.GetDirectoryName(_modelPath);
            if (!Directory.Exists(modelDir))
            {
                Directory.CreateDirectory(modelDir);
            }
            if (!File.Exists(_modelPath))
            {
                try
                {
                    await DownloadModelAsync(ggmlType, _modelPath);
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