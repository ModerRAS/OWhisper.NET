using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using Serilog;

namespace OWhisper.Core.Services
{
    public static class AudioProcessor
    {
        /// <summary>
        /// 获取音频文件的总时长
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>音频时长</returns>
        public static TimeSpan GetAudioDuration(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("音频文件不存在", filePath);
            }

            try
            {
                using var reader = new AudioFileReader(filePath);
                return reader.TotalTime;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("获取音频时长失败", ex);
            }
        }

        /// <summary>
        /// 使用VAD对音频进行分段处理
        /// </summary>
        /// <param name="audioData">16kHz单声道PCM音频数据</param>
        /// <param name="enableVad">是否启用VAD分段</param>
        /// <param name="vadSettings">VAD配置参数</param>
        /// <returns>音频分段列表，如果不启用VAD则返回包含原始音频的单个分段</returns>
        public static List<AudioSegment> ProcessAudioWithVad(byte[] audioData, bool enableVad = true, VadSettings vadSettings = null)
        {
            // 优先使用 Silero VAD
            try
            {
                return SileroVadAudioProcessor.ProcessAudioWithSileroVad(audioData, enableVad, vadSettings);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Silero VAD 失败，回退到原始VAD实现");
                
                // 回退到原始VAD实现
                return ProcessAudioWithLegacyVad(audioData, enableVad, vadSettings);
            }
        }

