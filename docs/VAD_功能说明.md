# VAD (语音活动检测) 功能说明

## 概述

VAD (Voice Activity Detection) 是 OWhisper.NET 的核心功能之一，用于智能分析音频中的语音活动区域，减少 Whisper 幻觉现象，提高转录效果。

## 主要功能

### 1. 语音活动检测
- **能量分析**: 基于音频能量级别检测语音活动
- **智能分段**: 自动识别并分割语音与静音区域
- **噪音过滤**: 过滤低能量的背景噪音
- **动态阈值**: 根据音频特征自适应调整检测阈值

### 2. 音频分段处理
- **时间戳保留**: 保持原始音频的时间戳信息
- **无缝合并**: 将分段转录结果重新组合为完整字幕
- **格式支持**: 支持 SRT 格式输出
- **高效处理**: 只转录有语音的部分，节省处理时间

## 核心组件

### VoiceActivityDetector 类
```csharp
// 处理音频并生成分段
var segments = AudioProcessor.ProcessAudioWithVad(audioData, enableVad, vadSettings);

// 配置VAD参数
var vadSettings = new VadSettings
{
    EnergyThreshold = 0.001,     // 能量阈值
    MinSpeechDuration = 0.5,     // 最小语音时长（秒）
    MinSilenceDuration = 1.0,    // 最小静音时长（秒）
    WindowSize = 1024,           // 窗口大小
    HopSize = 512               // 跳跃大小
};
```

### AudioSegment 类
表示检测到的音频分段：
```csharp
public class AudioSegment
{
    public TimeSpan StartTime { get; set; }      // 开始时间
    public TimeSpan Duration { get; set; }       // 持续时间
    public bool IsSpeech { get; set; }          // 是否为语音
    public double Energy { get; set; }          // 平均能量
    public byte[] AudioData { get; set; }       // 音频数据
}
```

## 使用方法

### 1. API 调用
```bash
# 启用VAD（默认）
curl -X POST http://localhost:5000/api/transcribe \
  -F "file=@audio.wav" \
  -F "enable_vad=true"

# 禁用VAD
curl -X POST http://localhost:5000/api/transcribe \
  -F "file=@audio.wav" \
  -F "enable_vad=false"
```

### 2. C# 代码调用
```csharp
var whisperService = WhisperService.Instance;

// 使用VAD转录
var result = await whisperService.Transcribe(audioData, enableVad: true);

// 自定义VAD设置
var vadSettings = new VadSettings
{
    EnergyThreshold = 0.002,
    MinSpeechDuration = 1.0
};
var result = await whisperService.Transcribe(audioData, enableVad: true, vadSettings);
```

### 3. GUI 界面
- 桌面应用自动启用 VAD 功能
- 可在设置中调整 VAD 参数
- 实时显示分段处理进度

## 配置参数

| 参数名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| EnergyThreshold | double | 0.001 | 能量阈值，低于此值视为静音 |
| MinSpeechDuration | double | 0.5 | 最小语音分段时长（秒） |
| MinSilenceDuration | double | 1.0 | 最小静音分段时长（秒） |
| WindowSize | int | 1024 | 分析窗口大小（样本数） |
| HopSize | int | 512 | 窗口跳跃大小（样本数） |

## 性能优化

### 智能启用策略
- 音频时长 ≤ 30秒：不使用VAD，直接转录
- 音频时长 > 30秒：自动启用VAD分段处理

### 处理流程
1. **音频预处理**: 格式验证、时长检测
2. **VAD分析**: 能量分析、分段检测
3. **分段提取**: 生成独立的音频片段
4. **并行转录**: 使用Whisper处理各分段
5. **结果合并**: 重建完整的转录结果

## 效果展示

### 原始转录（无VAD）
```
可能包含重复内容、幻觉文本...
```

### VAD优化后
```
清晰准确的语音内容，去除静音区域的幻觉
```

## 技术实现

### 能量检测算法
- **RMS能量计算**: 使用均方根计算音频能量
- **滑动窗口**: 平滑能量变化曲线
- **动态阈值**: 基于音频统计特征调整

### WAV文件处理
- **标准格式**: 生成符合WAV标准的音频文件
- **元数据保留**: 保持采样率、位深度等信息
- **内存优化**: 高效的音频数据处理

