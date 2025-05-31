using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using OWhisper.NET;
using Whisper.net.Ggml;

namespace IntegrationTests
{
    [TestFixture]
    public class WhisperManagerDownloadTests : ApplicationTestBase, IDisposable
    {
        private readonly string _testModelDir;
        private readonly WhisperManager _manager;

        public WhisperManagerDownloadTests()
        {
            // 创建专用测试目录
            _testModelDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _manager = new WhisperManager();
        }

        public void Dispose()
        {
            // 测试完成后清理临时目录
            try
            {
                if (Directory.Exists(_testModelDir))
                {
                    Directory.Delete(_testModelDir, true);
                }
            }
            catch { /* 忽略清理错误 */ }
        }

        // [Test]
        // public async Task DownloadModelAsync_ShouldDownloadValidModelFile()
        // {
        //     // 准备
        //     var modelType = GgmlType.LargeV3Turbo;
            
        //     // 执行
        //     await _manager.DownloadModelAsync(modelType, _testModelDir);

        //     // 验证
        //     var modelPath = Path.Combine(_testModelDir, "ggml-large-v3-turbo.bin");
        //     Assert.IsTrue(File.Exists(modelPath), "模型文件应存在");

        //     var fileInfo = new FileInfo(modelPath);
        //     Assert.IsTrue(fileInfo.Length > 10 * 1024 * 1024, "模型文件应大于10MB");
        // }

        // [Test]
        // public async Task DownloadModelAsync_ShouldHandleExistingDirectory()
        // {
        //     // 准备 - 预先创建目录
        //     Directory.CreateDirectory(_testModelDir);
            
        //     // 执行 & 验证 - 不应抛出异常
        //     Assert.DoesNotThrowAsync(() => 
        //         _manager.DownloadModelAsync(GgmlType.LargeV3Turbo, _testModelDir));
        // }

        // [Test]
        // public async Task CheckModelStatus_ShouldDetectDownloadedFile()
        // {
        //     // 准备 - 先下载模型
        //     await _manager.DownloadModelAsync(GgmlType.LargeV3Turbo, _testModelDir);

        //     // 执行
        //     var status = _manager.CheckModelStatus();

        //     // 验证
        //     Assert.IsTrue(status.exists, "应检测到模型文件");
        //     Assert.IsTrue(status.valid, "模型文件应有效");
        //     Assert.IsTrue(status.size > 0, "模型文件大小应大于0");
        // }

        // [Test]
        // public async Task DownloadModelAsync_ShouldOverwriteExistingFile()
        // {
        //     // 准备 - 创建假模型文件
        //     var fakeModelPath = Path.Combine(_testModelDir, "ggml-large-v3-turbo.bin");
        //     Directory.CreateDirectory(_testModelDir);
        //     File.WriteAllText(fakeModelPath, "fake model data");

        //     // 执行 - 下载真实模型
        //     await _manager.DownloadModelAsync(GgmlType.LargeV3Turbo, _testModelDir);

        //     // 验证 - 假文件应被覆盖
        //     var fileInfo = new FileInfo(fakeModelPath);
        //     Assert.IsTrue(fileInfo.Length > 10 * 1024 * 1024, "模型文件应被覆盖为真实文件");
        // }
    }
}