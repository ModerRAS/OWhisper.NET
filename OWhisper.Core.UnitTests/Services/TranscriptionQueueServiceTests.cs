using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OWhisper.Core.Models;
using OWhisper.Core.Services;
using Xunit;
using TaskStatus = OWhisper.Core.Models.TaskStatus;

namespace OWhisper.Core.UnitTests.Services
{
    public class TranscriptionQueueServiceTests : IDisposable
    {
        private readonly ITranscriptionQueueService _service;

        public TranscriptionQueueServiceTests()
        {
            _service = TranscriptionQueueService.Instance;
            // 确保服务处于干净状态
            _service.Stop();
        }

        [Fact]
        public void EnqueueTask_WithValidData_ShouldReturnTaskId()
        {
            // Arrange
            var audioData = new byte[] { 0x01, 0x02, 0x03 };
            var fileName = "test.wav";

            // Act
            var taskId = _service.EnqueueTask(audioData, fileName);

            // Assert
            taskId.Should().NotBeNullOrEmpty();
            _service.GetQueueLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public void EnqueueTask_WithNullAudioData_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _service.EnqueueTask(null, "test.wav"));
        }

        [Fact]
        public void EnqueueTask_WithEmptyFileName_ShouldThrowArgumentException()
        {
            // Arrange
            var audioData = new byte[] { 0x01, 0x02, 0x03 };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.EnqueueTask(audioData, ""));
        }

        [Fact]
        public void GetTask_WithValidTaskId_ShouldReturnTask()
        {
            // Arrange
            var audioData = new byte[] { 0x01, 0x02, 0x03 };
            var fileName = "test.wav";
            var taskId = _service.EnqueueTask(audioData, fileName);

            // Act
            var task = _service.GetTask(taskId);

            // Assert
            task.Should().NotBeNull();
            task.Id.Should().Be(taskId);
            task.FileName.Should().Be(fileName);
        }

        [Fact]
        public void GetTask_WithInvalidTaskId_ShouldReturnNull()
        {
            // Act
            var task = _service.GetTask("invalid-task-id");

            // Assert
            task.Should().BeNull();
        }

        [Fact]
        public void GetAllTasks_ShouldReturnTasks()
        {
            // Act
            var tasks = _service.GetAllTasks();

            // Assert
            tasks.Should().NotBeNull();
        }

        [Fact]
        public void GetAllTasks_WithMultipleTasks_ShouldReturnAllTasks()
        {
            // Arrange
            var audioData = new byte[] { 0x01, 0x02, 0x03 };
            var taskId1 = _service.EnqueueTask(audioData, "test1.wav");
            var taskId2 = _service.EnqueueTask(audioData, "test2.wav");

            // Act
            var tasks = _service.GetAllTasks();

            // Assert
            tasks.Should().Contain(t => t.Id == taskId1);
            tasks.Should().Contain(t => t.Id == taskId2);
        }

        [Fact]
        public void CancelTask_WithValidTaskId_ShouldReturnTrue()
        {
            // Arrange
            var audioData = new byte[] { 0x01, 0x02, 0x03 };
            var taskId = _service.EnqueueTask(audioData, "test.wav");

            // Act
            var result = _service.CancelTask(taskId);

            // Assert
            result.Should().BeTrue();
            var task = _service.GetTask(taskId);
            task.Status.Should().Be(TaskStatus.Cancelled);
        }

        [Fact]
        public void CancelTask_WithInvalidTaskId_ShouldReturnFalse()
        {
            // Act
            var result = _service.CancelTask("invalid-task-id");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetQueueLength_ShouldReturnNonNegativeValue()
        {
            // Act
            var length = _service.GetQueueLength();

            // Assert
            length.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void Start_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => _service.Start();
            action.Should().NotThrow();
        }

        [Fact]
        public void Stop_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => _service.Stop();
            action.Should().NotThrow();
        }

        [Fact]
        public void ProgressUpdated_Event_ShouldBeTriggerable()
        {
            // Arrange
            TranscriptionTask? receivedTask = null;
            _service.ProgressUpdated += (sender, task) => receivedTask = task;

            var audioData = new byte[] { 0x01, 0x02, 0x03 };
            var taskId = _service.EnqueueTask(audioData, "test.wav");

            // Act - 取消任务会触发进度更新事件
            _service.CancelTask(taskId);

            // Assert
            receivedTask.Should().NotBeNull();
            receivedTask!.Id.Should().Be(taskId);
        }

        public void Dispose()
        {
            _service?.Stop();
        }
    }
} 