# OWhisper.NET 测试运行脚本

Write-Host "开始运行 OWhisper.NET 测试套件..." -ForegroundColor Green

# 构建解决方案
Write-Host "构建解决方案..." -ForegroundColor Yellow
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "构建失败!" -ForegroundColor Red
    exit 1
}

Write-Host "构建成功!" -ForegroundColor Green

# 运行单元测试
Write-Host "`n运行单元测试..." -ForegroundColor Yellow

Write-Host "运行 OWhisper.Core 单元测试..." -ForegroundColor Cyan
dotnet test OWhisper.Core.UnitTests --configuration Release --logger "console;verbosity=minimal"
$coreUnitTestResult = $LASTEXITCODE

Write-Host "运行 OWhisper.CLI 单元测试..." -ForegroundColor Cyan
dotnet test OWhisper.CLI.UnitTests --configuration Release --logger "console;verbosity=minimal"
$cliUnitTestResult = $LASTEXITCODE

# 运行集成测试
Write-Host "`n运行集成测试..." -ForegroundColor Yellow

Write-Host "运行原有集成测试..." -ForegroundColor Cyan
dotnet test IntegrationTests --configuration Release --logger "console;verbosity=minimal"
$originalIntegrationTestResult = $LASTEXITCODE

Write-Host "运行 OWhisper.Core 集成测试..." -ForegroundColor Cyan
dotnet test OWhisper.Core.IntegrationTests --configuration Release --logger "console;verbosity=minimal"
$coreIntegrationTestResult = $LASTEXITCODE

Write-Host "运行 OWhisper.CLI 集成测试..." -ForegroundColor Cyan
dotnet test OWhisper.CLI.IntegrationTests --configuration Release --logger "console;verbosity=minimal"
$cliIntegrationTestResult = $LASTEXITCODE

# 汇总结果
Write-Host "`n=== 测试结果汇总 ===" -ForegroundColor Magenta

if ($coreUnitTestResult -eq 0) {
    Write-Host "✓ OWhisper.Core 单元测试: 通过" -ForegroundColor Green
} else {
    Write-Host "✗ OWhisper.Core 单元测试: 失败" -ForegroundColor Red
}

if ($cliUnitTestResult -eq 0) {
    Write-Host "✓ OWhisper.CLI 单元测试: 通过" -ForegroundColor Green
} else {
    Write-Host "✗ OWhisper.CLI 单元测试: 失败" -ForegroundColor Red
}

if ($originalIntegrationTestResult -eq 0) {
    Write-Host "✓ 原有集成测试: 通过" -ForegroundColor Green
} else {
    Write-Host "✗ 原有集成测试: 失败" -ForegroundColor Red
}

if ($coreIntegrationTestResult -eq 0) {
    Write-Host "✓ OWhisper.Core 集成测试: 通过" -ForegroundColor Green
} else {
    Write-Host "✗ OWhisper.Core 集成测试: 失败" -ForegroundColor Red
}

if ($cliIntegrationTestResult -eq 0) {
    Write-Host "✓ OWhisper.CLI 集成测试: 通过" -ForegroundColor Green
} else {
    Write-Host "✗ OWhisper.CLI 集成测试: 失败" -ForegroundColor Red
}

# 总体结果
$totalFailures = @($coreUnitTestResult, $cliUnitTestResult, $originalIntegrationTestResult, $coreIntegrationTestResult, $cliIntegrationTestResult) | Where-Object { $_ -ne 0 }

if ($totalFailures.Count -eq 0) {
    Write-Host "`n🎉 所有测试都通过了!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n❌ 有 $($totalFailures.Count) 个测试套件失败!" -ForegroundColor Red
    exit 1
} 