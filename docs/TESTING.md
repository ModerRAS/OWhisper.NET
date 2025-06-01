# OWhisper.NET 测试指南

本项目包含完整的测试套件，覆盖单元测试和集成测试，确保代码质量和功能正确性。

## 测试项目结构

### 现有测试项目
- **IntegrationTests**: 原有的针对 OWhisper.NET 主项目的集成测试

### 新增测试项目

#### Core 项目测试
- **OWhisper.Core.UnitTests**: Core 项目的单元测试
  - `Services/AudioProcessorTests.cs`: 音频处理服务的单元测试
  - `Services/TranscriptionQueueServiceTests.cs`: 转录队列服务的单元测试
  - `Models/ApiResponseTests.cs`: API响应模型的单元测试

- **OWhisper.Core.IntegrationTests**: Core 项目的集成测试
  - `Services/TranscriptionQueueServiceIntegrationTests.cs`: 转录队列服务的集成测试

#### CLI 项目测试
- **OWhisper.CLI.UnitTests**: CLI 项目的单元测试
  - `ProgramTests.cs`: 程序主入口和配置的单元测试
  - `WebServerHostedServiceTests.cs`: Web服务器托管服务的单元测试

- **OWhisper.CLI.IntegrationTests**: CLI 项目的集成测试
  - `WebServerIntegrationTests.cs`: Web服务器的集成测试

## 测试框架和工具

- **测试框架**: xUnit
- **断言库**: FluentAssertions
- **模拟框架**: Moq
- **代码覆盖率**: coverlet.collector
- **ASP.NET Core 测试**: Microsoft.AspNetCore.Mvc.Testing

## 运行测试

### 运行所有测试
```powershell
.\run-tests.ps1
```

### 单独运行特定测试项目
```bash
# Core 单元测试
dotnet test OWhisper.Core.UnitTests

# Core 集成测试
dotnet test OWhisper.Core.IntegrationTests

# CLI 单元测试
dotnet test OWhisper.CLI.UnitTests

# CLI 集成测试
dotnet test OWhisper.CLI.IntegrationTests

# 原有集成测试
dotnet test IntegrationTests
```

### 带代码覆盖率运行
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### 详细输出运行
```bash
dotnet test --logger "console;verbosity=detailed"
```

## 测试范围

### 单元测试覆盖范围
- **AudioProcessor**: 音频文件处理和格式转换
- **TranscriptionQueueService**: 任务队列管理和状态追踪
- **ApiResponse**: API响应模型的创建和验证
- **Program**: CLI程序的配置和服务注册
- **WebServerHostedService**: Web服务器生命周期管理

### 集成测试覆盖范围
- **转录队列服务**: 完整的任务生命周期测试
- **Web服务器**: HTTP端点和服务器启动测试
- **配置系统**: 配置加载和依赖注入测试

## 测试最佳实践

### 命名约定
- 测试方法: `MethodName_Scenario_ExpectedResult`
- 测试类: `ClassNameTests`

### 测试结构 (AAA模式)
```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange - 准备测试数据和环境
    var input = "test data";
    
    // Act - 执行被测试的方法
    var result = methodUnderTest(input);
    
    // Assert - 验证结果
    result.Should().Be("expected value");
}
```

### Mock 使用原则
- 仅对外部依赖进行 Mock
- 验证重要的交互行为
- 使用严格模式避免意外调用

### 集成测试注意事项
- 使用独立的测试配置
- 确保测试之间的隔离性
- 处理异步操作和超时

## 持续集成

测试套件设计为可在以下环境中运行：
- 本地开发环境
- GitHub Actions
- Azure DevOps
- 其他 CI/CD 系统

## 故障排除

### 常见问题
1. **音频测试失败**: 确保系统安装了必要的音频编解码器
2. **Web服务器测试失败**: 检查端口是否被占用
3. **依赖注入测试失败**: 验证服务注册配置

### 调试测试
```bash
# 运行特定测试方法
dotnet test --filter "TestMethodName"

# 运行特定测试类
dotnet test --filter "TestClassName"

# 运行带有特定分类的测试
dotnet test --filter "Category=Unit"
```

## 贡献指南

添加新功能时，请确保：
1. 编写对应的单元测试
2. 如果涉及外部依赖，添加集成测试
3. 保持测试覆盖率在 80% 以上
4. 遵循现有的测试命名和结构约定 