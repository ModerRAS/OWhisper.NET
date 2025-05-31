using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OWhisper.NET;
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

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);

            // 验证输出格式为16kHz单声道WAV
            using (var stream = new MemoryStream(result))
            using (var reader = new WaveFileReader(stream)) {
                Assert.AreEqual(16000, reader.WaveFormat.SampleRate);
                Assert.AreEqual(1, reader.WaveFormat.Channels);
                Assert.AreEqual(16, reader.WaveFormat.BitsPerSample);
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
            Assert.AreEqual(initialTempFiles, finalTempFiles);
        }

        public new void Dispose() {
            foreach (var file in _tempFiles.Where(File.Exists)) {
                File.Delete(file);
            }
        }
    }
}