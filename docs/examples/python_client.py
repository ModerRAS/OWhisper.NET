#!/usr/bin/env python3
"""
OWhisper.NET Python客户端示例
演示如何使用Python调用OWhisper.NET API进行音频转写
"""

import requests
import json
import time
import os
from pathlib import Path

class OWhisperClient:
    def __init__(self, base_url=None):
        # 支持环境变量配置
        if base_url is None:
            host = os.environ.get('OWHISPER_HOST', 'localhost')
            port = os.environ.get('OWHISPER_PORT', '11899')
            base_url = f"http://{host}:{port}"
        
        self.base_url = base_url
        self.session = requests.Session()
        
    def wait_for_service(self, timeout=60):
        """等待服务启动"""
        print(f"等待OWhisper.NET服务启动... ({self.base_url})")
        start_time = time.time()
        
        while time.time() - start_time < timeout:
            try:
                response = self.session.get(f"{self.base_url}/api/status", timeout=5)
                if response.status_code == 200:
                    print("✅ 服务已启动")
                    return True
            except requests.RequestException:
                pass
            
            print("⏳ 等待服务启动中...")
            time.sleep(2)
        
        print("❌ 服务启动超时")
        return False
    
    def get_status(self):
        """获取服务状态"""
        try:
            response = self.session.get(f"{self.base_url}/api/status")
            response.raise_for_status()
            return response.json()
        except requests.RequestException as e:
            print(f"获取状态失败: {e}")
            return None
    
    def get_model_status(self):
        """获取模型状态"""
        try:
            response = self.session.get(f"{self.base_url}/api/model/status")
            response.raise_for_status()
            return response.json()
        except requests.RequestException as e:
            print(f"获取模型状态失败: {e}")
            return None
    
    def transcribe_file(self, file_path, progress_callback=None):
        """转写音频文件"""
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"文件不存在: {file_path}")
        
        file_size = os.path.getsize(file_path)
        print(f"📁 文件: {file_path}")
        print(f"📏 大小: {file_size / 1024 / 1024:.1f} MB")
        
        with open(file_path, 'rb') as f:
            files = {'file': f}
            
            print("🚀 开始转写...")
            start_time = time.time()
            
            try:
                response = self.session.post(
                    f"{self.base_url}/api/transcribe",
                    files=files,
                    timeout=1800  # 30分钟超时
                )
                response.raise_for_status()
                
                result = response.json()
                processing_time = time.time() - start_time
                
                print(f"⏱️  总耗时: {processing_time:.1f}秒")
                
                return result
                
            except requests.RequestException as e:
                print(f"转写失败: {e}")
                return None
    
    def save_results(self, result, output_dir="output"):
        """保存转写结果"""
        if not result or result.get("Status") != "success":
            print("❌ 没有有效的转写结果")
            return False
        
        os.makedirs(output_dir, exist_ok=True)
        
        data = result["Data"]
        timestamp = int(time.time())
        
        # 保存文本文件
        txt_file = os.path.join(output_dir, f"transcription_{timestamp}.txt")
        with open(txt_file, 'w', encoding='utf-8') as f:
            f.write(data["Text"])
        print(f"💾 文本已保存: {txt_file}")
        
        # 保存SRT字幕文件
        srt_file = os.path.join(output_dir, f"transcription_{timestamp}.srt")
        with open(srt_file, 'w', encoding='utf-8') as f:
            f.write(data["SrtContent"])
        print(f"💾 字幕已保存: {srt_file}")
        
        # 保存JSON结果
        json_file = os.path.join(output_dir, f"transcription_{timestamp}.json")
        with open(json_file, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        print(f"💾 JSON已保存: {json_file}")
        
        print(f"🎯 处理时间: {data['ProcessingTime']:.1f}秒")
        
        return True

def main():
    """主函数示例"""
    # 显示配置信息
    host = os.environ.get('OWHISPER_HOST', 'localhost')
    port = os.environ.get('OWHISPER_PORT', '11899')
    print(f"🔧 连接配置: {host}:{port}")
    print(f"💡 可通过环境变量配置: OWHISPER_HOST, OWHISPER_PORT")
    
    client = OWhisperClient()
    
    # 等待服务启动
    if not client.wait_for_service():
        print("无法连接到OWhisper.NET服务，请确保应用已启动")
        print("启动命令: OWhisper.NET.exe --debug")
        return
    
    # 检查服务状态
    status = client.get_status()
    if status:
        print(f"📊 服务状态: {json.dumps(status, indent=2, ensure_ascii=False)}")
    
    # 检查模型状态
    model_status = client.get_model_status()
    if model_status:
        print(f"🤖 模型状态: {json.dumps(model_status, indent=2, ensure_ascii=False)}")
    
    # 示例音频文件路径
    audio_files = [
        "sample.mp3",
        "test.wav", 
        "audio.aac"
    ]
    
    # 查找可用的音频文件
    available_files = [f for f in audio_files if os.path.exists(f)]
    
    if not available_files:
        print("⚠️  未找到示例音频文件")
        print("请将音频文件放在当前目录下，支持格式: .mp3, .wav, .aac")
        return
    
    # 转写第一个可用文件
    audio_file = available_files[0]
    result = client.transcribe_file(audio_file)
    
    if result:
        client.save_results(result)
        print("✅ 转写完成!")
    else:
        print("❌ 转写失败")

if __name__ == "__main__":
    main() 