using System;
using System.IO;
using NAudio.Wave;

namespace OWhisper.NET
{
    public static class AudioProcessor
    {
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

        private static WaveStream TryCreateMediaFoundationReader(Stream stream)
        {
            string tempFile = null;
            try
            {
                // 临时文件方式处理流
                tempFile = Path.GetTempFileName();
                using (var fileStream = File.Create(tempFile))
                {
                    stream.Position = 0;
                    stream.CopyTo(fileStream);
                }
                return new MediaFoundationReader(tempFile);
            }
            catch
            {
                if (tempFile != null && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
                return null;
            }
            finally
            {
                // 注意: 这里不能删除文件，因为MediaFoundationReader还需要访问它
                // 文件删除将由调用方在适当时候处理
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
    }
}