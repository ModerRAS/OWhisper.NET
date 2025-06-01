using FluentAssertions;
using OWhisper.Core.Models;
using Xunit;
using TaskStatus = OWhisper.Core.Models.TaskStatus;

namespace OWhisper.Core.UnitTests.Models
{
    public class ApiResponseTests
    {
        [Fact]
        public void Success_ShouldCreateSuccessfulResponse()
        {
            // Arrange
            var data = "Test data";

            // Act
            var response = ApiResponse<string>.Success(data);

            // Assert
            response.Status.Should().Be("success");
            response.Data.Should().Be(data);
            response.Error.Should().BeNull();
            response.ErrorCode.Should().BeNull();
        }

        [Fact]
        public void CreateError_WithMessageAndCode_ShouldCreateErrorResponse()
        {
            // Arrange
            var message = "Error occurred";
            var errorCode = "E001";

            // Act
            var response = ApiResponse<string>.CreateError(errorCode, message);

            // Assert
            response.Status.Should().Be("error");
            response.Data.Should().BeNull();
            response.Error.Should().Be(message);
            response.ErrorCode.Should().Be(errorCode);
        }

        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            // Arrange
            var status = "success";
            var data = "Test data";
            var error = "Error message";
            var errorCode = "E001";

            // Act
            var response = new ApiResponse<string>
            {
                Status = status,
                Data = data,
                Error = error,
                ErrorCode = errorCode
            };

            // Assert
            response.Status.Should().Be(status);
            response.Data.Should().Be(data);
            response.Error.Should().Be(error);
            response.ErrorCode.Should().Be(errorCode);
        }

        [Fact]
        public void ApiResponse_WithNullData_ShouldBeValid()
        {
            // Act
            var response = ApiResponse<string>.Success(null);

            // Assert
            response.Status.Should().Be("success");
            response.Data.Should().BeNull();
        }

        [Fact]
        public void ApiResponse_WithComplexData_ShouldWork()
        {
            // Arrange
            var complexData = new { Name = "Test", Value = 123 };

            // Act
            var response = ApiResponse<object>.Success(complexData);

            // Assert
            response.Status.Should().Be("success");
            response.Data.Should().Be(complexData);
        }

        [Fact]
        public void TaskCreationResponse_ShouldSetProperties()
        {
            // Arrange
            var taskId = "test-task-id";
            var queuePosition = 5;

            // Act
            var response = new TaskCreationResponse
            {
                TaskId = taskId,
                QueuePosition = queuePosition
            };

            // Assert
            response.TaskId.Should().Be(taskId);
            response.QueuePosition.Should().Be(queuePosition);
        }

        [Fact]
        public void TranscriptionProgress_ShouldSetProperties()
        {
            // Arrange
            var taskId = "test-task-id";
            var status = TaskStatus.Processing;
            var progress = 0.5f;
            var message = "Processing...";

            // Act
            var progressResponse = new TranscriptionProgress
            {
                TaskId = taskId,
                Status = status,
                Progress = progress,
                Message = message
            };

            // Assert
            progressResponse.TaskId.Should().Be(taskId);
            progressResponse.Status.Should().Be(status);
            progressResponse.Progress.Should().Be(progress);
            progressResponse.Message.Should().Be(message);
        }
    }
} 