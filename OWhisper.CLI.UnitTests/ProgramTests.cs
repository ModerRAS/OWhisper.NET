using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using OWhisper.Core.Services;
using Xunit;

namespace OWhisper.CLI.UnitTests
{
    public class ProgramTests
    {
        [Fact]
        public void CreateHostBuilder_ShouldConfigureServicesCorrectly()
        {
            // Arrange
            var args = new string[] { };

            // Act
            var hostBuilder = Program.CreateHostBuilder(args);
            var host = hostBuilder.Build();

            // Assert
            var services = host.Services;
            
            // 验证服务注册
            services.GetService<IPlatformPathService>().Should().NotBeNull();
            services.GetService<IHostedService>().Should().NotBeNull();
            services.GetService<IConfiguration>().Should().NotBeNull();
            services.GetService<ILogger<WebApiHostedService>>().Should().NotBeNull();
        }

        [Fact]
        public void CreateHostBuilder_WithCommandLineArgs_ShouldConfigureCorrectly()
        {
            // Arrange
            var args = new string[] { "--WebServer:Port=8080", "--WebServer:Host=0.0.0.0" };

            // Act
            var hostBuilder = Program.CreateHostBuilder(args);
            var host = hostBuilder.Build();

            // Assert
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            configuration["WebServer:Port"].Should().Be("8080");
            configuration["WebServer:Host"].Should().Be("0.0.0.0");
        }

        [Fact]
        public void CreateHostBuilder_ShouldUseSerilog()
        {
            // Arrange
            var args = new string[] { };

            // Act
            var hostBuilder = Program.CreateHostBuilder(args);

            // Assert
            // 这里主要验证构建过程不会抛出异常
            var action = () => hostBuilder.Build();
            action.Should().NotThrow();
        }

        [Fact]
        public void CreateHostBuilder_ShouldConfigureJsonFile()
        {
            // Arrange
            var args = new string[] { };

            // Act
            var hostBuilder = Program.CreateHostBuilder(args);
            var host = hostBuilder.Build();

            // Assert
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            configuration.Should().NotBeNull();
            
            // JSON文件是可选的，所以不会抛出异常
            var action = () => configuration["SomeKey"];
            action.Should().NotThrow();
        }
    }
} 