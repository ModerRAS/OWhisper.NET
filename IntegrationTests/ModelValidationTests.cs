using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using OWhisper.Core.Services;

namespace IntegrationTests
{
    [TestFixture]
    public class ModelValidationTests : ApplicationTestBase
    {
        private WhisperManager _whisperManager;
        private IPlatformPathService _pathService;
        private string _testModelsDir;
        private string _testModelPath;

        [OneTimeSetUp]
        public new void SetUp()
        {
            base.SetUp(); // 调用基类的SetUp
            
            _pathService = new PlatformPathService();
            _whisperManager = new WhisperManager(_pathService);
            _testModelsDir = _pathService.GetModelsPath();
            _testModelPath = Path.Combine(_testModelsDir, "ggml-large-v3-turbo.bin");
            
            // 确保测试目录存在
            if (!Directory.Exists(_testModelsDir))
            {
                Directory.CreateDirectory(_testModelsDir);
            }
        }

        [Test]
        public void CheckModelStatus_ShouldReturnInvalidForNonExistentFile()
        {
            // 确保模型文件不存在
            if (File.Exists(_testModelPath))
            {
                File.Delete(_testModelPath);
            }

            var (exists, valid, size, path) = _whisperManager.CheckModelStatus();

            Assert.That(exists, Is.False, "文件不应该存在");
            Assert.That(valid, Is.False, "不存在的文件应该被标记为无效");
            Assert.That(size, Is.EqualTo(0), "不存在文件的大小应该为0");
            Assert.That(path, Is.EqualTo(_testModelPath), "路径应该匹配");
        }

        [Test]
        public void CheckModelStatus_ShouldReturnInvalidForCorruptedFile()
        {
            // 创建一个损坏的模型文件（错误的SHA256）
            var corruptedContent = "这是一个损坏的模型文件，SHA256不匹配";
            File.WriteAllText(_testModelPath, corruptedContent);

            var (exists, valid, size, path) = _whisperManager.CheckModelStatus();

            Assert.That(exists, Is.True, "文件应该存在");
            Assert.That(valid, Is.False, "损坏的文件应该被标记为无效");
            Assert.That(size, Is.GreaterThan(0), "文件大小应该大于0");
            Assert.That(path, Is.EqualTo(_testModelPath), "路径应该匹配");

            // 清理
            File.Delete(_testModelPath);
        }

        [Test]
        public void CheckModelStatus_ShouldReturnInvalidForSmallFile()
        {
            // 创建一个太小的文件（小于10MB）
            var smallContent = new byte[1024]; // 1KB
            File.WriteAllBytes(_testModelPath, smallContent);

            var (exists, valid, size, path) = _whisperManager.CheckModelStatus();

            Assert.That(exists, Is.True, "文件应该存在");
            Assert.That(valid, Is.False, "太小的文件应该被标记为无效");
            Assert.That(size, Is.EqualTo(1024), "文件大小应该为1024字节");
            Assert.That(path, Is.EqualTo(_testModelPath), "路径应该匹配");

            // 清理
            File.Delete(_testModelPath);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _whisperManager?.Dispose();
            
            // 清理测试文件
            if (File.Exists(_testModelPath))
            {
                try
                {
                    File.Delete(_testModelPath);
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        }
    }
} 