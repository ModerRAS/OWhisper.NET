# OWhisper.NET 编程语言调用示例

本目录包含各种编程语言调用OWhisper.NET API的完整示例代码。

## 📁 目录结构

```
docs/examples/
├── python/                    # Python 项目示例
│   ├── python_client.py      # 完整客户端 (功能丰富版)
│   └── requirements.txt       # 依赖文件
├── nodejs/                    # Node.js 项目示例
│   ├── nodejs_client.js      # 完整客户端 (功能丰富版)
│   └── package.json          # 项目配置
├── powershell/               # PowerShell 脚本示例
│   ├── test_config.ps1       # 配置测试脚本
│   └── verify_env_fallback.ps1 # 环境变量验证脚本
├── csharp_client.cs          # C#/.NET客户端示例
├── java_client.java          # Java客户端示例
├── php_client.php            # PHP客户端示例
├── go_client.go              # Go语言客户端示例
└── README.md                 # 本文件
```

## 🌟 功能特性

### 🔥 完整版客户端 (推荐)
- **Python**: [`python/python_client.py`](python/python_client.py)
- **Node.js**: [`nodejs/nodejs_client.js`](nodejs/nodejs_client.js)

**功能特性:**
- ✅ **服务等待** - 自动等待OWhisper.NET服务启动
- ✅ **状态检测** - 检查服务和模型状态
- ✅ **进度显示** - 详细的处理进度信息
- ✅ **结果保存** - 自动保存TXT、SRT、JSON格式
- ✅ **错误处理** - 完善的异常处理机制
- ✅ **环境变量** - 支持OWHISPER_HOST、OWHISPER_PORT配置
- ✅ **文件检测** - 自动查找音频文件
- ✅ **项目结构** - 包含完整的依赖和配置文件

### 📋 标准版客户端
所有其他语言的客户端都支持：
- ✅ **环境变量配置** - 支持`OWHISPER_HOST`和`OWHISPER_PORT`
- ✅ **默认值回退** - 未设置环境变量时使用默认配置
- ✅ **多种音频格式** - 支持MP3、WAV、AAC格式
- ✅ **Content-Type自动检测** - 根据文件扩展名设置正确的MIME类型
- ✅ **错误处理** - 完善的异常处理和错误信息
- ✅ **超时配置** - 适合长时间音频处理的超时设置

## 🚀 快速使用

### 1. 启动OWhisper.NET服务

```bash
# 默认配置 (监听 0.0.0.0:11899)
OWhisper.NET.exe --debug

# 自定义配置
$env:OWHISPER_HOST = "127.0.0.1"
$env:OWHISPER_PORT = "8080"
OWhisper.NET.exe --debug

# 后台API服务模式  
OWhisper.NET.exe --api-only
```

### 2. 选择编程语言

#### Python (完整版 - 推荐)
```bash
cd docs/examples/python

# 安装依赖
pip install -r requirements.txt

# 运行示例 (自动查找音频文件)
python python_client.py

# 或者直接使用
from python_client import OWhisperClient
client = OWhisperClient()
result = client.transcribe_file("your_audio.mp3")
if result:
    client.save_results(result)
```

#### Node.js (完整版 - 推荐)
```bash
cd docs/examples/nodejs

# 安装依赖
npm install

# 运行示例 (自动查找音频文件)
node nodejs_client.js

# 或者作为模块使用
const OWhisperClient = require('./nodejs_client');
const client = new OWhisperClient();
const result = await client.transcribeFile('your_audio.mp3');
if (result) {
    await client.saveResults(result);
}
```

#### C# (标准版)
```bash
# 编译运行
dotnet run csharp_client.cs
```

#### Java (标准版)
```bash
# 编译运行 (需要添加OkHttp和Gson依赖)
javac -cp "okhttp.jar:gson.jar" java_client.java
java -cp ".:okhttp.jar:gson.jar" OWhisperClient
```

#### PHP (标准版)
```bash
# 直接运行
php php_client.php
```

#### Go (标准版)
```bash
# 运行示例
go run go_client.go
```

