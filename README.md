# OWhisper.NET

<div align="center">

<img src="OWhisper.NET/Resources/app_icon_super_256x256.png" alt="OWhisper.NET" width="128" height="128">

![OWhisper.NET Logo](https://img.shields.io/badge/OWhisper.NET-音频转写工具-blue?style=for-the-badge)

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue)](https://dotnet.microsoft.com/download/dotnet-framework)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)](https://github.com/microsoft/windows)

**基于 Whisper.net 的高性能音频转写工具**

提供图形界面和 RESTful API，支持多种音频格式转文本和SRT字幕

</div>

## 🌟 功能特性

### 🎯 核心功能
- **多格式支持**: MP3、WAV、AAC 音频格式
- **智能输出**: 支持纯文本和SRT字幕格式
- **高精度转写**: 基于OpenAI Whisper大型模型
- **实时进度**: 转写进度实时显示
- **自动下载**: 模型文件自动下载和管理
- **队列识别**: 批量处理多个音频文件，支持文件夹导入 🆕

### 🖥️ 用户界面
- **图形界面**: 直观的Windows Forms界面
- **拖拽操作**: 简单易用的文件选择
- **格式预览**: 智能文件格式建议
- **进度显示**: 实时处理进度条
- **队列管理**: 专用的批量处理管理窗口 🆕

### 🔌 API服务
- **RESTful API**: 标准HTTP接口
- **多端支持**: 支持各种编程语言调用
- **状态监控**: 服务状态实时查询
- **错误处理**: 完善的错误信息返回
- **环境变量**: 支持OWHISPER_HOST、OWHISPER_PORT配置
- **任务队列**: 支持多任务串行处理和管理 🆕

### ✨ 队列识别功能 🆕

#### 🎛️ 托盘菜单更新
- **新增**: "队列识别" - 打开批量处理管理器
- **新增**: "模型管理" - 打开模型管理界面 🆕
- **保留**: "检查更新" - 检查软件更新
- **移除**: "下载更新" - 简化更新流程

#### 📋 批量处理特性
- **批量添加**: 支持多选文件或整个文件夹导入
- **智能队列**: 自动排队处理，避免资源冲突
- **实时监控**: 显示每个任务的处理状态和进度
- **自动保存**: 转录完成后自动保存到指定文件夹
- **智能分类**: 按日期自动创建子文件夹组织输出文件
- **状态管理**: 支持暂停、恢复、取消和移除任务

#### 🎵 支持的音频格式
- **WAV**: 无损音频格式（推荐）
- **MP3**: 常见的压缩音频格式
- **FLAC**: 无损压缩音频格式
- **OGG**: 开源音频格式
- **M4A**: Apple 音频格式
- **WMA**: Windows Media Audio
- **AAC**: 高级音频编码

#### 📁 智能文件组织
```
我的文档/OWhisper转录结果/
├── 2024-01-15/           # 按日期自动分类
│   ├── 会议录音.txt
│   ├── 课程讲座.txt
│   └── 采访记录.txt
├── 2024-01-16/
│   ├── 电话会议.txt
│   └── 培训视频.txt
└── ...
```

> 📖 **详细使用说明**: 查看 [`docs/队列识别功能说明.md`](docs/队列识别功能说明.md) 了解完整的使用方法和功能介绍。

### 🔧 模型管理功能 🆕

#### 🎯 核心特性
- **可视化管理**: 提供直观的模型管理界面
- **手动下载**: 支持手动选择和下载不同模型版本
- **文件导入**: 支持导入本地模型文件（.bin、.gguf格式）
- **状态监控**: 实时显示当前模型状态和文件信息
- **完整性验证**: 自动验证模型文件的SHA256校验和
- **多模型支持**: 支持Large V3 Turbo、Large V3、Medium、Small等版本

#### 🗂️ 模型版本支持

| 模型版本 | 文件大小 | 推荐用途 | 特点 |
|---------|----------|---------|------|
| **Large V3 Turbo** 🌟 | 809MB | 日常使用 | 速度最快，推荐使用 |
| **Large V3** | 1.5GB | 高精度需求 | 最高精度版本 |
| **Medium** | 769MB | 平衡使用 | 性能和精度的良好平衡 |
| **Small** | 244MB | 资源受限 | 轻量级版本 |

#### 🛠️ 管理功能
- **状态检查**: 检查当前模型文件是否有效
- **删除模型**: 安全删除当前模型文件
- **刷新状态**: 重新检查模型状态信息
- **打开文件夹**: 直接访问模型存储目录
- **下载进度**: 实时显示下载进度和速度
- **取消下载**: 支持随时取消正在进行的下载

> 📖 **详细使用说明**: 查看 [`docs/模型管理功能说明.md`](docs/模型管理功能说明.md) 了解完整的模型管理功能介绍。

## 📋 系统要求

- **操作系统**: Windows 10/11 (x64)
- **运行时**: .NET Framework 4.8
- **内存**: 建议 4GB+ RAM
- **存储**: 2GB+ 可用空间 (用于模型文件)
- **网络**: 首次运行需要网络连接下载模型

## 🚀 快速开始

### 1. [下载安装](https://velopack.miaostay.com/OWhisper.NET-nightly-win-x64-Setup.exe)
```bash
# 克隆项目
git clone https://github.com/ModerRAS/OWhisper.NET.git
cd OWhisper.NET

# 编译项目
dotnet build OWhisper.NET.sln
```

### 2. 启动应用

```bash
# 图形界面模式
OWhisper.NET.exe

# 调试模式 (显示窗口)
OWhisper.NET.exe --debug

# API服务模式
OWhisper.NET.exe --api-only
```

### 3. 环境变量配置

```bash
# Windows PowerShell
$env:OWHISPER_HOST = "0.0.0.0"     # 监听地址 (默认: 0.0.0.0)
$env:OWHISPER_PORT = "11899"       # 监听端口 (默认: 11899)

# Windows CMD
set OWHISPER_HOST=0.0.0.0
set OWHISPER_PORT=11899

# Linux/macOS
export OWHISPER_HOST=0.0.0.0
export OWHISPER_PORT=11899
```

### 4. 使用图形界面

1. **选择音频文件** - 点击"选择文件"按钮
2. **设置输出位置** - 点击"另存为"选择保存位置和格式
3. **开始转写** - 点击"开始处理"按钮
4. **等待完成** - 查看进度条，完成后自动保存

## 📡 API 接口

### 基础信息
- **基础URL**: `http://localhost:11899`
- **内容类型**: `application/json` (响应), `multipart/form-data` (文件上传)
- **环境变量配置**: 支持 `OWHISPER_HOST` 和 `OWHISPER_PORT`

### 📋 接口列表

| 接口 | 方法 | 说明 | 类型 |
|------|------|------|------|
| `/api/model/status` | GET | 获取模型状态 | 状态查询 |
| `/api/transcribe` | POST | 提交转录任务 | 任务提交 |
| `/api/tasks` | GET | 获取所有任务列表 | 任务管理 |
| `/api/tasks/{taskId}` | GET | 获取指定任务详情 | 任务查询 |
| `/api/tasks/{taskId}/cancel` | POST | 取消指定任务 | 任务控制 |
| `/api/tasks/{taskId}/progress` | GET | SSE实时进度监听 | 实时通信 |
| `/api/queue/progress` | GET | SSE队列状态监听 | 实时通信 |

### 🎯 核心功能

#### 1. 任务队列系统
- ✅ **串行处理** - 多个任务按顺序执行，避免资源冲突
- ✅ **队列管理** - 自动管理任务优先级和执行顺序
- ✅ **状态追踪** - 实时跟踪任务状态变化
- ✅ **任务取消** - 支持取消排队中的任务

#### 2. Server-Sent Events (SSE)
- ✅ **实时进度** - 通过SSE推送转录进度
- ✅ **状态更新** - 实时接收任务状态变化
- ✅ **队列监控** - 监听整个队列的状态
- ✅ **心跳检测** - 保持连接活跃状态

### 📝 接口详情

#### 获取模型状态
```http
GET /api/model/status
```

**响应示例:**
```json
{
  "status": "success",
  "data": {
    "exists": true,
    "valid": true,
    "size": 1550000000,
    "path": "Models/ggml-large-v3-turbo.bin"
  }
}
```

#### 提交转录任务
```http
POST /api/transcribe
Content-Type: multipart/form-data

file: [音频文件]
```

**响应示例:**
```json
{
  "status": "success",
  "data": {
    "taskId": "abc123def456",
    "queuePosition": 1
  }
}
```

#### 获取任务详情
```http
GET /api/tasks/{taskId}
```

**响应示例:**
```json
{
  "status": "success",
  "data": {
    "id": "abc123def456",
    "fileName": "audio.mp3",
    "status": "Completed",
    "progress": 100.0,
    "queuePosition": 0,
    "createdAt": "2024-01-01T10:00:00Z",
    "startedAt": "2024-01-01T10:01:00Z",
    "completedAt": "2024-01-01T10:05:00Z",
    "result": {
      "text": "转录的文本内容",
      "srtContent": "1\n00:00:00,000 --> 00:00:05,000\n转录的文本内容\n",
      "processingTime": 240.5
    },
    "errorMessage": null
  }
}
```

#### SSE实时进度监听
```http
GET /api/tasks/{taskId}/progress
Accept: text/event-stream
```

**SSE事件流示例:**
```
event: progress
data: {"taskId":"abc123","status":"Queued","progress":0,"queuePosition":2,"message":"队列中等待，位置: 2"}

event: progress  
data: {"taskId":"abc123","status":"Processing","progress":25.5,"queuePosition":0,"message":"正在处理... (25.5%)"}

event: progress
data: {"taskId":"abc123","status":"Completed","progress":100,"result":{"text":"转录结果","srtContent":"...","processingTime":180.2}}

event: heartbeat
data: {"timestamp":"2024-01-01T10:05:00Z"}
```

### 🔄 任务状态流程

```mermaid
graph TD
    A[提交任务] --> B[Queued 排队中]
    B --> C[Processing 处理中]
    C --> D[Completed 已完成]
    C --> E[Failed 失败]
    B --> F[Cancelled 已取消]
    
    B -.-> G[可以取消]
    C -.-> H[无法取消]
    D -.-> I[获取结果]
    E -.-> J[查看错误]
```

### 📊 任务状态说明

| 状态 | 说明 | 可操作 |
|------|------|--------|
| `Queued` | 任务已提交，等待处理 | ✅ 可取消 |
| `Processing` | 正在转录处理中 | ❌ 无法取消 |
| `Completed` | 转录完成 | ✅ 获取结果 |
| `Failed` | 处理失败 | ✅ 查看错误 |
| `Cancelled` | 已被取消 | ❌ 无法恢复 |

## 💻 编程语言调用示例

为了更好地组织代码示例，我们将所有编程语言的客户端代码统一到了专门的文档目录中。

### 📁 可用的客户端示例

| 语言 | 文件 | 类型 | 说明 |
|------|------|------|------|
| **Python** | [`docs/examples/python/python_client.py`](docs/examples/python/python_client.py) | 完整版 ⭐ | 功能丰富的Python客户端，包含服务等待、状态检测、自动保存 |
| **Node.js** | [`docs/examples/nodejs/nodejs_client.js`](docs/examples/nodejs/nodejs_client.js) | 完整版 ⭐ | 功能丰富的JavaScript客户端，包含完整项目结构 |
| **C#** | [`docs/examples/csharp_client.cs`](docs/examples/csharp_client.cs) | 标准版 | .NET Framework/Core客户端示例 |
| **C# SSE** | [`docs/examples/csharp_client_with_sse.cs`](docs/examples/csharp_client_with_sse.cs) | 高级版 ⭐ | 支持任务队列和SSE实时进度的C#客户端 |
| **Java** | [`docs/examples/java_client.java`](docs/examples/java_client.java) | 标准版 | 使用OkHttp库的Java客户端 |
| **PHP** | [`docs/examples/php_client.php`](docs/examples/php_client.php) | 标准版 | 使用cURL的PHP客户端 |
| **Go** | [`docs/examples/go_client.go`](docs/examples/go_client.go) | 标准版 | 原生HTTP客户端的Go示例 |
| **PowerShell** | [`docs/examples/powershell/`](docs/examples/powershell/) | 测试脚本 | 配置测试和环境变量验证脚本 |

### 🌟 功能特性

#### ⭐ 完整版客户端 (推荐)
- ✅ **服务等待** - 自动等待OWhisper.NET服务启动
- ✅ **状态检测** - 检查服务和模型状态  
- ✅ **进度显示** - 详细的处理进度信息
- ✅ **结果保存** - 自动保存TXT、SRT、JSON格式
- ✅ **文件检测** - 自动查找音频文件
- ✅ **项目结构** - 包含完整的依赖文件和配置

#### 🚀 高级版客户端 (SSE支持)
- ✅ **任务队列** - 支持多任务串行处理
- ✅ **实时进度** - 通过SSE获取实时处理进度
- ✅ **状态监听** - 实时监听任务状态变化
- ✅ **任务管理** - 支持任务取消和状态查询
- ✅ **队列监控** - 监听整个任务队列状态
- ✅ **心跳检测** - 自动维护SSE连接

#### 📋 标准版客户端
- ✅ **环境变量配置** - 支持`OWHISPER_HOST`和`OWHISPER_PORT`环境变量
- ✅ **默认值回退** - 未设置环境变量时自动使用默认配置
- ✅ **多种音频格式** - 自动检测MP3、WAV、AAC格式
- ✅ **错误处理** - 完善的异常处理机制
- ✅ **超时配置** - 适合长音频处理的超时设置

### 🚀 快速开始

1. **查看完整示例** - 访问 [`docs/examples/`](docs/examples/) 目录
2. **选择客户端类型**:
   - **完整版** (Python/Node.js) - 功能丰富，适合生产环境
   - **标准版** - 代码简洁，适合快速集成
3. **安装依赖** - 按照各语言的说明安装必要的第三方库
4. **配置环境** - 设置环境变量或直接修改代码中的服务地址
5. **运行示例** - 使用你的音频文件测试转写功能

### 📖 详细文档

查看 [`docs/examples/README.md`](docs/examples/README.md) 获取：
- 完整的目录结构说明
- 详细的安装和使用说明
- 环境变量配置方法
- 错误处理和故障排除
- API响应格式说明

## ⚙️ 配置说明

### 环境变量

```bash
# 监听地址 (默认: 0.0.0.0)
OWHISPER_HOST=0.0.0.0

# API服务端口 (默认: 11899)
OWHISPER_PORT=11899

# 模型下载代理 (可选)
HTTP_PROXY=http://proxy.example.com:8080
HTTPS_PROXY=http://proxy.example.com:8080
```

### 配置文件

创建 `appsettings.json` 文件:

```json
{
  "Whisper": {
    "ModelName": "ggml-large-v3-turbo.bin",
    "ModelType": "LargeV3Turbo",
    "ThreadCount": 16,
    "Language": "auto"
  },
  "Api": {
    "Host": "0.0.0.0",
    "Port": 11899,
    "AllowedOrigins": ["*"],
    "MaxFileSize": 524288000
  },
  "Logging": {
    "Level": "Information",
    "FilePath": "Logs/log-.txt"
  }
}
```

## 🔧 故障排除

### 常见问题

#### 1. 模型下载失败
```
错误: 模型下载失败: 网络连接超时
```
**解决方案:**
- 检查网络连接
- 配置代理服务器
- 手动下载模型文件到 `Models` 目录

#### 2. API服务无法启动
```
错误: 端口 11899 已被占用
```
**解决方案:**
- 使用环境变量修改端口: `$env:OWHISPER_PORT="12000"`
- 终止占用端口的进程
- 使用 `netstat -an | findstr 11899` 查看端口占用

#### 3. 音频格式不支持
```
错误: 不支持的音频格式
```
**解决方案:**
- 使用支持的格式: MP3, WAV, AAC
- 使用 FFmpeg 转换音频格式

#### 4. 内存不足
```
错误: 处理大文件时内存不足
```
**解决方案:**
- 增加系统内存
- 分割大音频文件
- 关闭其他占用内存的程序

### 日志分析

查看日志文件位置: `Logs/log-{date}.txt`

```bash
# 查看最新日志
Get-Content "Logs\log-*.txt" -Tail 50

# 搜索错误信息
Select-String -Path "Logs\*.txt" -Pattern "ERROR|FATAL"
```

## 📊 性能优化

### 硬件要求建议

| 音频时长 | 推荐配置 | 预估处理时间 |
|---------|---------|-------------|
| < 10分钟 | 4GB RAM | 1-3分钟 |
| 10-60分钟 | 8GB RAM | 3-15分钟 |
| > 1小时 | 16GB RAM | 15-60分钟 |

### 优化建议

1. **使用SSD硬盘**: 提高模型加载速度
2. **充足内存**: 避免虚拟内存交换
3. **多核CPU**: 利用多线程处理
4. **网络稳定**: 确保模型下载不中断

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

### 开发环境

```bash
# 克隆项目
git clone https://github.com/ModerRAS/OWhisper.NET.git

# 安装依赖
dotnet restore

# 运行测试
dotnet test

# 编译发布版本
dotnet publish -c Release
```

### 代码规范

- 使用 C# 编码规范
- 添加适当的注释和文档
- 确保测试通过
- 遵循现有的架构模式

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE.txt](LICENSE.txt) 文件了解详情。

## 🙏 致谢

- [Whisper.net](https://github.com/sandrohanea/whisper.net) - 核心语音识别库
- [OpenAI Whisper](https://github.com/openai/whisper) - 原始模型
- [NAudio](https://github.com/naudio/NAudio) - 音频处理库

---

<div align="center">

**如果这个项目对你有帮助，请给一个 ⭐️**

[报告问题](https://github.com/ModerRAS/OWhisper.NET/issues) · [功能请求](https://github.com/ModerRAS/OWhisper.NET/issues) · [贡献代码](https://github.com/ModerRAS/OWhisper.NET/pulls)

</div>