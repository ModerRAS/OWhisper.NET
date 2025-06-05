using System;
using OWhisper.Core.Models;
using OWhisper.Core.Services;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Whisper.net;
using Whisper.net.Ggml;
using Serilog;

namespace OWhisper.Core.Services
{
    public sealed class WhisperService : IDisposable
    {
        private static readonly Lazy<WhisperService> _instance =
            new Lazy<WhisperService>(() => new WhisperService());

        public static WhisperService Instance => _instance.Value;

        private readonly IPlatformPathService _platformPathService;
        private readonly WhisperManager _whisperManager;

        public enum ServiceStatus
        {
            Stopped,
            Starting,
            Running,
            Stopping
        }

        private ServiceStatus _status = ServiceStatus.Stopped;
        private readonly object _lock = new object();

        public event EventHandler<ServiceStatus> StatusChanged;
        public event EventHandler<float> ProgressChanged;

        private WhisperService()
        {
            _platformPathService = new PlatformPathService();
            _whisperManager = new WhisperManager(_platformPathService);
        }

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

        private bool IsValidAac(byte[] audioData)
        {
            try
            {
                // AAC文件头检查: 常见的AAC格式标识
                // ADTS格式: 0xFF 0xF1 或 0xFF 0xF9
                // M4A格式: "ftyp" 在偏移4处
                return audioData != null &&
                       audioData.Length > 8 &&
                       (
                           // ADTS AAC
                           (audioData[0] == 0xFF && (audioData[1] == 0xF1 || audioData[1] == 0xF9)) ||
                           // M4A/MP4 AAC - 检查 'ftyp' 标识
                           (audioData.Length > 8 &&
                            audioData[4] == 'f' && audioData[5] == 't' &&
                            audioData[6] == 'y' && audioData[7] == 'p')
                       );
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

                Log.Information("服务状态变更: {OldStatus} -> {NewStatus}", _status, ServiceStatus.Starting);
                _status = ServiceStatus.Starting;
                StatusChanged?.Invoke(this, _status);
                Log.Information("启动服务线程...");

            }
        }

        /// <summary>
        /// 转录音频数据为文本
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <param name="enableVad">是否启用VAD分段处理</param>
        /// <param name="vadSettings">VAD配置参数</param>
        /// <returns>转写文本</returns>
        public async Task<TranscriptionResult> Transcribe(byte[] audioData, bool enableVad = true, VadSettings vadSettings = null)
        {
            // 前置音频验证
            Log.Information("验证音频数据格式...");
            bool isMp3 = IsValidMp3(audioData);
            bool isWav = IsValidWav(audioData);
            bool isAacOrM4a = IsValidAac(audioData);
            Log.Information("音频格式检测 - MP3: {IsMp3}, WAV: {IsWav}, AAC/M4A: {IsAacOrM4a}", isMp3, isWav, isAacOrM4a);

            if (!isMp3 && !isWav && !isAacOrM4a)
            {
                Log.Error("不支持的音频格式");
                throw new AudioProcessingException("INVALID_AUDIO_FORMAT", "不支持的音频格式");
            }

            // 创建临时文件以获取音频时长
            string tempFilePath = Path.GetTempFileName();
            double totalMs = 0;
            try
            {
                File.WriteAllBytes(tempFilePath, audioData);
                TimeSpan totalDuration = AudioProcessor.GetAudioDuration(tempFilePath);
                totalMs = totalDuration.TotalMilliseconds;
                Log.Information("音频总时长: {TotalMs}ms, VAD启用: {VadEnabled}", totalMs, enableVad);

                var startTime = DateTime.UtcNow;

                // 使用VAD进行音频分段处理
                if (enableVad && totalMs > 30000) // 大于30秒的音频才使用VAD
                {
                    Log.Information("使用VAD进行音频分段处理");
                    return await TranscribeWithVad(audioData, vadSettings, _whisperManager, totalMs, startTime);
                }
                else
                {
                    Log.Information("不使用VAD，直接进行转录");
                    var (srtContent, plainText) = await _whisperManager.Transcribe(audioData, (progress) =>
                    {
                        // 触发进度事件
                        ProgressChanged?.Invoke(this, progress);
                    }, totalMs);

                    return new TranscriptionResult
                    {
                        Success = true,
                        Text = plainText,
                        SrtContent = srtContent,
                        ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "转写过程中发生错误");
                throw;
            }
            finally
            {
                // 删除临时文件
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "删除临时文件失败");
                }
            }
        }

