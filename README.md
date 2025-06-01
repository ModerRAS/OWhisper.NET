# OWhisper.NET

<div align="center">

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

### 🖥️ 用户界面
- **图形界面**: 直观的Windows Forms界面
- **拖拽操作**: 简单易用的文件选择
- **格式预览**: 智能文件格式建议
- **进度显示**: 实时处理进度条

### 🔌 API服务
- **RESTful API**: 标准HTTP接口
- **多端支持**: 支持各种编程语言调用
- **状态监控**: 服务状态实时查询
- **错误处理**: 完善的错误信息返回
- **环境变量**: 支持OWHISPER_HOST、OWHISPER_PORT配置

## 📋 系统要求

- **操作系统**: Windows 10/11 (x64)
- **运行时**: .NET Framework 4.8
- **内存**: 建议 4GB+ RAM
- **存储**: 2GB+ 可用空间 (用于模型文件)
- **网络**: 首次运行需要网络连接下载模型

## 🚀 快速开始

### 1. 下载安装

```bash
# 克隆项目
git clone https://github.com/your-username/OWhisper.NET.git
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

## 📡 API 文档

### 基础信息

- **基础URL**: `http://localhost:11899` (可通过环境变量配置)
- **内容类型**: `application/json` (响应), `multipart/form-data` (上传)
- **字符编码**: UTF-8

### 环境变量配置

| 环境变量 | 默认值 | 说明 |
|---------|--------|------|
| `OWHISPER_HOST` | `0.0.0.0` | 监听地址 |
| `OWHISPER_PORT` | `11899` | 监听端口 |

### 响应格式

所有API响应都遵循统一格式：

```json
{
  "Status": "success|error",
  "Data": { /* 具体数据 */ },
  "Error": "错误信息",
  "ErrorCode": "错误代码"
}
```

### 接口列表

#### 1. 获取服务状态

```http
GET /api/status
```

**响应示例:**
```json
{
  "Status": "success",
  "Data": {
    "serviceStatus": "Running"
  },
  "Error": null,
  "ErrorCode": null
}
```

#### 2. 获取模型状态

```http
GET /api/model/status
```

**响应示例:**
```json
{
  "Status": "success",
  "Data": {
    "exists": true,
    "valid": true,
    "size": 1624555275,
    "path": "Models/ggml-large-v3-turbo.bin"
  },
  "Error": null,
  "ErrorCode": null
}
```

#### 3. 音频转写

```http
POST /api/transcribe
Content-Type: multipart/form-data
```

**请求参数:**
- `file`: 音频文件 (支持 .mp3, .wav, .aac)

**响应示例:**
```json
{
  "Status": "success",
  "Data": {
    "Text": "这是转写的文本内容",
    "SrtContent": "1\n00:00:00,000 --> 00:00:02,500\n这是转写的文本内容\n\n",
    "ProcessingTime": 15.6
  },
  "Error": null,
  "ErrorCode": null
}
```

#### 4. 启动服务

```http
POST /api/start
```

#### 5. 停止服务

```http
POST /api/stop
```

## 💻 编程语言调用示例

### Python

```python
import requests
import json
import os

class OWhisperClient:
    def __init__(self, base_url=None):
        # 支持环境变量配置
        if base_url is None:
            host = os.environ.get('OWHISPER_HOST', 'localhost')
            port = os.environ.get('OWHISPER_PORT', '11899')
            base_url = f"http://{host}:{port}"
        
        self.base_url = base_url
    
    def get_status(self):
        """获取服务状态"""
        response = requests.get(f"{self.base_url}/api/status")
        return response.json()
    
    def transcribe_file(self, file_path):
        """转写音频文件"""
        with open(file_path, 'rb') as f:
            files = {'file': f}
            response = requests.post(
                f"{self.base_url}/api/transcribe",
                files=files,
                timeout=1800  # 30分钟超时
            )
        return response.json()
    
    def transcribe_bytes(self, audio_bytes, filename):
        """转写音频字节数据"""
        files = {'file': (filename, audio_bytes)}
        response = requests.post(
            f"{self.base_url}/api/transcribe",
            files=files,
            timeout=1800
        )
        return response.json()

# 使用示例
if __name__ == "__main__":
    client = OWhisperClient()
    
    # 检查服务状态
    status = client.get_status()
    print(f"服务状态: {status}")
    
    # 转写音频文件
    result = client.transcribe_file("audio.mp3")
    if result["Status"] == "success":
        # 保存文本
        with open("output.txt", "w", encoding="utf-8") as f:
            f.write(result["Data"]["Text"])
        
        # 保存SRT字幕
        with open("output.srt", "w", encoding="utf-8") as f:
            f.write(result["Data"]["SrtContent"])
        
        print(f"转写完成，耗时: {result['Data']['ProcessingTime']:.1f}秒")
    else:
        print(f"转写失败: {result['Error']}")
```

