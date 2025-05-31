using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace OWhisper.NET
{
    public sealed class WhisperService
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
        public async Task<string> Transcribe(byte[] audioData)
        {
            if (_status != ServiceStatus.Running)
            {
                throw new InvalidOperationException("服务未运行");
            }

            using var whisperManager = new WhisperManager();
            return await whisperManager.Transcribe(audioData);
        }

        private void ServiceWorker()
        {
            _status = ServiceStatus.Running;
            StatusChanged?.Invoke(this, _status);

            // 初始化Whisper功能
            var whisperManager = new WhisperManager();
            
            try
            {
                while (_status == ServiceStatus.Running)
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
    }

    internal class WhisperManager : IDisposable
    {
        private WhisperProcessor _processor;
        private const string ModelName = "ggml-large-v3-turbo.bin";
        const GgmlType ggmlType = GgmlType.LargeV3Turbo;
        private readonly string _modelPath = ModelName;
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
        public async Task<string> Transcribe(byte[] audioData)
        {
            TimeSpan timeTaken;
            var startTime = DateTime.UtcNow;
            
            // 音频预处理
            try
            {
                audioData = AudioProcessor.ProcessAudio(audioData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"音频预处理失败: {ex.Message}");
                throw;
            }
            if (!File.Exists(_modelPath))
            {
                await DownloadModelAsync(ggmlType, _modelPath);
                timeTaken = DateTime.UtcNow - startTime;
                Console.WriteLine($"Time Taken to Download: {timeTaken.TotalSeconds} Seconds");
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