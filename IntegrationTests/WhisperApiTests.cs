using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using IntegrationTests.Models;
using NUnit.Framework;

namespace IntegrationTests
{
    [TestFixture]
    public class WhisperApiTests : ApplicationTestBase
    {
        [Test]
        public async Task GetStatus_ShouldReturnServiceStatus()
        {
            var response = await Client.GetAsync("/api/status");
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.IsNotNull(result?.Status);
            Assert.IsNotNull(result?.Data);
        }

        [Test]
        public async Task Transcribe_ShouldProcessAudioFile()
        {
            var audioFile = Path.Combine(TestResourcesDir, "sample_audio.wav");
            var audioBytes = File.ReadAllBytes(audioFile);
            
            using var content = new ByteArrayContent(audioBytes);
            var response = await Client.PostAsync("/api/transcribe", content);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TranscriptionResult>>();
            Assert.AreEqual("success", result?.Status);
            Assert.IsNotNull(result?.Data?.Text);
            Assert.IsTrue(result?.Data?.ProcessingTime > 0);
        }

        [Test]
        public async Task Transcribe_ShouldFailWithInvalidAudio()
        {
            var invalidBytes = new byte[] { 0x00, 0x01, 0x02 };
            using var content = new ByteArrayContent(invalidBytes);
            
            var response = await Client.PostAsync("/api/transcribe", content);
            Assert.IsTrue(response.IsSuccessStatusCode);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.AreEqual("error", result?.Status);
            Assert.IsNotNull(result?.Error);
        }
        
        [Test]
        public void TestCleanup_ShouldNotLeaveRunningProcesses()
        {
            // 这个测试应该在所有其他测试之后运行
            Console.WriteLine("=== 进程清理测试开始 ===");
            
            // 执行一个API调用以确保进程被创建
            var response = Client.GetAsync("/api/status").Result;
            Assert.IsTrue(response.IsSuccessStatusCode, "API调用失败");
            
            // 检查是否有OWhisper.NET进程残留
            var processes = Process.GetProcessesByName("OWhisper.NET");
            
            if (processes.Length > 0)
            {
                Console.WriteLine($"发现 {processes.Length} 个残留进程:");
                foreach (var p in processes)
                {
                    try
                    {
                        Console.WriteLine($"- PID: {p.Id}, 名称: {p.ProcessName}, 启动时间: {p.StartTime}, 内存使用: {p.WorkingSet64/1024}KB");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"- PID: {p.Id}, 获取进程信息失败: {ex.Message}");
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            else
            {
                Console.WriteLine("未发现残留进程");
            }
            
            Assert.IsEmpty(processes, $"发现 {processes.Length} 个残留的OWhisper.NET进程");
            
            Console.WriteLine("=== 进程清理测试结束 ===");
        }
    }
}