### 时间戳同步
- **精确对齐**: 保持毫秒级时间戳精度
- **SRT格式**: 标准字幕格式输出
- **时间偏移**: 自动计算分段时间偏移

## 故障排除

### 常见问题

1. **VAD分段过多**
   - 降低 `EnergyThreshold`
   - 增加 `MinSpeechDuration`

2. **遗漏语音片段**
   - 提高 `EnergyThreshold`
   - 减少 `MinSilenceDuration`

3. **处理时间过长**
   - 优化分段参数
   - 考虑禁用VAD（短音频）

### 调试信息
系统会在日志中记录：
- VAD检测到的分段数量
- 每个分段的处理时间
- 能量分布统计信息

## 版本历史

### v1.2.0 (2025-06-05) - 效率大幅优化
**重大性能提升**
- **Whisper实例复用**: 不再每次分段都重新初始化Whisper，大幅减少处理时间
- **智能初始化**: 只在首次调用或模型变更时初始化Whisper实例
- **内存优化**: 改进了WhisperManager的资源管理
- **性能提升**: VAD分段处理效率提升约80%，从每分段5-6秒初始化时间降至毫秒级

**技术改进**
- 添加了`EnsureInitializedAsync()`方法，支持实例复用
- 使用双重检查锁定模式确保线程安全
- 优化了资源清理机制
- 改进了异常处理和错误恢复

### v1.1.1 (2025-06-05) - WAV格式修复
**修复内容**
- 修复了VAD生成的WAV文件格式问题
- 解决了"Invalid wave chunk size"错误
- 改进了`CreateSegment`方法的WAV文件生成逻辑
- 添加了`FindDataChunkOffset`和`CreateWavFile`方法
- 完善了音频数据提取和文件大小计算

### v1.1.0 (2025-06-05) - 稳定性提升
**修复内容**
- 修复了`System.OverflowException`错误
- 解决了`Math.Abs(Int16.MinValue)`溢出问题
- 改进了能量计算中的数据类型处理
- 使用`long`类型避免整数溢出
- 添加了`using System.Text`支持

### v1.0.0 (2025-06-05) - 初始版本
**核心功能**
- 基于能量的语音活动检测
- 智能音频分段处理
- Whisper集成和结果合并
- SRT字幕格式支持
- 配置参数自定义
- API和GUI界面支持

## 支持的音频格式

- **WAV**: 推荐格式，最佳兼容性
- **MP3**: 广泛支持的压缩格式
- **AAC/M4A**: 现代音频格式
- **采样率**: 支持16kHz、44.1kHz、48kHz等
- **位深度**: 16位、24位、32位

## 最佳实践

1. **长音频文件**: 建议启用VAD以提高效率和准确性
2. **短音频片段**: 可禁用VAD以减少处理开销
3. **噪音环境**: 适当调高能量阈值
4. **清晰录音**: 可使用较低的能量阈值以捕获更多细节
5. **实时应用**: 考虑使用较小的窗口大小以降低延迟

## 开发者API

### 核心接口
```csharp
// VAD设置
public class VadSettings
{
    public double EnergyThreshold { get; set; } = 0.001;
    public double MinSpeechDuration { get; set; } = 0.5;
    public double MinSilenceDuration { get; set; } = 1.0;
    public int WindowSize { get; set; } = 1024;
    public int HopSize { get; set; } = 512;
}

// 音频分段
public class AudioSegment
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsSpeech { get; set; }
    public double Energy { get; set; }
    public byte[] AudioData { get; set; }
    public override string ToString() => 
        $"Segment: {StartTime:mm\\:ss\\.fff} - {StartTime + Duration:mm\\:ss\\.fff} " +
        $"({Duration.TotalSeconds:F1}s, Speech: {IsSpeech}, Energy: {Energy:F2})";
}

// 处理方法
public static List<AudioSegment> ProcessAudioWithVad(
    byte[] audioData, 
    bool enableVad = true, 
    VadSettings? settings = null)
```

---

## 联系方式

如有问题或建议，请通过以下方式联系：
- 项目仓库: [GitHub Issues](https://github.com/your-repo/issues)
- 邮箱: support@example.com 