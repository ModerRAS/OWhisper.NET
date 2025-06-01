#!/usr/bin/env node
/**
 * OWhisper.NET Node.js客户端示例
 * 演示如何使用Node.js调用OWhisper.NET API进行音频转写
 */

const fs = require('fs');
const path = require('path');
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

    async waitForService(timeout = 60000) {
        console.log(`等待OWhisper.NET服务启动... (${this.baseUrl})`);
        const startTime = Date.now();

        while (Date.now() - startTime < timeout) {
            try {
                const response = await this.client.get(`${this.baseUrl}/api/status`, {
                    timeout: 5000
                });
                if (response.status === 200) {
                    console.log('✅ 服务已启动');
                    return true;
                }
            } catch (error) {
                // 忽略连接错误
            }

            console.log('⏳ 等待服务启动中...');
            await new Promise(resolve => setTimeout(resolve, 2000));
        }

        console.log('❌ 服务启动超时');
        return false;
    }

    async getStatus() {
        try {
            const response = await this.client.get(`${this.baseUrl}/api/status`);
            return response.data;
        } catch (error) {
            console.error(`获取状态失败: ${error.message}`);
            return null;
        }
    }

    async getModelStatus() {
        try {
            const response = await this.client.get(`${this.baseUrl}/api/model/status`);
            return response.data;
        } catch (error) {
            console.error(`获取模型状态失败: ${error.message}`);
            return null;
        }
    }

    async transcribeFile(filePath) {
        if (!fs.existsSync(filePath)) {
            throw new Error(`文件不存在: ${filePath}`);
        }

        const stats = fs.statSync(filePath);
        console.log(`📁 文件: ${filePath}`);
        console.log(`📏 大小: ${(stats.size / 1024 / 1024).toFixed(1)} MB`);

        const form = new FormData();
        form.append('file', fs.createReadStream(filePath));

        console.log('🚀 开始转写...');
        const startTime = Date.now();

        try {
            const response = await this.client.post(
                `${this.baseUrl}/api/transcribe`,
                form,
                {
                    headers: {
                        ...form.getHeaders(),
                    },
                }
            );

            const processingTime = (Date.now() - startTime) / 1000;
            console.log(`⏱️  总耗时: ${processingTime.toFixed(1)}秒`);

            return response.data;
        } catch (error) {
            console.error(`转写失败: ${error.message}`);
            return null;
        }
    }

    async saveResults(result, outputDir = 'output') {
        if (!result || result.Status !== 'success') {
            console.log('❌ 没有有效的转写结果');
            return false;
        }

        if (!fs.existsSync(outputDir)) {
            fs.mkdirSync(outputDir, { recursive: true });
        }

        const data = result.Data;
        const timestamp = Math.floor(Date.now() / 1000);

        // 保存文本文件
        const txtFile = path.join(outputDir, `transcription_${timestamp}.txt`);
        fs.writeFileSync(txtFile, data.Text, 'utf8');
        console.log(`💾 文本已保存: ${txtFile}`);

        // 保存SRT字幕文件
        const srtFile = path.join(outputDir, `transcription_${timestamp}.srt`);
        fs.writeFileSync(srtFile, data.SrtContent, 'utf8');
        console.log(`💾 字幕已保存: ${srtFile}`);

        // 保存JSON结果
        const jsonFile = path.join(outputDir, `transcription_${timestamp}.json`);
        fs.writeFileSync(jsonFile, JSON.stringify(result, null, 2), 'utf8');
        console.log(`💾 JSON已保存: ${jsonFile}`);

        console.log(`🎯 处理时间: ${data.ProcessingTime.toFixed(1)}秒`);

        return true;
    }
}

async function main() {
    // 显示配置信息
    const host = process.env.OWHISPER_HOST || 'localhost';
    const port = process.env.OWHISPER_PORT || '11899';
    console.log(`🔧 连接配置: ${host}:${port}`);
    console.log(`💡 可通过环境变量配置: OWHISPER_HOST, OWHISPER_PORT`);
    
    const client = new OWhisperClient();

    // 等待服务启动
    if (!(await client.waitForService())) {
        console.log('无法连接到OWhisper.NET服务，请确保应用已启动');
        console.log('启动命令: OWhisper.NET.exe --debug');
        return;
    }

    // 检查服务状态
    const status = await client.getStatus();
    if (status) {
        console.log(`📊 服务状态: ${JSON.stringify(status, null, 2)}`);
    }

    // 检查模型状态
    const modelStatus = await client.getModelStatus();
    if (modelStatus) {
        console.log(`🤖 模型状态: ${JSON.stringify(modelStatus, null, 2)}`);
    }

    // 示例音频文件路径
    const audioFiles = [
        'sample.mp3',
        'test.wav',
        'audio.aac'
    ];

    // 查找可用的音频文件
    const availableFiles = audioFiles.filter(f => fs.existsSync(f));

    if (availableFiles.length === 0) {
        console.log('⚠️  未找到示例音频文件');
        console.log('请将音频文件放在当前目录下，支持格式: .mp3, .wav, .aac');
        return;
    }

    // 转写第一个可用文件
    const audioFile = availableFiles[0];
    const result = await client.transcribeFile(audioFile);

    if (result) {
        await client.saveResults(result);
        console.log('✅ 转写完成!');
    } else {
        console.log('❌ 转写失败');
    }
}

// 如果直接运行此脚本
if (require.main === module) {
    main().catch(console.error);
}

module.exports = OWhisperClient; 