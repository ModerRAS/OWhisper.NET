#!/usr/bin/env node
/**
 * OWhisper.NET Node.js客户端示例
 * 演示如何使用Node.js调用OWhisper.NET API进行音频转写
 */

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

// 如果直接运行此脚本
if (require.main === module) {
    main().catch(console.error);
}

module.exports = OWhisperClient; 