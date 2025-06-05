# OWhisper.NET 快速开始

## 5分钟上手指南

### 1. 🚀 启动应用

```bash
# 下载并解压到任意目录
# 双击运行或命令行启动
OWhisper.NET.exe --debug

# 环境变量配置 (可选)
$env:OWHISPER_HOST = "0.0.0.0"    # 监听地址
$env:OWHISPER_PORT = "11899"      # 监听端口
```

### 2. 🎵 转写音频文件

**图形界面方式:**
1. 点击"选择文件" → 选择MP3/WAV/AAC音频
2. 点击"另存为" → 选择输出位置
   - 选择`.srt`文件 → 生成字幕文件
   - 选择`.txt`文件 → 生成纯文本
3. 点击"开始处理" → 等待完成

**命令行API方式:**
```bash
# Python 一行命令
curl -X POST http://localhost:11899/api/transcribe -F "file=@audio.mp3"
```

### 3. 📄 常用格式示例

**SRT字幕格式:**
```
1
00:00:00,000 --> 00:00:03,500
欢迎使用OWhisper音频转写工具

2
00:00:03,500 --> 00:00:08,200
支持多种音频格式转换为文本
```

**Python集成:**
```python
import requests
import os

# 支持环境变量配置
host = os.environ.get('OWHISPER_HOST', 'localhost')
port = os.environ.get('OWHISPER_PORT', '11899')
base_url = f"http://{host}:{port}"

# 转写文件
with open('audio.mp3', 'rb') as f:
    response = requests.post(
        f'{base_url}/api/transcribe',
        files={'file': f}
    )

result = response.json()
if result['Status'] == 'success':
    # 保存字幕文件
    with open('output.srt', 'w', encoding='utf-8') as f:
        f.write(result['Data']['SrtContent'])
    print("转写完成！")
```

### 4. ⚠️ 常见问题

| 问题 | 解决方案 |
|------|---------|
| 首次启动很慢 | 正在下载模型，请等待 |
| API连接失败 | 确保应用已启动且端口11899未被占用 |
| 音频格式错误 | 使用MP3、WAV或AAC格式 |
| 内存不足 | 处理大文件需要4GB+内存 |

### 5. 🔗 完整功能

查看完整文档: [README.md](README.md)

---

**💡 提示**: 首次运行需要下载1.5GB的模型文件，请确保网络连接稳定。 