        /// <summary>
        /// 使用原始VAD对音频进行分段处理（备用方案）
        /// </summary>
        private static List<AudioSegment> ProcessAudioWithLegacyVad(byte[] audioData, bool enableVad = true, VadSettings vadSettings = null)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("音频数据不能为空");
            }

            var segments = new List<AudioSegment>();

            if (!enableVad)
            {
                // 不使用VAD，返回完整音频作为单个分段
                Log.Information("原始VAD未启用，返回完整音频作为单个分段");
                
                TimeSpan totalDuration;
                try
                {
                    using var stream = new MemoryStream(audioData);
                    using var reader = new WaveFileReader(stream);
                    totalDuration = reader.TotalTime;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "无法获取音频时长，使用默认值");
                    totalDuration = TimeSpan.FromSeconds(60); // 默认值
                }

                segments.Add(new AudioSegment
                {
                    StartTime = TimeSpan.Zero,
                    EndTime = totalDuration,
                    AudioData = audioData,
                    HasSpeech = true,
                    AverageEnergy = 0.5 // 默认能量值
                });

                return segments;
            }

            try
            {
                // 自动选择VAD设置
                if (vadSettings == null)
                {
                    TimeSpan duration;
                    try
                    {
                        using var stream = new MemoryStream(audioData);
                        using var reader = new WaveFileReader(stream);
                        duration = reader.TotalTime;
                    }
                    catch
                    {
                        duration = TimeSpan.FromSeconds(60); // 默认值
                    }
                    
                    vadSettings = VoiceActivityDetector.GetRecommendedSettings(duration.TotalSeconds);
                    Log.Information("使用推荐原始VAD设置 - 音频时长: {Duration}秒, 最大分段: {MaxSegment}秒", 
                        duration.TotalSeconds, vadSettings.MaxSegmentDuration);
                }

                // 使用VAD进行分段
                var vad = new VoiceActivityDetector(vadSettings);
                segments = vad.SegmentAudio(audioData);

                // 过滤掉静音段（如果设置了移除静音段）
                if (vadSettings.RemoveSilentSegments)
                {
                    var speechSegments = segments.Where(s => s.HasSpeech).ToList();
                    Log.Information("原始VAD分段完成 - 总分段: {Total}, 语音分段: {Speech}, 静音分段: {Silent}", 
                        segments.Count, speechSegments.Count, segments.Count - speechSegments.Count);
                    return speechSegments;
                }

                Log.Information("原始VAD分段完成 - 共 {Count} 个分段", segments.Count);
                return segments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "原始VAD处理失败，回退到不使用VAD");
                
                // VAD失败时回退到原始处理方式
                return ProcessAudioWithLegacyVad(audioData, false, null);
            }
        }

        /// <summary>
        /// 将音频数据转换为16kHz单声道Wave格式（带文件名参数）
        /// </summary>
        /// <param name="audioData">原始音频数据</param>
        /// <param name="fileName">音频文件名（用于格式识别）</param>
        /// <returns>处理后的Wave格式音频数据</returns>
        /// <exception cref="ArgumentException">无效音频格式</exception>
        /// <exception cref="InvalidOperationException">转码失败</exception>
        public static byte[] ProcessAudio(byte[] audioData, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return ProcessAudio(audioData);
            }

            var fileExtension = Path.GetExtension(fileName).ToLower();
            try
            {
                using (var inputStream = new MemoryStream(audioData))
                using (var outputStream = new MemoryStream())
                {
                    // 根据文件扩展名优先选择读取器
                    WaveStream reader = fileExtension switch
                    {
                        ".mp3" => TryCreateMp3Reader(inputStream),
                        ".wav" => new WaveFileReader(inputStream),
                        ".aac" => TryCreateMediaFoundationReader(inputStream),
                        ".m4a" => TryCreateMediaFoundationReader(inputStream),
                        _ => TryCreateMediaFoundationReader(inputStream) ?? new WaveFileReader(inputStream)
                    };

                    if (reader == null)
                    {
                        throw new ArgumentException("不支持的音频格式");
                    }

                    // 设置目标格式: 16kHz, 16bit, 单声道
                    var targetFormat = new WaveFormat(16000, 16, 1);

                    // 使用重采样器转换格式
                    using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                    {
                        WaveFileWriter.WriteWavFileToStream(outputStream, resampler);
                        return outputStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("音频处理失败", ex);
            }
        }

        /// <summary>
        /// 将音频数据转换为16kHz单声道Wave格式
        /// </summary>
        /// <param name="audioData">原始音频数据</param>
        /// <returns>处理后的Wave格式音频数据</returns>
        /// <exception cref="ArgumentException">无效音频格式</exception>
        /// <exception cref="InvalidOperationException">转码失败</exception>
        public static byte[] ProcessAudio(byte[] audioData)
        {
            try
            {
                using (var inputStream = new MemoryStream(audioData))
                using (var outputStream = new MemoryStream())
                {
                    // 优先尝试MP3读取器，再尝试其他格式
                    using (var reader = TryCreateMp3Reader(inputStream) ??
                           TryCreateMediaFoundationReader(inputStream) ??
                           new WaveFileReader(inputStream))
                    {
                        if (reader == null)
                        {
                            throw new ArgumentException("不支持的音频格式");
                        }

                        // 设置目标格式: 16kHz, 16bit, 单声道
                        var targetFormat = new WaveFormat(16000, 16, 1);

                        // 使用重采样器转换格式
                        using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                        {
                            WaveFileWriter.WriteWavFileToStream(outputStream, resampler);
                            return outputStream.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("音频处理失败", ex);
            }
        }

        private static WaveStream TryCreateMp3Reader(Stream stream)
        {
            try
            {
                stream.Position = 0;
                return new Mp3FileReader(stream);
            }
            catch
            {
                return null;
            }
        }

        private static WaveStream TryCreateMediaFoundationReader(Stream stream)
        {
            // MediaFoundationReader只支持文件路径，必须使用临时文件方式
            return TryCreateMediaFoundationReaderFromTempFile(stream);
        }

        private static WaveStream TryCreateMediaFoundationReaderFromTempFile(Stream stream)
        {
            string tempFile = null;
            try
            {
                // 创建临时文件
                tempFile = Path.GetTempFileName();
                var tempExtension = ".aac"; // 假设是AAC格式，MediaFoundation会自动检测
                var tempFileWithExt = Path.ChangeExtension(tempFile, tempExtension);

                // 如果扩展名不同，重命名文件
                if (tempFile != tempFileWithExt)
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                    tempFile = tempFileWithExt;
                }

                using (var fileStream = File.Create(tempFile))
                {
                    stream.Position = 0;
                    stream.CopyTo(fileStream);
                }

                // 创建一个会自动清理临时文件的包装读取器
                return new TempFileMediaFoundationReader(tempFile);
            }
            catch
            {
                // 清理临时文件
                if (tempFile != null && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
                return null;
            }
        }

        /// <summary>
        /// 自动清理临时文件的MediaFoundationReader包装类
        /// </summary>
        private class TempFileMediaFoundationReader : WaveStream
        {
            private readonly MediaFoundationReader _reader;
            private readonly string _tempFile;
            private bool _disposed = false;

            public TempFileMediaFoundationReader(string tempFile)
            {
                _tempFile = tempFile;
                _reader = new MediaFoundationReader(tempFile);
            }

            public override WaveFormat WaveFormat => _reader.WaveFormat;
            public override long Length => _reader.Length;
            public override long Position
            {
                get => _reader.Position;
                set => _reader.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _reader.Read(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _reader?.Dispose();
                    }

                    // 清理临时文件
                    if (_tempFile != null && File.Exists(_tempFile))
                    {
                        try
                        {
                            File.Delete(_tempFile);
                        }
                        catch
                        {
                            // 忽略删除错误，临时文件最终会被系统清理
                        }
                    }

                    _disposed = true;
                }
                base.Dispose(disposing);
            }
        }
    }
} 