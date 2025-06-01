<?php
/**
 * OWhisper.NET PHP客户端示例
 * 演示如何使用PHP调用OWhisper.NET API进行音频转写
 */

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