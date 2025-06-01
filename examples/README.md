# OWhisper.NET API 调用示例

本目录包含各种编程语言调用OWhisper.NET API的示例代码。

## 📁 目录结构

```
examples/
├── python/          # Python 示例
│   ├── client.py     # 完整客户端
│   └── requirements.txt
├── nodejs/          # Node.js 示例  
│   ├── client.js     # 完整客户端
│   └── package.json
└── README.md        # 本文件
```

## 🚀 快速开始

### 1. 启动 OWhisper.NET 服务

```bash
# 方式一: 图形界面调试模式
OWhisper.NET.exe --debug

# 方式二: 后台API服务模式  
OWhisper.NET.exe --api-only
```

### 2. Python 示例

```bash
cd examples/python

# 安装依赖
pip install -r requirements.txt

# 运行示例 (确保当前目录有音频文件)
python client.py
```

**Python 快速使用:**
```python
from client import OWhisperClient

client = OWhisperClient()
result = client.transcribe_file("your_audio.mp3")
if result:
    client.save_results(result)
```

### 3. Node.js 示例

```bash
cd examples/nodejs

# 安装依赖
npm install

# 运行示例 (确保当前目录有音频文件)
npm start
```

**Node.js 快速使用:**
```javascript
const OWhisperClient = require('./client');

const client = new OWhisperClient();
const result = await client.transcribeFile('your_audio.mp3');
if (result) {
    await client.saveResults(result);
}
```

## 📊 API 响应格式

### 成功响应

```json
{
  "Status": "success",
  "Data": {
    "Text": "转写的纯文本内容",
    "SrtContent": "1\n00:00:00,000 --> 00:00:03,500\n转写的文本内容\n\n",
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

## 🔧 故障排除

### 连接失败
```bash
# 检查服务是否启动
curl http://localhost:9000/api/status

# 检查端口是否被占用
netstat -an | findstr 9000
```

### 转写失败
1. 检查音频文件格式是否支持
2. 检查文件是否损坏
3. 查看应用日志文件: `Logs/log-*.txt`

### 性能优化
1. 使用SSD硬盘
2. 增加系统内存
3. 关闭其他占用资源的程序

## 📖 更多信息

- [完整文档](../README.md)
- [快速开始](../QUICK_START.md)
- [API文档](../README.md#api-文档) 