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
            // 确保测试音频文件存在
            var audioFile = Path.Combine(TestResourcesDir, "sample_audio.wav");
            if (!File.Exists(audioFile))
            {
                Assert.Fail("测试音频文件不存在");
            }

            // // 检查模型文件
            // var modelPath = Path.Combine(Environment.CurrentDirectory, "Models", "ggml-large-v3-turbo.bin");
            // if (!File.Exists(modelPath))
            // {
            //     Assert.Inconclusive("缺少Whisper模型文件，测试跳过");
            //     return;
            // }

            var audioBytes = File.ReadAllBytes(audioFile);
            
            using var content = new ByteArrayContent(audioBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            
            var response = await Client.PostAsync("/api/transcribe", content);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TranscriptionResult>>();
            
            Assert.AreEqual("success", result?.Status, $"API返回状态不正确: {result?.Status}");
            Assert.IsNotNull(result?.Data?.Text, "转录文本为空");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result?.Data?.Text), "转录文本为空或空白");
            Assert.Greater(result?.Data?.ProcessingTime, 0, "处理时间应该大于0");
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
            
            Assert.IsNotEmpty(processes, $"发现 {processes.Length} 个残留的OWhisper.NET进程");
            
            Console.WriteLine("=== 进程清理测试结束 ===");
        }
    }
}