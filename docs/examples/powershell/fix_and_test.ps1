# 修复项目编译并运行测试

Write-Host "🔧 开始修复和测试 OWhisper.NET 项目..." -ForegroundColor Green

# 清理所有项目
Write-Host "📦 清理所有项目..." -ForegroundColor Yellow
dotnet clean OWhisper.NET.sln

# 还原NuGet包
Write-Host "📥 还原NuGet包..." -ForegroundColor Yellow
dotnet restore OWhisper.NET.sln

# 编译解决方案
Write-Host "🔨 编译解决方案..." -ForegroundColor Yellow
$buildResult = dotnet build OWhisper.NET.sln --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 编译失败" -ForegroundColor Red
    Write-Host "编译输出:" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    exit 1
}

Write-Host "✅ 编译成功" -ForegroundColor Green

# 运行集成测试
Write-Host "🧪 运行集成测试..." -ForegroundColor Yellow
$testResult = dotnet test IntegrationTests/IntegrationTests.csproj --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 测试失败" -ForegroundColor Red
    Write-Host "测试输出:" -ForegroundColor Red
    Write-Host $testResult -ForegroundColor Red
    exit 1
}

Write-Host "✅ 所有测试通过" -ForegroundColor Green

Write-Host "🎉 项目修复和测试完成！" -ForegroundColor Green 