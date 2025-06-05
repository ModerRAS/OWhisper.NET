# OWhisper.NET 文本润色功能使用指南

## 概述

OWhisper.NET 现在支持通过 Deepseek API 对转录结果进行文本润色，使用 Scriban 模板引擎来处理专业领域的系统提示词。

## 功能特性

- ✅ 支持多种润色模板（通用、学术、商务等）
- ✅ 使用 Scriban 模板引擎，支持自定义提示词
- ✅ 支持 Deepseek 多种模型（deepseek-chat、deepseek-coder）
- ✅ 自动生成润色后的 SRT 字幕文件
- ✅ 保留原始转录结果作为备份
- ✅ 配置持久化存储
- ✅ API 连接测试功能

## 使用步骤

### 1. 配置 API Key

1. 在主界面的"文本润色设置"区域中，勾选"启用文本润色"
2. 输入您的 Deepseek API Key
3. 点击"测试连接"按钮验证 API 连接

### 2. 选择模型和模板

1. **模型选择**：
   - `deepseek-chat`：适用于通用文本润色
   - `deepseek-coder`：适用于技术文档润色

2. **模板选择**：
   - **通用润色**：适用于大多数场景的通用文本润色
   - **学术/专业润色**：适用于学术论文、专业报告等正式文档
   - **商务沟通润色**：适用于商务邮件、报告、提案等商业文档

### 3. 开始转录和润色

1. 选择音频文件
2. 设置输出路径
3. 确保"启用文本润色"已勾选
4. 点击"开始处理"

### 4. 查看结果

处理完成后，您将获得：
- **润色后的文件**：保存在您指定的路径
- **原始转录文件**：保存为 `.original.srt` 或 `.original.txt`
- **处理统计**：包括转录耗时、润色耗时、Token 消耗等信息

## 自定义模板

### 模板位置

模板文件存储在：`%APPDATA%\OWhisper.NET\PolishingTemplates\`

点击"高级设置"按钮可以直接打开模板目录。

### 模板结构

模板使用 JSON 格式，包含以下字段：

```json
{
  "Name": "custom_template",
  "DisplayName": "自定义模板",
  "Description": "这是一个自定义的润色模板",
  "Category": "自定义",
  "SystemPromptTemplate": "你是一个专业的文本润色助手...",
  "UserMessageTemplate": "请对以下文本进行润色：\n\n{{ original_text }}",
  "Parameters": [
    {
      "Name": "original_text",
      "DisplayName": "原始文本",
      "Description": "需要润色的原始文本",
      "Type": "string",
      "IsRequired": true
    }
  ],
  "Version": "1.0.0",
  "IsEnabled": true
}
```

### Scriban 模板语法

模板支持 Scriban 语法，可以使用以下功能：

- **变量替换**：`{{ variable_name }}`
- **条件判断**：`{{ if condition }} ... {{ end }}`
- **循环**：`{{ for item in items }} ... {{ end }}`
- **默认值**：`{{ variable || "default_value" }}`

### 示例模板

#### 医疗领域模板

```json
{
  "Name": "medical",
  "DisplayName": "医疗专业润色",
  "Description": "适用于医疗相关文档的专业润色模板",
  "Category": "医疗",
  "SystemPromptTemplate": "你是一个医疗文档润色专家。请对用户提供的医疗相关文本进行专业润色，要求：\n\n1. 使用准确的医学术语\n2. 确保表达的严谨性和科学性\n3. 保持医疗文档的专业格式\n4. 纠正医学术语的使用错误\n5. 提升文本的可读性和专业性\n\n{{ if specialty }}\n专业领域：{{ specialty }}\n{{ end }}\n\n请直接返回润色后的文本，保持医疗文档的专业性。",
  "UserMessageTemplate": "请对以下医疗文本进行专业润色：\n\n{{ original_text }}",
  "Parameters": [
    {
      "Name": "original_text",
      "DisplayName": "原始文本",
      "Type": "string",
      "IsRequired": true
    },
    {
      "Name": "specialty",
      "DisplayName": "医学专业",
      "Description": "具体的医学专业领域",
      "Type": "string",
      "DefaultValue": "内科"
    }
  ]
}
```

#### 法律领域模板

```json
{
  "Name": "legal",
  "DisplayName": "法律文档润色",
  "Description": "适用于法律文档的专业润色模板",
  "Category": "法律",
  "SystemPromptTemplate": "你是一个法律文档润色专家。请对用户提供的法律相关文本进行专业润色，要求：\n\n1. 使用准确的法律术语\n2. 确保逻辑严密和表达准确\n3. 保持法律文档的正式性\n4. 优化条款结构和语言表达\n5. 确保符合法律文书的规范\n\n{{ if document_type }}\n文档类型：{{ document_type }}\n{{ end }}\n\n请直接返回润色后的文本，保持法律文档的专业性和严谨性。",
  "UserMessageTemplate": "请对以下法律文本进行专业润色：\n\n{{ original_text }}",
  "Parameters": [
    {
      "Name": "original_text",
      "DisplayName": "原始文本",
      "Type": "string",
      "IsRequired": true
    },
    {
      "Name": "document_type",
      "DisplayName": "文档类型",
      "Type": "enum",
      "Options": ["合同", "协议", "法律意见书", "起诉书", "答辩书", "其他"],
      "DefaultValue": "合同"
    }
  ]
}
```

## 配置说明

### API 设置

- **API Key**：您的 Deepseek API 密钥
- **API 基地址**：默认为 `https://api.deepseek.com/v1`
- **最大 Token 数**：默认为 4000
- **温度参数**：默认为 0.7，控制输出的随机性

### 文件输出

- 启用润色时，会生成两个文件：
  - 主文件：润色后的内容
  - 备份文件：原始转录内容（文件名添加 `.original` 后缀）

## 注意事项

1. **API 费用**：使用 Deepseek API 会产生费用，请注意 Token 消耗
2. **网络连接**：需要稳定的网络连接访问 Deepseek API
3. **处理时间**：润色过程需要额外时间，具体取决于文本长度和网络状况
4. **模板语法**：自定义模板时请确保 Scriban 语法正确
5. **API 限制**：请遵守 Deepseek API 的使用限制和频率限制

## 故障排除

### 常见问题

1. **API 连接失败**
   - 检查 API Key 是否正确
   - 确认网络连接正常
   - 验证 API 基地址设置

2. **润色失败**
   - 检查文本长度是否超过限制
   - 确认模板语法是否正确
   - 查看错误信息进行诊断

3. **模板加载失败**
   - 检查模板 JSON 格式是否正确
   - 确认模板文件位置是否正确
   - 验证模板参数定义

### 日志查看

程序运行日志可以帮助诊断问题，日志文件位置：
- Windows：`%APPDATA%\OWhisper.NET\Logs\`

## 技术支持

如果您在使用过程中遇到问题，请：

1. 查看程序日志文件
2. 检查模板语法和配置
3. 验证 API 连接和设置
4. 提交 Issue 到项目仓库

---

*本功能基于 Deepseek API 和 Scriban 模板引擎实现，感谢这些优秀的开源项目。* 