### JavaScript (Node.js)

```javascript
const fs = require('fs');
const FormData = require('form-data');
const axios = require('axios');

class OWhisperClient {
    constructor(baseUrl = null) {
        // 支持环境变量配置
        if (baseUrl === null) {
            const host = process.env.OWHISPER_HOST || 'localhost';
            const port = process.env.OWHISPER_PORT || '11899';
            baseUrl = `http://${host}:${port}`;
        }
        
        this.baseUrl = baseUrl;
        this.client = axios.create({
            timeout: 1800000 // 30分钟超时
        });
    }

    async getStatus() {
        try {
            const response = await this.client.get(`${this.baseUrl}/api/status`);
            return response.data;
        } catch (error) {
            throw new Error(`获取状态失败: ${error.message}`);
        }
    }

    async transcribeFile(filePath) {
        try {
            const form = new FormData();
            form.append('file', fs.createReadStream(filePath));

            const response = await this.client.post(
                `${this.baseUrl}/api/transcribe`,
                form,
                {
                    headers: {
                        ...form.getHeaders(),
                    },
                }
            );

            return response.data;
        } catch (error) {
            throw new Error(`转写失败: ${error.message}`);
        }
    }

    async transcribeBuffer(audioBuffer, filename) {
        try {
            const form = new FormData();
            form.append('file', audioBuffer, filename);

            const response = await this.client.post(
                `${this.baseUrl}/api/transcribe`,
                form,
                {
                    headers: {
                        ...form.getHeaders(),
                    },
                }
            );

            return response.data;
        } catch (error) {
            throw new Error(`转写失败: ${error.message}`);
        }
    }
}

// 使用示例
async function main() {
    const client = new OWhisperClient();

    try {
        // 检查服务状态
        const status = await client.getStatus();
        console.log('服务状态:', status);

        // 转写音频文件
        const result = await client.transcribeFile('audio.mp3');
        
        if (result.Status === 'success') {
            // 保存结果
            fs.writeFileSync('output.txt', result.Data.Text, 'utf8');
            fs.writeFileSync('output.srt', result.Data.SrtContent, 'utf8');
            
            console.log(`转写完成，耗时: ${result.Data.ProcessingTime.toFixed(1)}秒`);
        } else {
            console.error('转写失败:', result.Error);
        }
    } catch (error) {
        console.error('错误:', error.message);
    }
}

