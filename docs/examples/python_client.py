#!/usr/bin/env python3
"""
OWhisper.NET Python客户端示例
演示如何使用Python调用OWhisper.NET API进行音频转写
"""

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