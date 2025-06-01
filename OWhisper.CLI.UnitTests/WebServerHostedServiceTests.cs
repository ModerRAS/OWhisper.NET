using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OWhisper.Core.Services;
using OWhisper.CLI;
using System.Threading;
using Xunit;

namespace OWhisper.CLI.UnitTests
{
    public class WebApiHostedServiceTests
    {
        private readonly Mock<ILogger<WebApiHostedService>> _mockLogger;
        private readonly Mock<IWebApiService> _mockWebApiService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly WebApiHostedService _service;

        public WebApiHostedServiceTests()
        {
            _mockLogger = new Mock<ILogger<WebApiHostedService>>();
            _mockWebApiService = new Mock<IWebApiService>();
            _mockConfiguration = new Mock<IConfiguration>();
            
            _service = new WebApiHostedService(
                _mockLogger.Object,
                _mockWebApiService.Object,
                _mockConfiguration.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var action = () => new WebApiHostedService(
                null!,
                _mockWebApiService.Object,
                _mockConfiguration.Object);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithNullWebApiService_ThrowsArgumentNullException()
        {
            var action = () => new WebApiHostedService(
                _mockLogger.Object,
                null!,
                _mockConfiguration.Object);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("webApiService");
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            var action = () => new WebApiHostedService(
                _mockLogger.Object,
                _mockWebApiService.Object,
                null!);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("configuration");
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var action = () => new WebApiHostedService(
                _mockLogger.Object,
                _mockWebApiService.Object,
                _mockConfiguration.Object);

            action.Should().NotThrow();
        }

        [Fact]
        public async Task ExecuteAsync_StartsWebApiService()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel(); // 立即取消以避免无限循环

            // Act
            await _service.StartAsync(cancellationTokenSource.Token);

            // Assert
            _mockWebApiService.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopAsync_StopsWebApiService()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            await _service.StopAsync(cancellationTokenSource.Token);

            // Assert
            _mockWebApiService.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Dispose_DisposesWebApiService()
        {
            // Act
            _service.Dispose();

            // Assert
            _mockWebApiService.Verify(s => s.Dispose(), Times.Once);
        }
    }
} 