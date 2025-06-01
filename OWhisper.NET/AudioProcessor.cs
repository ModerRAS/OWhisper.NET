using System;
using System.IO;
using NAudio.Wave;

namespace OWhisper.NET {
    public static class AudioProcessor {
        /// <summary>
        /// 将音频数据转换为16kHz单声道Wave格式（带文件名参数）
        /// </summary>
        /// <param name="audioData">原始音频数据</param>
        /// <param name="fileName">音频文件名（用于格式识别）</param>
        /// <returns>处理后的Wave格式音频数据</returns>
        /// <exception cref="ArgumentException">无效音频格式</exception>
        /// <exception cref="InvalidOperationException">转码失败</exception>
        public static byte[] ProcessAudio(byte[] audioData, string fileName) {
            if (string.IsNullOrEmpty(fileName)) {
                return ProcessAudio(audioData);
            }

            var fileExtension = Path.GetExtension(fileName).ToLower();
            try {
                using (var inputStream = new MemoryStream(audioData))
                using (var outputStream = new MemoryStream()) {
                    // 根据文件扩展名优先选择读取器
                    WaveStream reader = fileExtension switch {
                        ".mp3" => TryCreateMp3Reader(inputStream),
                        ".wav" => new WaveFileReader(inputStream),
                        ".aac" => TryCreateMediaFoundationReader(inputStream),
                        _ => TryCreateMediaFoundationReader(inputStream) ?? new WaveFileReader(inputStream)
                    };

                    if (reader == null) {
                        throw new ArgumentException("不支持的音频格式");
                    }

                    // 设置目标格式: 16kHz, 16bit, 单声道
                    var targetFormat = new WaveFormat(16000, 16, 1);

                    // 使用重采样器转换格式
                    using (var resampler = new MediaFoundationResampler(reader, targetFormat)) {
                        WaveFileWriter.WriteWavFileToStream(outputStream, resampler);
                        return outputStream.ToArray();
                    }
                }
            } catch (Exception ex) {
                throw new InvalidOperationException("音频处理失败", ex);
            }
        }

        /// <summary>
        /// 从文件读取音频并转换为16kHz单声道Wave格式
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>处理后的Wave格式音频数据</returns>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="ArgumentException">无效音频格式</exception>
        /// <exception cref="InvalidOperationException">转码失败</exception>
        public static byte[] ProcessAudioFromFile(string filePath) {
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException("音频文件不存在", filePath);
            }

            var fileExtension = Path.GetExtension(filePath).ToLower();
            if (string.IsNullOrEmpty(fileExtension) ||
                !( fileExtension == ".mp3" || fileExtension == ".wav" || fileExtension == ".aac" )) {
                throw new ArgumentException("不支持的音频文件格式");
            }

            try {
                var audioData = File.ReadAllBytes(filePath);
                return ProcessAudio(audioData);
            } catch (Exception ex) {
                throw new InvalidOperationException("音频文件处理失败", ex);
            }
        }

        /// <summary>
        /// 将音频数据转换为16kHz单声道Wave格式
        /// </summary>
        /// <param name="audioData">原始音频数据</param>
        /// <returns>处理后的Wave格式音频数据</returns>
        /// <exception cref="ArgumentException">无效音频格式</exception>
        /// <exception cref="InvalidOperationException">转码失败</exception>
        public static byte[] ProcessAudio(byte[] audioData) {
            try {
                using (var inputStream = new MemoryStream(audioData))
                using (var outputStream = new MemoryStream()) {
                    // 优先尝试MP3读取器，再尝试其他格式
                    using (var reader = TryCreateMp3Reader(inputStream) ??
                           TryCreateMediaFoundationReader(inputStream) ??
                           new WaveFileReader(inputStream)) {
                        if (reader == null) {
                            throw new ArgumentException("不支持的音频格式");
                        }

                        // 设置目标格式: 16kHz, 16bit, 单声道
                        var targetFormat = new WaveFormat(16000, 16, 1);

                        // 使用重采样器转换格式
                        using (var resampler = new MediaFoundationResampler(reader, targetFormat)) {
                            WaveFileWriter.WriteWavFileToStream(outputStream, resampler);
                            return outputStream.ToArray();
                        }
                    }
                }
            } catch (Exception ex) {
                throw new InvalidOperationException("音频处理失败", ex);
            }
        }

        private static WaveStream TryCreateMediaFoundationReader(Stream stream) {
            // MediaFoundationReader只支持文件路径，必须使用临时文件方式
            return TryCreateMediaFoundationReaderFromTempFile(stream);
        }

        private static WaveStream TryCreateMediaFoundationReaderFromTempFile(Stream stream) {
            string tempFile = null;
            try {
                // 创建临时文件
                tempFile = Path.GetTempFileName();
                var tempExtension = ".aac"; // 假设是AAC格式，MediaFoundation会自动检测
                var tempFileWithExt = Path.ChangeExtension(tempFile, tempExtension);
                
                // 如果扩展名不同，重命名文件
                if (tempFile != tempFileWithExt) {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                    tempFile = tempFileWithExt;
                }

                using (var fileStream = File.Create(tempFile)) {
                    stream.Position = 0;
                    stream.CopyTo(fileStream);
                }

                // 创建一个会自动清理临时文件的包装读取器
                return new TempFileMediaFoundationReader(tempFile);
            } catch {
                // 清理临时文件
                if (tempFile != null && File.Exists(tempFile)) {
                    try { File.Delete(tempFile); } catch { }
                }
                return null;
            }
        }

        /// <summary>
        /// 自动清理临时文件的MediaFoundationReader包装类
        /// </summary>
        private class TempFileMediaFoundationReader : WaveStream {
            private readonly MediaFoundationReader _reader;
            private readonly string _tempFile;
            private bool _disposed = false;

            public TempFileMediaFoundationReader(string tempFile) {
                _tempFile = tempFile;
                _reader = new MediaFoundationReader(tempFile);
            }

            public override WaveFormat WaveFormat => _reader.WaveFormat;
            public override long Length => _reader.Length;
            public override long Position { 
                get => _reader.Position; 
                set => _reader.Position = value; 
            }

            public override int Read(byte[] buffer, int offset, int count) {
                return _reader.Read(buffer, offset, count);
            }

            protected override void Dispose(bool disposing) {
                if (!_disposed) {
                    if (disposing) {
                        _reader?.Dispose();
                    }
                    
                    // 清理临时文件
                    if (_tempFile != null && File.Exists(_tempFile)) {
                        try {
                            File.Delete(_tempFile);
                        } catch {
                            // 忽略删除错误，临时文件最终会被系统清理
                        }
                    }
                    
                    _disposed = true;
                }
                base.Dispose(disposing);
            }
        }

        private static WaveStream TryCreateMp3Reader(Stream stream) {
            try {
                stream.Position = 0;
                return new Mp3FileReader(stream);
            } catch {
                return null;
            }
        }
        /// <summary>
        /// 获取音频文件的总时长
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>音频时长</returns>
        public static TimeSpan GetAudioDuration(string filePath) {
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException("音频文件不存在", filePath);
            }
            
            try {
                using var reader = new AudioFileReader(filePath);
                return reader.TotalTime;
            } catch (Exception ex) {
                throw new InvalidOperationException("获取音频时长失败", ex);
            }
        }
    }
}
