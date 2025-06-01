# 任务队列和SSE实时进度功能

## 概述

OWhisper.NET v2.0 引入了全新的任务队列系统和Server-Sent Events (SSE) 实时进度功能，提供了更好的多任务处理能力和用户体验。

## 🎯 核心特性

### 1. 任务队列系统
- **串行处理**: 多个转录任务按顺序执行，避免资源冲突
- **状态管理**: 完整的任务生命周期管理
- **队列控制**: 支持任务取消和优先级管理
- **内存管理**: 自动清理已完成任务的音频数据

### 2. Server-Sent Events (SSE)
- **实时进度**: 通过SSE推送转录进度更新
- **状态通知**: 实时接收任务状态变化
- **心跳检测**: 保持连接活跃状态
- **队列监控**: 监听整个任务队列状态

## 🔄 任务状态流程

```
提交任务 → Queued (排队中) → Processing (处理中) → Completed (已完成)
                ↓                      ↓
            Cancelled (已取消)      Failed (失败)
```

### 状态说明

| 状态 | 描述 | 可操作 |
|------|------|--------|
| `Queued` | 任务已提交，等待处理 | ✅ 可取消 |
| `Processing` | 正在转录处理中 | ❌ 无法取消 |
| `Completed` | 转录完成 | ✅ 获取结果 |
| `Failed` | 处理失败 | ✅ 查看错误 |
| `Cancelled` | 已被取消 | ❌ 无法恢复 |

## 📡 API 接口

### 任务管理接口

#### 提交转录任务
```http
POST /api/transcribe
Content-Type: multipart/form-data

file: [音频文件]
```

**响应:**
```json
{
  "status": "success",
  "data": {
    "taskId": "abc123def456",
    "queuePosition": 1
  }
}
```

#### 获取任务详情
```http
GET /api/tasks/{taskId}
```

#### 获取所有任务
```http
GET /api/tasks
```

#### 取消任务
```http
POST /api/tasks/{taskId}/cancel
```

### SSE 接口

#### 监听任务进度
```http
GET /api/tasks/{taskId}/progress
Accept: text/event-stream
```

**事件流示例:**
```
event: progress
data: {"taskId":"abc123","status":"Queued","progress":0,"queuePosition":2}

event: progress  
data: {"taskId":"abc123","status":"Processing","progress":25.5,"queuePosition":0}

event: progress
data: {"taskId":"abc123","status":"Completed","progress":100,"result":{...}}

event: heartbeat
data: {"timestamp":"2024-01-01T10:05:00Z"}
```

#### 监听队列状态
```http
GET /api/queue/progress
Accept: text/event-stream
```

## 💻 客户端实现

### C# 客户端示例

```csharp
using var client = new OWhisperApiClient();

// 提交任务
var taskResponse = await client.SubmitTranscriptionTaskAsync("audio.mp3");
Console.WriteLine($"任务ID: {taskResponse.TaskId}");

// 监听进度
var result = await client.MonitorTaskProgressAsync(taskResponse.TaskId, progress => {
    Console.WriteLine($"状态: {progress.Status}, 进度: {progress.Progress}%");
});

Console.WriteLine($"转录完成: {result.Text}");
```

### JavaScript 客户端示例

```javascript
// 提交任务
const response = await fetch('/api/transcribe', {
    method: 'POST',
    body: formData
});
const { data: { taskId } } = await response.json();

// 监听进度
const eventSource = new EventSource(`/api/tasks/${taskId}/progress`);
eventSource.onmessage = (event) => {
    const progress = JSON.parse(event.data);
    console.log(`状态: ${progress.status}, 进度: ${progress.progress}%`);
    
    if (progress.status === 'Completed') {
        console.log('转录完成:', progress.result.text);
        eventSource.close();
    }
};
```

## 🏗️ 架构设计

### 核心组件

1. **TranscriptionQueueService**: 单例服务，管理任务队列
2. **TranscriptionTask**: 任务模型，包含状态和进度信息
3. **SseController**: SSE控制器，处理实时通信
4. **WhisperController**: 主API控制器，处理任务CRUD操作

### 设计模式

- **单例模式**: TranscriptionQueueService 确保全局唯一
- **观察者模式**: 进度更新事件通知
- **状态机模式**: 任务状态转换管理
- **生产者-消费者模式**: 任务队列处理

## 🔧 配置和优化

### 队列配置

```csharp
// 在 TranscriptionQueueService 中可配置的参数
private const int MAX_CONCURRENT_TASKS = 1;  // 并发任务数
private const int QUEUE_CHECK_INTERVAL = 500; // 队列检查间隔(ms)
private const int HEARTBEAT_INTERVAL = 2000;  // 心跳间隔(ms)
```

### 性能优化建议

1. **内存管理**: 及时清理已完成任务的音频数据
2. **连接管理**: 合理设置SSE连接超时时间
3. **队列大小**: 根据服务器资源限制队列长度
4. **并发控制**: 根据硬件性能调整并发任务数

## 🧪 测试

### 单元测试

```bash
# 运行所有测试
dotnet test

# 运行特定测试
dotnet test --filter "TestCategory!=LongRunning"

# 运行SSE测试
dotnet test --filter "SSE"
```

### 集成测试

项目包含完整的集成测试，覆盖：
- 任务提交和状态查询
- SSE连接和进度监听
- 任务取消功能
- 错误处理场景

## 🚀 升级指南

### 从 v1.x 升级

1. **API变更**: `/api/transcribe` 现在返回任务ID而不是直接结果
2. **新增接口**: 添加了任务管理和SSE接口
3. **客户端更新**: 需要更新客户端代码以支持异步任务处理

### 兼容性

- 保持向后兼容的API响应格式
- 新功能为可选，不影响现有集成
- 提供了新旧两种客户端示例

## 📚 相关文档

- [API 接口文档](../README.md#-api-接口)
- [客户端示例](../examples/)
- [配置说明](../README.md#-配置说明)
- [故障排除](../README.md#-故障排除) 