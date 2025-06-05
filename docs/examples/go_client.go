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

// OWhisper.NET Go客户端示例
// 演示如何使用Go调用OWhisper.NET API进行音频转写

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
    case ".m4a":
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