        /// <summary>
        /// 转录音频数据为文本 (向后兼容方法)
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <returns>转写文本</returns>
        public async Task<TranscriptionResult> Transcribe(byte[] audioData)
        {
            return await Transcribe(audioData, true, null);
        }

        /// <summary>
        /// 使用VAD分段进行转录
        /// </summary>
        private async Task<TranscriptionResult> TranscribeWithVad(byte[] audioData, VadSettings vadSettings, 
            WhisperManager whisperManager, double totalMs, DateTime startTime)
        {
            try
            {
                // 使用VAD对音频进行分段
                var segments = AudioProcessor.ProcessAudioWithVad(audioData, true, vadSettings);
                
                if (segments == null || segments.Count == 0)
                {
                    Log.Warning("VAD未产生任何分段，回退到完整音频转录");
                    var (srtContent, plainText) = await whisperManager.Transcribe(audioData, (progress) =>
                    {
                        ProgressChanged?.Invoke(this, progress);
                    }, totalMs);

                    return new TranscriptionResult
                    {
                        Success = true,
                        Text = plainText,
                        SrtContent = srtContent,
                        ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                    };
                }

                Log.Information("开始分段转录，共 {Count} 个分段", segments.Count);
                
                var allSrtSegments = new List<string>();
                var allPlainTextSegments = new List<string>();
                var totalProcessedDuration = 0.0;
                
                for (int i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];
                    Log.Information("正在处理分段 {Index}/{Total}: {Segment}", i + 1, segments.Count, segment);
                    
                    try
                    {
                        // 转录当前分段
                        var (segmentSrt, segmentText) = await whisperManager.Transcribe(segment.AudioData, null, 0);
                        
                        if (!string.IsNullOrWhiteSpace(segmentText))
                        {
                            // 调整SRT时间戳
                            Log.Information("分段 {Index} 原始时间: {StartTime} - {EndTime}", i + 1, segment.StartTime, segment.EndTime);
                            Log.Information("分段 {Index} 原始SRT内容:\n{SrtContent}", i + 1, segmentSrt);
                            
                            var adjustedSrt = AdjustSrtTimestamps(segmentSrt, segment.StartTime, allSrtSegments.Count);
                            
                            Log.Information("分段 {Index} 调整后SRT内容:\n{AdjustedSrt}", i + 1, string.Join("\n", adjustedSrt));
                            
                            allSrtSegments.AddRange(adjustedSrt);
                            allPlainTextSegments.Add(segmentText.Trim());
                        }
                        
                        // 更新总体进度
                        totalProcessedDuration += segment.Duration.TotalMilliseconds;
                        var overallProgress = (float)(totalProcessedDuration / totalMs * 100);
                        ProgressChanged?.Invoke(this, overallProgress);
                        
                        Log.Information("分段 {Index} 转录完成，文本长度: {Length}", i + 1, segmentText?.Length ?? 0);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "分段 {Index} 转录失败: {Segment}", i + 1, segment);
                        // 继续处理下一个分段
                    }
                }
                
                // 合并结果
                var finalSrtContent = string.Join("\n", allSrtSegments);
                var finalPlainText = string.Join(" ", allPlainTextSegments);
                
                Log.Information("VAD分段转录完成 - 总分段: {SegmentCount}, 有效文本分段: {TextSegmentCount}", 
                    segments.Count, allPlainTextSegments.Count);
                
                return new TranscriptionResult
                {
                    Success = true,
                    Text = finalPlainText,
                    SrtContent = finalSrtContent,
                    ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "VAD分段转录失败，回退到完整音频转录");
                
                // 回退到完整音频转录
                var (srtContent, plainText) = await whisperManager.Transcribe(audioData, (progress) =>
                {
                    ProgressChanged?.Invoke(this, progress);
                }, totalMs);

