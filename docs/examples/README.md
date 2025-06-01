# OWhisper.NET 编程语言调用示例

本目录包含各种编程语言调用OWhisper.NET API的完整示例代码。

## 📁 文件列表

| 语言 | 文件名 | 说明 |
|------|--------|------|
| Python | [`python_client.py`](python_client.py) | Python客户端示例，支持环境变量配置 |
| Node.js | [`nodejs_client.js`](nodejs_client.js) | JavaScript/Node.js客户端示例 |
| C# | [`csharp_client.cs`](csharp_client.cs) | C#/.NET客户端示例 |
| Java | [`java_client.java`](java_client.java) | Java客户端示例，使用OkHttp库 |
| PHP | [`php_client.php`](php_client.php) | PHP客户端示例，使用cURL |
| Go | [`go_client.go`](go_client.go) | Go语言客户端示例 |

## 🌟 功能特性

所有示例都支持：

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
```

### 2. 选择编程语言

#### Python
```bash
# 安装依赖
pip install requests

# 运行示例
python python_client.py
```

#### Node.js
```bash
# 安装依赖
npm install axios form-data

# 运行示例
node nodejs_client.js
```

#### C#
```bash
# 编译运行
dotnet run csharp_client.cs
```

#### Java
```bash
# 编译运行 (需要添加OkHttp和Gson依赖)
javac -cp "okhttp.jar:gson.jar" java_client.java
java -cp ".:okhttp.jar:gson.jar" OWhisperClient
```

#### PHP
```bash
# 直接运行
php php_client.php
```

#### Go
```bash
# 运行示例
go run go_client.go
```

## 📝 基本用法

所有客户端都遵循相同的模式：

```python
# 1. 创建客户端 (自动读取环境变量)
client = OWhisperClient()

# 2. 检查服务状态
status = client.get_status()

# 3. 转写音频文件
result = client.transcribe_file("audio.mp3")

# 4. 处理结果
if result["Status"] == "success":
    # 保存转写文本
    with open("output.txt", "w") as f:
        f.write(result["Data"]["Text"])
    
    # 保存SRT字幕
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

## ⚠️ 注意事项

1. **文件大小限制** - 建议单个音频文件不超过500MB
2. **超时设置** - 长音频文件需要更长的处理时间
3. **网络连接** - 确保能够访问OWhisper.NET服务
4. **音频格式** - 支持MP3、WAV、AAC格式
5. **编码格式** - 输出文件使用UTF-8编码

## 🔗 相关链接

- [主要文档](../../README.md)
- [快速开始](../../QUICK_START.md)
- [完整API文档](../../README.md#api-文档) 