main();
```

### C#

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class OWhisperClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OWhisperClient(string baseUrl = null)
    {
        // 支持环境变量配置
        if (baseUrl == null)
        {
            var host = Environment.GetEnvironmentVariable("OWHISPER_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("OWHISPER_PORT") ?? "11899";
            baseUrl = $"http://{host}:{port}";
        }
        
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
    }

    public async Task<ApiResponse<object>> GetStatusAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/status");
        var json = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<ApiResponse<object>>(json);
    }

    public async Task<ApiResponse<TranscriptionResult>> TranscribeFileAsync(string filePath)
    {
        using var form = new MultipartFormDataContent();
        var fileBytes = File.ReadAllBytes(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        
        // 设置Content-Type
        var extension = Path.GetExtension(filePath).ToLower();
        switch (extension)
        {
            case ".mp3":
                fileContent.Headers.ContentType = 
                    new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
                break;
            case ".wav":
                fileContent.Headers.ContentType = 
                    new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                break;
            case ".aac":
                fileContent.Headers.ContentType = 
                    new System.Net.Http.Headers.MediaTypeHeaderValue("audio/aac");
                break;
        }

        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/transcribe", form);
        var json = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<ApiResponse<TranscriptionResult>>(json);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// 模型类
public class ApiResponse<T>
{
    public string Status { get; set; }
    public T Data { get; set; }
    public string Error { get; set; }
    public string ErrorCode { get; set; }
}

public class TranscriptionResult
{
    public string Text { get; set; }
    public string SrtContent { get; set; }
    public double ProcessingTime { get; set; }
}

// 使用示例
class Program
{
    static async Task Main(string[] args)
    {
        using var client = new OWhisperClient();

        try
        {
            // 检查服务状态
            var status = await client.GetStatusAsync();
            Console.WriteLine($"服务状态: {status.Status}");

            // 转写音频文件
            var result = await client.TranscribeFileAsync("audio.mp3");
            
            if (result.Status == "success")
            {
                // 保存结果
                File.WriteAllText("output.txt", result.Data.Text, System.Text.Encoding.UTF8);
                File.WriteAllText("output.srt", result.Data.SrtContent, System.Text.Encoding.UTF8);
                
                Console.WriteLine($"转写完成，耗时: {result.Data.ProcessingTime:F1}秒");
            }
            else
            {
                Console.WriteLine($"转写失败: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
    }
}
```

### Java

```java
import okhttp3.*;
import com.google.gson.Gson;
import com.google.gson.JsonObject;
import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.concurrent.TimeUnit;

public class OWhisperClient {
    private final OkHttpClient client;
    private final String baseUrl;
    private final Gson gson;

    public OWhisperClient(String baseUrl) {
        // 支持环境变量配置
        if (baseUrl == null) {
            String host = System.getenv().getOrDefault("OWHISPER_HOST", "localhost");
            String port = System.getenv().getOrDefault("OWHISPER_PORT", "11899");
            baseUrl = String.format("http://%s:%s", host, port);
        }
        
        this.baseUrl = baseUrl;
        this.client = new OkHttpClient.Builder()
                .connectTimeout(30, TimeUnit.SECONDS)
                .writeTimeout(30, TimeUnit.MINUTES)
                .readTimeout(30, TimeUnit.MINUTES)
                .build();
        this.gson = new Gson();
    }

    public JsonObject getStatus() throws IOException {
        Request request = new Request.Builder()
                .url(baseUrl + "/api/status")
                .build();

        try (Response response = client.newCall(request).execute()) {
            String responseBody = response.body().string();
            return gson.fromJson(responseBody, JsonObject.class);
        }
    }

    public JsonObject transcribeFile(String filePath) throws IOException {
        File file = new File(filePath);
        
        // 确定Content-Type
        String contentType = "application/octet-stream";
        String fileName = file.getName().toLowerCase();
        if (fileName.endsWith(".mp3")) {
            contentType = "audio/mpeg";
        } else if (fileName.endsWith(".wav")) {
            contentType = "audio/wav";
        } else if (fileName.endsWith(".aac")) {
            contentType = "audio/aac";
        }

        RequestBody fileBody = RequestBody.create(
            MediaType.parse(contentType), 
            file
        );

        RequestBody requestBody = new MultipartBody.Builder()
                .setType(MultipartBody.FORM)
                .addFormDataPart("file", file.getName(), fileBody)
                .build();

        Request request = new Request.Builder()
                .url(baseUrl + "/api/transcribe")
                .post(requestBody)
                .build();

        try (Response response = client.newCall(request).execute()) {
            String responseBody = response.body().string();
            return gson.fromJson(responseBody, JsonObject.class);
        }
    }

    // 使用示例
    public static void main(String[] args) {
        OWhisperClient client = new OWhisperClient(null);

        try {
            // 检查服务状态
            JsonObject status = client.getStatus();
            System.out.println("服务状态: " + status);

            // 转写音频文件
            JsonObject result = client.transcribeFile("audio.mp3");
            
            if ("success".equals(result.get("Status").getAsString())) {
                JsonObject data = result.getAsJsonObject("Data");
                String text = data.get("Text").getAsString();
                String srtContent = data.get("SrtContent").getAsString();
                double processingTime = data.get("ProcessingTime").getAsDouble();

                // 保存结果
                Files.write(Paths.get("output.txt"), text.getBytes("UTF-8"));
                Files.write(Paths.get("output.srt"), srtContent.getBytes("UTF-8"));

                System.out.printf("转写完成，耗时: %.1f秒%n", processingTime);
            } else {
                System.out.println("转写失败: " + result.get("Error").getAsString());
            }
        } catch (Exception e) {
            System.out.println("错误: " + e.getMessage());
        }
    }
}
```