#### PowerShell (测试脚本)
```bash
# 测试配置
./powershell/test_config.ps1

# 验证环境变量
./powershell/verify_env_fallback.ps1
```

## 📝 基本用法

### 完整版客户端模式

```python
# Python 完整版示例
from python.python_client import OWhisperClient

client = OWhisperClient()

# 1. 等待服务启动
if client.wait_for_service():
    # 2. 检查服务状态
    status = client.get_status()
    
    # 3. 转写音频文件
    result = client.transcribe_file("audio.mp3")
    
    # 4. 自动保存结果 (TXT + SRT + JSON)
    if result:
        client.save_results(result)
```

### 标准版客户端模式

```python
# 标准版示例 (适用于所有其他语言)
client = OWhisperClient()

# 1. 检查服务状态
status = client.get_status()

# 2. 转写音频文件
result = client.transcribe_file("audio.mp3")

# 3. 手动处理结果
if result["Status"] == "success":
    with open("output.txt", "w") as f:
        f.write(result["Data"]["Text"])
    with open("output.srt", "w") as f:
        f.write(result["Data"]["SrtContent"])
```

## 🔧 环境变量配置

| 环境变量 | 默认值 | 说明 |
|---------|--------|------|
| `OWHISPER_HOST` | `localhost` | API服务器地址 |
| `OWHISPER_PORT` | `11899` | API服务器端口 |

设置方式：

```bash
# Windows PowerShell
$env:OWHISPER_HOST = "192.168.1.100"
$env:OWHISPER_PORT = "8080"

# Windows CMD
set OWHISPER_HOST=192.168.1.100
set OWHISPER_PORT=8080

# Linux/macOS
export OWHISPER_HOST=192.168.1.100
export OWHISPER_PORT=8080
```

## 📊 API响应格式

### 成功响应
```json
{
  "Status": "success",
  "Data": {
    "Text": "转写的纯文本内容",
    "SrtContent": "SRT字幕格式内容",
    "ProcessingTime": 15.6
  },
  "Error": null,
  "ErrorCode": null
}
```

### 错误响应
```json
{
  "Status": "error",
  "Data": null,
  "Error": "错误详细信息",
  "ErrorCode": "ERROR_CODE"
}
```

## 🎯 支持的音频格式

| 格式 | 扩展名 | Content-Type |
|------|--------|--------------|
| MP3  | .mp3   | audio/mpeg   |
| WAV  | .wav   | audio/wav    |
| AAC  | .aac   | audio/aac    |
| M4A  | .m4a   | audio/aac    |

## 📝 输出格式说明

### 1. 纯文本 (Text)
```
这是转写的完整文本内容，所有语音片段连接在一起。
```

### 2. SRT字幕 (SrtContent)
```
1
00:00:00,000 --> 00:00:03,500
第一段语音内容

2  
00:00:03,500 --> 00:00:08,200
第二段语音内容
```

## ⚠️ 注意事项

1. **首次运行**: 需要下载约1.5GB的模型文件，请确保网络连接稳定
2. **文件大小**: 建议单个音频文件不超过500MB
3. **处理时间**: 通常为音频长度的1/10到1/5，取决于硬件性能
4. **内存要求**: 处理大文件建议4GB+内存
5. **并发限制**: 当前版本不支持并发处理，请依次提交
6. **编码格式**: 输出文件使用UTF-8编码

## 🔧 故障排除

### 连接失败
```bash
# 检查服务是否启动
curl http://localhost:11899/api/status

# 检查端口是否被占用
netstat -an | findstr 11899
```

### 转写失败
1. 检查音频文件格式是否支持
2. 检查文件是否损坏
3. 查看应用日志文件: `Logs/log-*.txt`

### 性能优化
1. 使用SSD硬盘
2. 增加系统内存
3. 关闭其他占用资源的程序

## 🔗 相关链接

- [主要文档](../../README.md)
- [快速开始](../../QUICK_START.md)
- [完整API文档](../../README.md#api-文档) 