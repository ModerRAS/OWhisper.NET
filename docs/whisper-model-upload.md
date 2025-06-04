# Whisper 模型自动下载和上传工具

这个 GitHub Action workflow 可以自动下载 Whisper 模型并上传到 Cloudflare R2 存储。

## 功能特性

- 🔄 **手动触发**: 支持通过 GitHub UI 手动触发
- 📥 **智能下载**: 优先从 GGUF 格式源下载，支持多个备用源
- 🔄 **自动转换**: 必要时从 HuggingFace 下载原始模型（需要手动转换）
- ☁️ **云存储**: 自动上传到 Cloudflare R2
- ✅ **文件验证**: 自动计算和验证 SHA256 校验和
- 🔍 **重复检查**: 智能检查文件是否已存在，避免重复下载
- 📊 **详细报告**: 生成详细的上传报告和摘要
- 🛠️ **兼容性**: 支持 whisper.cpp、llama.cpp 等多种推理引擎

## 使用方法

### 1. 设置必要的 Secrets

在您的 GitHub 仓库中设置以下 Secrets:

```
R2_ACCESS_KEY_ID      - Cloudflare R2 访问密钥 ID
R2_SECRET_ACCESS_KEY  - Cloudflare R2 秘密访问密钥
S3_ENDPOINT          - Cloudflare R2 端点 URL (例如: https://your-account.r2.cloudflarestorage.com)
R2_BUCKET            - Cloudflare R2 存储桶名称
```

#### 获取 Cloudflare R2 凭证

