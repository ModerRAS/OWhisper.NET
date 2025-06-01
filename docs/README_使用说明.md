# OWhisper.NET 使用说明

## 概述

OWhisper.NET 现在已经成功重构为多项目架构，支持 Windows 桌面应用和跨平台命令行版本。

## 项目结构

```
OWhisper.NET/
├── OWhisper.Core/          # 核心业务逻辑库（多目标框架：.NET 4.8 + .NET 8.0）
├── OWhisper.NET/           # Windows 桌面应用（.NET 4.8）
├── OWhisper.CLI/           # 跨平台命令行应用（.NET 8.0）
└── OWhisper.NET.sln        # 解决方案文件
```

## 编译和运行

### 编译整个解决方案
```bash
dotnet build OWhisper.NET.sln
```

### 运行 Windows 桌面版
```bash
# 方法1：直接运行编译后的程序
OWhisper.NET\bin\Debug\net48\OWhisper.NET.exe

# 方法2：使用 dotnet（如果支持）
dotnet run --project OWhisper.NET
```

### 运行跨平台 CLI 版
```bash
dotnet run --project OWhisper.CLI
```

CLI 版本启动后会：
- 在 http://localhost:5000 运行 Web 服务器
- 提供 REST API 端点：
  - `GET /` - 健康检查
  - `GET /api/status` - 服务状态查询

## 配置

### CLI 配置文件 (appsettings.json)
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

### 环境变量
- `OWHISPER_PORT`: 覆盖 Web 服务器端口
- `OWHISPER_HOST`: 覆盖 Web 服务器主机地址

## 平台特定路径

### Windows
- 应用数据: `%LOCALAPPDATA%\OWhisper.NET`
- 模型文件: `%LOCALAPPDATA%\OWhisper.NET\models`
- 日志文件: `%LOCALAPPDATA%\OWhisper.NET\logs`

### Linux
- 应用数据: `/var/lib/owhisper`
- 模型文件: `/var/lib/owhisper/models`
- 日志文件: `/var/lib/owhisper/logs`

### macOS
- 应用数据: `~/Library/Application Support/OWhisper.NET`
- 模型文件: `~/Library/Application Support/OWhisper.NET/models`
- 日志文件: `~/Library/Application Support/OWhisper.NET/logs`

## 功能特性

### ✅ 已完成功能
- 多目标框架支持（.NET 4.8 + .NET 8.0）
- 跨平台路径管理
- 音频处理（MP3, WAV, AAC）
- Whisper 语音转文本
- Web API 服务器
- 结构化日志记录
- 依赖注入架构

### 🚧 部分完成
- 模型自动下载功能
- Web API 端点（基础功能已实现）

### 📋 待完善
- 完整的 REST API
- 文件上传转录接口
- Docker 容器化支持
- 单元测试

## 技术架构

### 核心组件
- **OWhisper.Core**: 业务逻辑、服务层、跨平台抽象
- **OWhisper.NET**: Windows Forms 桌面应用
- **OWhisper.CLI**: .NET 8.0 现代化命令行应用

### 主要依赖
- Whisper.net: 语音转文本核心
- NAudio: 音频处理
- Serilog: 结构化日志
- EmbedIO: 轻量级 Web 服务器
- Microsoft.Extensions.Hosting: 现代化应用框架

## 故障排除

### 常见问题

1. **模型文件缺失**
   - 首次运行时会自动下载所需的 Whisper 模型
   - 确保网络连接正常

2. **权限问题（Linux/macOS）**
   ```bash
   # 创建应用目录并设置权限
   sudo mkdir -p /var/lib/owhisper
   sudo chown $USER:$USER /var/lib/owhisper
   ```

3. **端口占用**
   - 修改 `appsettings.json` 中的端口配置
   - 或使用环境变量 `OWHISPER_PORT=8080`

### 日志查看
- CLI 版本日志: `logs/owhisper-cli-*.txt`
- 桌面版日志: 应用数据目录的 `logs` 文件夹

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

MIT 许可证 