                return new TranscriptionResult
                {
                    Success = true,
                    Text = plainText,
                    SrtContent = srtContent,
                    ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                };
            }
        }

        /// <summary>
        /// 调整SRT时间戳以反映分段在原始音频中的实际位置
        /// </summary>
        private List<string> AdjustSrtTimestamps(string srtContent, TimeSpan segmentStartTime, int startId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(srtContent)) 
            {
                Log.Warning("SRT内容为空，无法调整时间戳");
                return result;
            }
            
            Log.Information("开始调整SRT时间戳 - 分段开始时间: {SegmentStartTime}, 起始ID: {StartId}", segmentStartTime, startId);
            Log.Information("原始SRT内容长度: {Length}字符", srtContent.Length);
            
            var lines = srtContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var currentId = startId + 1;
            
            Log.Information("分割后共 {Count} 行", lines.Length);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // 检查是否是序号行
                if (int.TryParse(line, out _))
                {
                    result.Add(currentId.ToString());
                    Log.Information("调整序号: {OriginalId} -> {NewId}", line, currentId);
                    currentId++;
                }
                // 检查是否是时间戳行
                else if (line.Contains("-->"))
                {
                    var adjustedTimestamp = AdjustTimestamp(line, segmentStartTime);
                    result.Add(adjustedTimestamp);
                    Log.Information("调整时间戳: {Original} -> {Adjusted}", line, adjustedTimestamp);
                }
                // 其他行（文本内容）
                else
                {
                    result.Add(line);
                    Log.Information("保持文本行: {Text}", line);
                }
                
                // 在每个SRT条目后添加空行
                if (i < lines.Length - 1 && !string.IsNullOrWhiteSpace(line) && 
                    (i + 1 >= lines.Length || int.TryParse(lines[i + 1].Trim(), out _)))
                {
                    result.Add("");
                }
            }
            
            Log.Information("时间戳调整完成，共生成 {Count} 行", result.Count);
            return result;
        }

        /// <summary>
        /// 调整单个时间戳行
        /// </summary>
        private string AdjustTimestamp(string timestampLine, TimeSpan offset)
        {
            try
            {
                var parts = timestampLine.Split(new[] { " --> " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) return timestampLine;
                
                var startTime = ParseSrtTimestamp(parts[0].Trim()) + offset;
                var endTime = ParseSrtTimestamp(parts[1].Trim()) + offset;
                
                return $"{FormatSrtTimestamp(startTime)} --> {FormatSrtTimestamp(endTime)}";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "调整时间戳失败: {TimestampLine}", timestampLine);
                return timestampLine;
            }
        }

        /// <summary>
        /// 解析SRT时间戳
        /// </summary>
        private TimeSpan ParseSrtTimestamp(string timestamp)
        {
            try
            {
                Log.Information("解析时间戳: {Timestamp}", timestamp);
                
                // 支持多种格式: hh:mm:ss,fff 或 hh:mm:ss.fff
                timestamp = timestamp.Replace('.', ','); // 统一使用逗号分隔符
                
                var parts = timestamp.Split(':');
                if (parts.Length != 3) 
                {
                    Log.Error("时间戳格式错误，应为 hh:mm:ss,fff 格式: {Timestamp}", timestamp);
                    throw new FormatException($"无效时间戳格式: {timestamp}");
                }
                
                var hours = int.Parse(parts[0]);
                var minutes = int.Parse(parts[1]);
                var secondsParts = parts[2].Split(',');
                var seconds = int.Parse(secondsParts[0]);
                var milliseconds = 0;
                
                if (secondsParts.Length > 1)
                {
                    var msString = secondsParts[1].PadRight(3, '0').Substring(0, 3); // 确保3位数
                    milliseconds = int.Parse(msString);
                }
                
                var result = new TimeSpan(0, hours, minutes, seconds, milliseconds);
                Log.Information("时间戳解析结果: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "解析时间戳失败: {Timestamp}", timestamp);
                throw;
            }
        }

        /// <summary>
        /// 格式化SRT时间戳
        /// </summary>
        private string FormatSrtTimestamp(TimeSpan timeSpan)
        {
            return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2},{timeSpan.Milliseconds:D3}";
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public void Stop()
        {
            lock (_lock)
            {
                if (_status != ServiceStatus.Running)
                {
                    Log.Information("服务状态为{Status}，无需停止", _status);
                    return;
                }

                _status = ServiceStatus.Stopping;
                StatusChanged?.Invoke(this, _status);
                Log.Information("正在停止服务线程...");

                _cts.Cancel();
                _status = ServiceStatus.Stopped;
                StatusChanged?.Invoke(this, _status);
                Log.Information("服务已完全停止");
            }
        }

        public void Dispose()
        {
            try
            {
                Stop();
                _cts?.Dispose();
                _whisperManager?.Dispose();
            }
            catch
            {
                // 记录错误到日志系统
                throw;
            }

            GC.SuppressFinalize(this);
        }
    }
} 