1. 登录 [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. 点击 "R2 Object Storage"
3. 创建一个新的存储桶或使用现有存储桶
4. 前往 "Manage R2 API tokens"
5. 创建新的 API token，确保有读写权限
6. 记录下 Access Key ID、Secret Access Key 和端点 URL

### 2. 手动触发 Workflow

1. 进入您的 GitHub 仓库
2. 点击 "Actions" 标签页
3. 选择 "下载并上传 Whisper Large V3 Turbo 模型" workflow
4. 点击 "Run workflow" 按钮
5. 配置参数（可选）:
   - **模型版本**: 默认为 `large-v3-turbo`，也可以选择其他版本
   - **强制重新下载**: 如果模型已存在，是否强制重新下载

### 3. 支持的模型版本

目前支持以下 Whisper 模型版本:

| 模型版本 | 参数量 | 文件大小 | 推荐用途 |
|---------|-------|----------|----------|
| `large-v3-turbo` | 809M | ~1.5GB | 推荐，速度最快 |
| `large-v3` | 1550M | ~3.1GB | 最高精度 |
| `large-v2` | 1550M | ~3.1GB | 稳定版本 |
| `large` | 1550M | ~3.1GB | 经典版本 |
| `medium` | 769M | ~1.5GB | 平衡性能 |
| `small` | 244M | ~500MB | 轻量级 |
| `base` | 74M | ~150MB | 基础版本 |
| `tiny` | 39M | ~80MB | 最小版本 |

## 下载策略

工具使用多层次的下载策略确保成功率：

### 优先级 1: 直接 GGUF 下载
- 从 `ggerganov/whisper.cpp` 仓库下载预编译的 GGUF 模型
- 从社区维护的 GGUF 格式仓库下载
- 适用于所有支持的模型版本

### 优先级 2: 原始模型下载 + 转换说明
- 从 OpenAI 官方仓库下载 PyTorch 格式模型
- 生成转换说明文件
- 需要用户手动使用 whisper.cpp 工具转换

### 优先级 3: 错误处理
- 详细的错误日志
- 建议的解决方案
- 重试机制

## 文件结构

上传到 R2 后，文件将按以下结构组织:

```
models/
├── ggml-large-v3-turbo.bin              # 模型文件
├── ggml-large-v3-turbo.bin.sha256       # SHA256 校验和
└── whisper-large-v3-turbo-info.json     # 模型信息文件
```

## 模型信息文件

每个上传的模型都会包含一个 JSON 信息文件，包含以下信息:

```json
{
  "model_name": "whisper-large-v3-turbo",
  "version": "large-v3-turbo",
  "format": "gguf",
  "file_name": "ggml-large-v3-turbo.bin",
  "uploaded_at": "2024-01-01T12:00:00Z",
  "upload_workflow": "下载并上传 Whisper Large V3 Turbo 模型",
  "run_id": "123456789",
  "run_number": "42",
  "file_size_bytes": 1234567890,
  "file_size_human": "1.2G",
  "sha256": "abc123...",
  "download_url": "https://r2.your-domain.com/models/ggml-large-v3-turbo.bin",
  "description": "Whisper large-v3-turbo GGUF format model for speech recognition",
  "compatible_with": ["whisper.cpp", "llama.cpp", "candle-whisper"],
  "license": "MIT",
  "source": "OpenAI Whisper"
}
```

## 在应用中使用

上传完成后，您可以在应用程序中使用这些模型:

### C# 示例 (OWhisper.NET)

```csharp
using OWhisper.Core;

public class WhisperService
{
    private const string ModelBaseUrl = "https://your-r2-domain.com/models/";
    private readonly string _modelsDirectory;
    
    public WhisperService()
    {
        _modelsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "OWhisper", 
            "models"
        );
        Directory.CreateDirectory(_modelsDirectory);
    }
    
    public async Task<string> DownloadModelAsync(string modelVersion = "large-v3-turbo")
    {
        var modelFileName = $"ggml-{modelVersion}.bin";
        var modelPath = Path.Combine(_modelsDirectory, modelFileName);
        
        // 检查模型是否已存在
        if (File.Exists(modelPath))
        {
            return modelPath;
        }
        
        // 下载模型信息
        using var httpClient = new HttpClient();
        var infoUrl = $"{ModelBaseUrl}whisper-{modelVersion}-info.json";
        var infoJson = await httpClient.GetStringAsync(infoUrl);
        var modelInfo = JsonSerializer.Deserialize<ModelInfo>(infoJson);
        
        // 下载模型文件
        Console.WriteLine($"正在下载模型: {modelInfo.model_name}");
        Console.WriteLine($"文件大小: {modelInfo.file_size_human}");
        
        var modelUrl = $"{ModelBaseUrl}{modelFileName}";
        var modelData = await httpClient.GetByteArrayAsync(modelUrl);
        await File.WriteAllBytesAsync(modelPath, modelData);
        
        // 验证校验和
        if (!string.IsNullOrEmpty(modelInfo.sha256))
        {
            await VerifyChecksumAsync(modelPath, modelInfo.sha256);
        }
        
        Console.WriteLine($"模型下载完成: {modelPath}");
        return modelPath;
    }
    
    private async Task VerifyChecksumAsync(string filePath, string expectedSha256)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        var actualSha256 = Convert.ToHexString(hash).ToLowerInvariant();
        
        if (actualSha256 != expectedSha256.ToLowerInvariant())
        {
            throw new InvalidOperationException($"文件校验和不匹配。期望: {expectedSha256}, 实际: {actualSha256}");
        }
    }
    
    public async Task<string> TranscribeAsync(string audioPath, string modelVersion = "large-v3-turbo")
    {
        var modelPath = await DownloadModelAsync(modelVersion);
        
        // 使用 OWhisper.NET 进行转录
        using var whisper = new WhisperProcessor(modelPath);
        return await whisper.TranscribeAsync(audioPath);
    }
}

public class ModelInfo
{
    public string model_name { get; set; }
    public string version { get; set; }
    public string format { get; set; }
    public string file_name { get; set; }
    public long file_size_bytes { get; set; }
    public string file_size_human { get; set; }
    public string sha256 { get; set; }
    public string[] compatible_with { get; set; }
}
```

### JavaScript/Node.js 示例

```javascript
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const fetch = require('node-fetch');

class WhisperModelManager {
    constructor(baseUrl = 'https://your-r2-domain.com/models/') {
        this.baseUrl = baseUrl;
        this.modelsDir = path.join(process.cwd(), 'models');
        
        // 确保模型目录存在
        if (!fs.existsSync(this.modelsDir)) {
            fs.mkdirSync(this.modelsDir, { recursive: true });
        }
    }
    
    async downloadModel(modelVersion = 'large-v3-turbo') {
        const modelFileName = `ggml-${modelVersion}.bin`;
        const modelPath = path.join(this.modelsDir, modelFileName);
        
        // 检查模型是否已存在
        if (fs.existsSync(modelPath)) {
            console.log(`模型已存在: ${modelPath}`);
            return modelPath;
        }
        
        try {
            // 获取模型信息
            const infoUrl = `${this.baseUrl}whisper-${modelVersion}-info.json`;
            const infoResponse = await fetch(infoUrl);
            const modelInfo = await infoResponse.json();
            
            console.log(`正在下载模型: ${modelInfo.model_name}`);
            console.log(`文件大小: ${modelInfo.file_size_human}`);
            
            // 下载模型文件
            const modelUrl = `${this.baseUrl}${modelFileName}`;
            const modelResponse = await fetch(modelUrl);
            
            if (!modelResponse.ok) {
                throw new Error(`下载失败: ${modelResponse.statusText}`);
            }
            
            const modelBuffer = await modelResponse.buffer();
            fs.writeFileSync(modelPath, modelBuffer);
            
            // 验证校验和
            if (modelInfo.sha256) {
                await this.verifyChecksum(modelPath, modelInfo.sha256);
            }
            
            console.log(`模型下载完成: ${modelPath}`);
            return modelPath;
            
        } catch (error) {
            console.error(`下载模型失败: ${error.message}`);
            throw error;
        }
    }
    
    async verifyChecksum(filePath, expectedSha256) {
        return new Promise((resolve, reject) => {
            const hash = crypto.createHash('sha256');
            const stream = fs.createReadStream(filePath);
            
            stream.on('data', data => hash.update(data));
            stream.on('end', () => {
                const actualSha256 = hash.digest('hex');
                if (actualSha256.toLowerCase() !== expectedSha256.toLowerCase()) {
                    reject(new Error(`文件校验和不匹配。期望: ${expectedSha256}, 实际: ${actualSha256}`));
                } else {
                    resolve();
                }
            });
            stream.on('error', reject);
        });
    }
    
    async listAvailableModels() {
        // 这个功能需要您的 R2 配置支持列出对象
        try {
            const response = await fetch(`${this.baseUrl}?list-type=2&prefix=whisper-&suffix=-info.json`);
            // 根据您的 R2 配置解析响应
            return response.json();
        } catch (error) {
            console.error('获取模型列表失败:', error);
            return [];
        }
    }
}

// 使用示例
async function main() {
    const modelManager = new WhisperModelManager();
    
    try {
        // 下载模型
        const modelPath = await modelManager.downloadModel('large-v3-turbo');
        
        // 这里您可以使用 whisper.cpp 的 Node.js 绑定
        // 或者调用 whisper.cpp 的命令行工具
        console.log(`模型路径: ${modelPath}`);
        
    } catch (error) {
        console.error('错误:', error);
    }
}

module.exports = WhisperModelManager;
```

### Python 示例

```python
import os
import json
import hashlib
import requests
from pathlib import Path

class WhisperModelManager:
    def __init__(self, base_url="https://your-r2-domain.com/models/"):
        self.base_url = base_url
        self.models_dir = Path.cwd() / "models"
        self.models_dir.mkdir(exist_ok=True)
    
    async def download_model(self, model_version="large-v3-turbo"):
        model_filename = f"ggml-{model_version}.bin"
        model_path = self.models_dir / model_filename
        
        # 检查模型是否已存在
        if model_path.exists():
            print(f"模型已存在: {model_path}")
            return str(model_path)
        
        try:
            # 获取模型信息
            info_url = f"{self.base_url}whisper-{model_version}-info.json"
            info_response = requests.get(info_url)
            info_response.raise_for_status()
            model_info = info_response.json()
            
            print(f"正在下载模型: {model_info['model_name']}")
            print(f"文件大小: {model_info['file_size_human']}")
            
            # 下载模型文件
            model_url = f"{self.base_url}{model_filename}"
            model_response = requests.get(model_url, stream=True)
            model_response.raise_for_status()
            
            # 保存文件
            with open(model_path, 'wb') as f:
                for chunk in model_response.iter_content(chunk_size=8192):
                    f.write(chunk)
            
            # 验证校验和
            if model_info.get('sha256'):
                self.verify_checksum(model_path, model_info['sha256'])
            
            print(f"模型下载完成: {model_path}")
            return str(model_path)
            
        except Exception as e:
            print(f"下载模型失败: {e}")
            raise
    
    def verify_checksum(self, file_path, expected_sha256):
        hash_sha256 = hashlib.sha256()
        with open(file_path, "rb") as f:
            for chunk in iter(lambda: f.read(4096), b""):
                hash_sha256.update(chunk)
        
        actual_sha256 = hash_sha256.hexdigest()
        if actual_sha256.lower() != expected_sha256.lower():
            raise ValueError(f"文件校验和不匹配。期望: {expected_sha256}, 实际: {actual_sha256}")

# 使用示例
if __name__ == "__main__":
    import asyncio
    
    async def main():
        manager = WhisperModelManager()
        model_path = await manager.download_model("large-v3-turbo")
        
        # 这里您可以使用 whisper.cpp 的 Python 绑定
        # 或者使用 openai-whisper 库
        print(f"模型路径: {model_path}")
    
    asyncio.run(main())
```

## 故障排除

### 下载失败

如果模型下载失败，workflow 会自动尝试备用下载方式。检查以下项目:

1. **网络连接**: 确保 GitHub Actions runner 可以访问 Hugging Face
2. **模型可用性**: 确认请求的模型版本存在
3. **存储空间**: 确保 runner 有足够的磁盘空间
4. **下载源状态**: 检查 Hugging Face 和相关仓库的可用性

#### 常见错误及解决方案

**错误**: `urllib.error.HTTPError: HTTP Error 404: Not Found`
- **原因**: 模型文件不存在或 URL 错误
- **解决**: 检查模型版本是否正确，或尝试其他版本

**错误**: `No space left on device`
- **原因**: 磁盘空间不足
- **解决**: 清理临时文件或使用更小的模型

**错误**: `Connection timeout`
- **原因**: 网络连接问题
- **解决**: 重新运行 workflow，或检查网络状态

### 上传失败

如果上传到 R2 失败，检查以下项目:

1. **R2 凭证**: 确认 Secrets 配置正确
2. **存储桶权限**: 确保访问密钥有写入权限
3. **端点 URL**: 确认 S3_ENDPOINT 配置正确
4. **存储桶名称**: 确认 R2_BUCKET 名称正确

#### R2 配置检查清单

- [ ] Access Key ID 正确
- [ ] Secret Access Key 正确
- [ ] 端点 URL 格式正确 (https://账户ID.r2.cloudflarestorage.com)
- [ ] 存储桶存在且可访问
- [ ] API Token 有 Object Read 和 Object Write 权限

### 文件校验

如果担心文件完整性，可以：

1. 下载 `.sha256` 文件进行本地校验
2. 比对模型信息文件中的校验和
3. 检查文件大小是否符合预期
4. 使用工具验证 GGUF 文件格式

```bash
# 验证校验和
sha256sum ggml-large-v3-turbo.bin
# 应该与 .sha256 文件中的值匹配

# 检查 GGUF 文件头
xxd -l 32 ggml-large-v3-turbo.bin
# 应该以 GGUF 魔术字节开头
```

## 高级配置

### 自定义存储路径

默认情况下，模型存储在 `models/` 目录下。要修改路径，更新 workflow 中的相关步骤:

```yaml
# 修改上传路径
aws s3 cp "$model_file" "s3://$R2_BUCKET/custom-path/models/" --endpoint-url $S3_ENDPOINT
```

### 添加新的模型版本

要支持新的模型版本，修改 workflow 文件中的下载 URL 映射:

```python
gguf_download_urls = {
    "large-v3-turbo": [...],
    "your-new-model": [
        "https://your-custom-source.com/model.bin",
        "https://backup-source.com/model.bin"
    ]
}
```

### 添加通知功能

您可以添加额外的步骤来发送通知:

```yaml
- name: 发送成功通知
  if: success()
  run: |
    curl -X POST -H 'Content-type: application/json' \
      --data '{"text":"✅ Whisper 模型 ${{ github.event.inputs.model_version }} 上传完成！"}' \
      ${{ secrets.SLACK_WEBHOOK_URL }}

- name: 发送失败通知
  if: failure()
  run: |
    curl -X POST -H 'Content-type: application/json' \
      --data '{"text":"❌ Whisper 模型上传失败，请检查日志。"}' \
      ${{ secrets.SLACK_WEBHOOK_URL }}
```

### 自动化触发

如果您想要自动化模型更新，可以添加定时触发:

```yaml
on:
  schedule:
    # 每周检查一次新模型
    - cron: '0 2 * * 1'
  workflow_dispatch:
    # 保留手动触发
```

### 并行下载多个模型

要同时下载多个模型版本，可以修改 workflow 使用矩阵策略:

```yaml
jobs:
  download-and-upload-model:
    strategy:
      matrix:
        model_version: [large-v3-turbo, large-v3, medium, small]
    steps:
      # ... 现有步骤
```

## 性能优化建议

### 模型选择指南

根据您的使用场景选择合适的模型:

- **实时转录**: 选择 `large-v3-turbo` 或 `medium`
- **高精度转录**: 选择 `large-v3` 或 `large-v2`
- **资源受限环境**: 选择 `small` 或 `base`
- **原型开发**: 选择 `tiny` 进行快速测试

### 存储成本优化

- 只下载需要的模型版本
- 设置定期清理旧版本的 workflow
- 使用 R2 的生命周期策略自动管理文件

### 网络优化

- 使用 CDN 加速模型下载
- 在应用中实现断点续传
- 考虑使用压缩传输

## 许可证

本工具遵循项目的主许可证。Whisper 模型本身遵循 OpenAI 的 MIT 许可证条款。

## 贡献指南

欢迎贡献代码和改进建议：

1. Fork 本仓库
2. 创建功能分支
3. 提交更改
4. 创建 Pull Request

### 已知问题

- 大型模型下载可能需要较长时间
- 某些地区可能无法访问 Hugging Face
- GGUF 格式转换需要额外工具

### 未来计划

- [ ] 支持自动 GGUF 格式转换
- [ ] 添加模型量化选项
- [ ] 支持批量模型管理
- [ ] 集成更多推理引擎
- [ ] 添加模型性能基准测试 