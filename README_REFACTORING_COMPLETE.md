# OWhisper.NET 重构完成

## 项目结构

重构后的项目现在包含三个主要组件：

### 1. OWhisper.Core (.NET Standard 2.0 + .NET 8.0 多目标)
**核心业务逻辑库，支持跨平台**

#### 功能特性
- **多目标框架支持**: 同时支持 .NET Framework 4.8 和 .NET 8.0
- **跨平台路径管理**: 自动适配 Windows、Linux、macOS 的存储路径
- **音频处理**: 支持 MP3、WAV、AAC 格式的音频文件处理
- **Whisper 转录**: 集成 Whisper.net 进行语音转文本

#### 核心组件
```
OWhisper.Core/
├── Models/
│   ├── TranscriptionResult.cs      # 转录结果模型
│   └── TranscriptionTask.cs        # 转录任务模型
├── Services/
│   ├── IPlatformPathService.cs     # 平台路径服务接口
│   ├── PlatformPathService.cs      # 跨平台路径实现
│   ├── WhisperService.cs           # Whisper 服务（单例）
│   ├── WhisperManager.cs           # Whisper 管理器
│   ├── AudioProcessor.cs           # 音频处理工具
│   └── HttpClientHelper.cs         # HTTP 客户端助手
└── Exceptions/
    └── AudioProcessingException.cs # 音频处理异常
```

#### 平台路径配置
- **Windows**: `%LOCALAPPDATA%\OWhisper.NET`
- **Linux**: `/var/lib/owhisper`
- **macOS**: `~/Library/Application Support/OWhisper.NET`

### 2. OWhisper.NET (.NET Framework 4.8)
**Windows 桌面应用程序**

#### 功能特性
- 保持原有的 Windows Forms 界面
- 引用 OWhisper.Core 获得核心功能
- 支持系统托盘运行
- 内置 Web 服务器提供 API 接口

#### 依赖关系
```
OWhisper.NET → OWhisper.Core (net48)
```

### 3. OWhisper.CLI (.NET 8.0)
**跨平台命令行应用程序**

#### 功能特性
- 基于 Microsoft.Extensions.Hosting 的现代 .NET 应用架构
- 依赖注入和配置管理
- Serilog 结构化日志
- 后台服务架构
- 跨平台兼容（Windows、Linux、macOS）

#### 依赖关系
```
OWhisper.CLI → OWhisper.Core (net8.0)
```

#### 核心组件
```
OWhisper.CLI/
├── Program.cs              # 主程序入口
├── appsettings.json        # 配置文件
└── WebServerHostedService  # Web 服务器后台服务
```

## 技术架构

### 多目标框架支持
OWhisper.Core 使用多目标框架配置：
```xml
<TargetFrameworks>net48;net8.0</TargetFrameworks>
```

这允许：
- OWhisper.NET (.NET 4.8) 使用 `net48` 目标
- OWhisper.CLI (.NET 8.0) 使用 `net8.0` 目标

### 依赖包管理
所有项目共享相同的核心依赖：
- **Whisper.net**: 语音转文本核心库
- **NAudio**: 音频处理
- **Serilog**: 结构化日志
- **EmbedIO**: 轻量级 Web 服务器
- **Newtonsoft.Json**: JSON 序列化

### 跨平台兼容性
通过 `IPlatformPathService` 实现平台特定的路径管理：
```csharp
public interface IPlatformPathService
{
    string GetApplicationDataPath();
    string GetModelsPath();
    string GetLogsPath();
    string GetTempPath();
    void EnsureDirectoriesExist();
}
```

## 编译和运行

### 编译整个解决方案
```bash
dotnet build OWhisper.NET.sln
```

### 运行 Windows 桌面版
```bash
# 在 Windows 上
dotnet run --project OWhisper.NET
```

### 运行跨平台 CLI 版
```bash
# 在任何支持的平台上
dotnet run --project OWhisper.CLI
```

## 配置说明

### CLI 配置 (appsettings.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "WebServer": {
    "Port": 5000,
    "Host": "localhost"
  }
}
```

### 环境变量支持
CLI 版本支持通过环境变量进行配置：
- `OWHISPER_PORT`: Web 服务器端口
- `OWHISPER_HOST`: Web 服务器主机地址

## 注意事项

### 模型文件
当前版本暂时简化了模型下载功能。需要手动下载 Whisper 模型文件到相应的模型目录：
- Windows: `%LOCALAPPDATA%\OWhisper.NET\models\`
- Linux: `/var/lib/owhisper/models/`
- macOS: `~/Library/Application Support/OWhisper.NET/models/`

### 已知限制
1. 模型自动下载功能暂未完全实现
2. 部分可空性警告需要进一步优化
3. CLI 版本的 Web 服务器功能需要进一步完善

## 下一步计划

1. **完善模型下载功能**: 实现自动模型下载和管理
2. **添加单元测试**: 为 Core 项目添加 xUnit 测试
3. **完善 CLI Web 服务器**: 实现完整的 REST API
4. **Docker 支持**: 为 CLI 版本添加 Docker 容器化支持
5. **性能优化**: 优化音频处理和转录性能

## 总结

重构成功实现了以下目标：
- ✅ 将核心逻辑分离到独立的 Core 项目
- ✅ 支持多目标框架 (.NET 4.8 + .NET 8.0)
- ✅ 创建跨平台 CLI 版本
- ✅ 实现平台特定的路径管理
- ✅ 保持原有 Windows 桌面应用的功能
- ✅ 整个解决方案可以成功编译

这个重构为项目的未来扩展和跨平台部署奠定了坚实的基础。 