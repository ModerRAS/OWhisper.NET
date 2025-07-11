using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OWhisper.Core.Services;
using NAudio.Wave;

namespace IntegrationTests {
    [TestFixture]
    public class AudioProcessorTests : IDisposable {
        private readonly string _testAudioDir;
        private readonly string TestResourcesDir = Path.Combine(
                Path.GetDirectoryName(typeof(ApplicationTestBase).Assembly.Location),
                "TestResources");
        private readonly List<string> _tempFiles = new();

        public AudioProcessorTests() {
            _testAudioDir = Path.Combine(TestResourcesDir, "sample");
        }

        [Test]
        public void ProcessAudio_ShouldConvertMp3To16kHzWav() {
            var inputFile = Path.Combine(TestResourcesDir, "sample_audio.mp3");
            var audioBytes = File.ReadAllBytes(inputFile);

            var result = AudioProcessor.ProcessAudio(audioBytes);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.GreaterThan(0));

            // 验证输出格式为16kHz单声道WAV
            using (var stream = new MemoryStream(result))
            using (var reader = new WaveFileReader(stream)) {
                Assert.That(reader.WaveFormat.SampleRate, Is.EqualTo(16000));
                Assert.That(reader.WaveFormat.Channels, Is.EqualTo(1));
                Assert.That(reader.WaveFormat.BitsPerSample, Is.EqualTo(16));
            }
        }

        [Test]
        public void ProcessAudio_ShouldThrowOnInvalidFile() {
            var invalidFile = "invalid_file.txt";
            Assert.Catch<FileNotFoundException>(() => {
                var bytes = File.ReadAllBytes(invalidFile);
                AudioProcessor.ProcessAudio(bytes);
            });
        }

        [Test]
        public void ProcessAudio_ShouldCleanUpTemporaryFiles() {
            var inputFile = Path.Combine(TestResourcesDir, "sample_audio.mp3");
            var audioBytes = File.ReadAllBytes(inputFile);

            // 获取处理前的临时文件数量
            var tempDir = Path.GetTempPath();
            var initialTempFiles = Directory.GetFiles(tempDir, "OWhisper_*.tmp").Length;

            // 处理音频
            var result = AudioProcessor.ProcessAudio(audioBytes);

            // 验证临时文件已被清理
            var finalTempFiles = Directory.GetFiles(tempDir, "OWhisper_*.tmp").Length;
            Assert.That(finalTempFiles, Is.EqualTo(initialTempFiles));
        }

        public void Dispose() {
            foreach (var file in _tempFiles.Where(File.Exists)) {
                File.Delete(file);
            }
        }
    }
}