### PHP

```php
<?php
class OWhisperClient {
    private $baseUrl;
    private $timeout;

    public function __construct($baseUrl = null, $timeout = 1800) {
        // 支持环境变量配置
        if ($baseUrl === null) {
            $host = $_ENV['OWHISPER_HOST'] ?? 'localhost';
            $port = $_ENV['OWHISPER_PORT'] ?? '11899';
            $baseUrl = "http://{$host}:{$port}";
        }
        
        $this->baseUrl = rtrim($baseUrl, '/');
        $this->timeout = $timeout;
    }

    public function getStatus() {
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $this->baseUrl . '/api/status');
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_TIMEOUT, 30);
        
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode !== 200) {
            throw new Exception("HTTP错误: $httpCode");
        }

        return json_decode($response, true);
    }

    public function transcribeFile($filePath) {
        if (!file_exists($filePath)) {
            throw new Exception("文件不存在: $filePath");
        }

        $cfile = new CURLFile($filePath);
        
        // 设置MIME类型
        $extension = strtolower(pathinfo($filePath, PATHINFO_EXTENSION));
        switch ($extension) {
            case 'mp3':
                $cfile->setMimeType('audio/mpeg');
                break;
            case 'wav':
                $cfile->setMimeType('audio/wav');
                break;
            case 'aac':
                $cfile->setMimeType('audio/aac');
                break;
        }

        $postData = ['file' => $cfile];

        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $this->baseUrl . '/api/transcribe');
        curl_setopt($ch, CURLOPT_POST, true);
        curl_setopt($ch, CURLOPT_POSTFIELDS, $postData);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_TIMEOUT, $this->timeout);
        
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode !== 200) {
            throw new Exception("HTTP错误: $httpCode");
        }

        return json_decode($response, true);
    }
}

// 使用示例
try {
    $client = new OWhisperClient();

    // 检查服务状态
    $status = $client->getStatus();
    echo "服务状态: " . json_encode($status) . "\n";

    // 转写音频文件
    $result = $client->transcribeFile('audio.mp3');
    
    if ($result['Status'] === 'success') {
        // 保存结果
        file_put_contents('output.txt', $result['Data']['Text']);
        file_put_contents('output.srt', $result['Data']['SrtContent']);
        
        printf("转写完成，耗时: %.1f秒\n", $result['Data']['ProcessingTime']);
    } else {
        echo "转写失败: " . $result['Error'] . "\n";
    }
} catch (Exception $e) {
    echo "错误: " . $e->getMessage() . "\n";
}
?>
```

### Go

