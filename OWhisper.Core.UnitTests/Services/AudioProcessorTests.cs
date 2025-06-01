using FluentAssertions;
using OWhisper.Core.Services;
using System.IO;
using Xunit;

namespace OWhisper.Core.UnitTests.Services
{
    public class AudioProcessorTests
    {
        [Fact]
        public void GetAudioDuration_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var nonExistentFile = "non_existent_file.wav";

            // Act & Assert
            var exception = Assert.Throws<FileNotFoundException>(() => AudioProcessor.GetAudioDuration(nonExistentFile));
            exception.Message.Should().Contain("音频文件不存在");
        }

        [Fact]
        public void GetAudioDuration_WithInvalidFile_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "This is not an audio file");

            try
            {
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => AudioProcessor.GetAudioDuration(tempFile));
                exception.Message.Should().Contain("获取音频时长失败");
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ProcessAudio_WithNullData_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => AudioProcessor.ProcessAudio(null));
            exception.Message.Should().Contain("音频处理失败");
            exception.InnerException.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void ProcessAudio_WithEmptyData_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var emptyData = new byte[0];

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => AudioProcessor.ProcessAudio(emptyData));
            exception.Message.Should().Contain("音频处理失败");
        }

        [Fact]
        public void ProcessAudio_WithInvalidAudioData_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var invalidData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => AudioProcessor.ProcessAudio(invalidData));
            exception.Message.Should().Contain("音频处理失败");
        }

        [Fact]
        public void ProcessAudio_WithFileName_WithEmptyFileName_ShouldCallOverloadWithoutFileName()
        {
            // Arrange
            var invalidData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => AudioProcessor.ProcessAudio(invalidData, ""));
            exception.Message.Should().Contain("音频处理失败");
        }

        [Theory]
        [InlineData(".mp3")]
        [InlineData(".wav")]
        [InlineData(".aac")]
        [InlineData(".m4a")]
        public void ProcessAudio_WithDifferentFileExtensions_ShouldAttemptProcessing(string extension)
        {
            // Arrange
            var invalidData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var fileName = $"test{extension}";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => AudioProcessor.ProcessAudio(invalidData, fileName));
            exception.Message.Should().Contain("音频处理失败");
        }
    }
} 