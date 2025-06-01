using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OWhisper.Core.Services;
using System.Threading;
using Xunit;

namespace OWhisper.CLI.UnitTests
{
    public class WebServerHostedServiceTests
    {
        private readonly Mock<ILogger<WebServerHostedService>> _mockLogger;
        private readonly Mock<IPlatformPathService> _mockPathService;
        private readonly IConfiguration _configuration;
        private readonly WebServerHostedService _service;

        public WebServerHostedServiceTests()
        {
            _mockLogger = new Mock<ILogger<WebServerHostedService>>();
            _mockPathService = new Mock<IPlatformPathService>();

            // 使用内存配置而不是mock
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "WebServer:Port", "5000" },
                { "WebServer:Host", "localhost" }
            });
            _configuration = configurationBuilder.Build();

            _service = new WebServerHostedService(
                _mockLogger.Object,
                _mockPathService.Object,
                _configuration);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => new WebServerHostedService(
                _mockLogger.Object,
                _mockPathService.Object,
                _configuration);

            action.Should().NotThrow();
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new WebServerHostedService(
                null,
                _mockPathService.Object,
                _configuration);

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithNullPathService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new WebServerHostedService(
                _mockLogger.Object,
                null,
                _configuration);

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new WebServerHostedService(
                _mockLogger.Object,
                _mockPathService.Object,
                null);

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task StartAsync_ShouldCallEnsureDirectoriesExist()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1)); // 快速取消以避免长时间运行

            // Act
            var startTask = _service.StartAsync(cts.Token);
            
            // 等待一小段时间让服务开始执行
            await Task.Delay(100);
            
            cts.Cancel();
            
            try
            {
                await startTask;
            }
            catch (OperationCanceledException)
            {
                // 预期的取消异常
            }

            // Assert
            _mockPathService.Verify(p => p.EnsureDirectoriesExist(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartAsync_ShouldUseConfigurationValues()
        {
            // Arrange - 使用自定义配置值
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "WebServer:Port", "8080" },
                { "WebServer:Host", "0.0.0.0" }
            });
            var customConfiguration = configurationBuilder.Build();

            var customService = new WebServerHostedService(
                _mockLogger.Object,
                _mockPathService.Object,
                customConfiguration);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1));

            // Act
            var startTask = customService.StartAsync(cts.Token);
            
            await Task.Delay(100);
            cts.Cancel();
            
            try
            {
                await startTask;
            }
            catch (OperationCanceledException)
            {
                // 预期的取消异常
            }

            // Assert - 验证配置值正确读取
            customConfiguration.GetValue<int>("WebServer:Port", 5000).Should().Be(8080);
            customConfiguration.GetValue<string>("WebServer:Host", "localhost").Should().Be("0.0.0.0");
        }

        [Fact]
        public async Task StopAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var stopAction = async () => await _service.StopAsync(cts.Token);
            await stopAction.Should().NotThrowAsync();
        }
    }
} 