```go
package main

import (
    "bytes"
    "encoding/json"
    "fmt"
    "io"
    "mime/multipart"
    "net/http"
    "os"
    "path/filepath"
    "strings"
    "time"
)

type OWhisperClient struct {
    BaseURL string
    Client  *http.Client
}

type ApiResponse struct {
    Status    string      `json:"Status"`
    Data      interface{} `json:"Data"`
    Error     string      `json:"Error"`
    ErrorCode string      `json:"ErrorCode"`
}

type TranscriptionResult struct {
    Text           string  `json:"Text"`
    SrtContent     string  `json:"SrtContent"`
    ProcessingTime float64 `json:"ProcessingTime"`
}

func NewOWhisperClient(baseURL string) *OWhisperClient {
    // 支持环境变量配置
    if baseURL == "" {
        host := os.Getenv("OWHISPER_HOST")
        if host == "" {
            host = "localhost"
        }
        port := os.Getenv("OWHISPER_PORT")
        if port == "" {
            port = "11899"
        }
        baseURL = fmt.Sprintf("http://%s:%s", host, port)
    }
    
    return &OWhisperClient{
        BaseURL: strings.TrimRight(baseURL, "/"),
        Client: &http.Client{
            Timeout: 30 * time.Minute,
        },
    }
}

func (c *OWhisperClient) GetStatus() (*ApiResponse, error) {
    resp, err := c.Client.Get(c.BaseURL + "/api/status")
    if err != nil {
        return nil, err
    }
    defer resp.Body.Close()

    var result ApiResponse
    if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
        return nil, err
    }

    return &result, nil
}

func (c *OWhisperClient) TranscribeFile(filePath string) (*ApiResponse, error) {
    file, err := os.Open(filePath)
    if err != nil {
        return nil, err
    }
    defer file.Close()

    var body bytes.Buffer
    writer := multipart.NewWriter(&body)

    // 确定Content-Type
    contentType := "application/octet-stream"
    ext := strings.ToLower(filepath.Ext(filePath))
    switch ext {
    case ".mp3":
        contentType = "audio/mpeg"
    case ".wav":
        contentType = "audio/wav"
    case ".aac":
        contentType = "audio/aac"
    }

    part, err := writer.CreateFormFile("file", filepath.Base(filePath))
    if err != nil {
        return nil, err
    }

    if _, err := io.Copy(part, file); err != nil {
        return nil, err
    }

    if err := writer.Close(); err != nil {
        return nil, err
    }

    req, err := http.NewRequest("POST", c.BaseURL+"/api/transcribe", &body)
    if err != nil {
        return nil, err
    }
    req.Header.Set("Content-Type", writer.FormDataContentType())

    resp, err := c.Client.Do(req)
    if err != nil {
        return nil, err
    }
    defer resp.Body.Close()

    var result ApiResponse
    if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
        return nil, err
    }

    return &result, nil
}

func main() {
    client := NewOWhisperClient("")

    // 检查服务状态
    status, err := client.GetStatus()
    if err != nil {
        fmt.Printf("获取状态失败: %v\n", err)
        return
    }
    fmt.Printf("服务状态: %+v\n", status)

    // 转写音频文件
    result, err := client.TranscribeFile("audio.mp3")
    if err != nil {
        fmt.Printf("转写失败: %v\n", err)
        return
    }

    if result.Status == "success" {
        // 解析数据
        dataBytes, _ := json.Marshal(result.Data)
        var transcription TranscriptionResult
        json.Unmarshal(dataBytes, &transcription)

        // 保存结果
        os.WriteFile("output.txt", []byte(transcription.Text), 0644)
        os.WriteFile("output.srt", []byte(transcription.SrtContent), 0644)

        fmt.Printf("转写完成，耗时: %.1f秒\n", transcription.ProcessingTime)
    } else {
        fmt.Printf("转写失败: %s\n", result.Error)
    }
}
```

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
git clone https://github.com/your-username/OWhisper.NET.git

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

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

- [Whisper.net](https://github.com/sandrohanea/whisper.net) - 核心语音识别库
- [OpenAI Whisper](https://github.com/openai/whisper) - 原始模型
- [NAudio](https://github.com/naudio/NAudio) - 音频处理库

---

<div align="center">

**如果这个项目对你有帮助，请给一个 ⭐️**

[报告问题](https://github.com/your-username/OWhisper.NET/issues) · [功能请求](https://github.com/your-username/OWhisper.NET/issues) · [贡献代码](https://github.com/your-username/OWhisper.NET/pulls)

</div>