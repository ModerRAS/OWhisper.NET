# 验证环境变量默认值处理脚本

Write-Host "🔍 验证环境变量默认值处理..." -ForegroundColor Cyan

# 1. 测试Program.cs中的方法
Write-Host "`n1️⃣ 测试C# Program.cs的GetListenHost()和GetListenPort()方法:" -ForegroundColor Yellow

# 编译一个简单的测试程序
$testCode = @'
using System;
using OWhisper.NET;

class Program {
    static void Main() {
        // 测试无环境变量的情况
        Console.WriteLine($"Host: '{OWhisper.NET.Program.GetListenHost()}'");
        Console.WriteLine($"Port: {OWhisper.NET.Program.GetListenPort()}");
        Console.WriteLine($"URL: '{OWhisper.NET.Program.GetListenUrl()}'");
    }
}
'@

# 清除环境变量
if ($env:OWHISPER_HOST) { Remove-Item Env:OWHISPER_HOST }
if ($env:OWHISPER_PORT) { Remove-Item Env:OWHISPER_PORT }

Write-Host "✅ 环境变量已清除" -ForegroundColor Green
Write-Host "   OWHISPER_HOST: $env:OWHISPER_HOST" -ForegroundColor Gray
Write-Host "   OWHISPER_PORT: $env:OWHISPER_PORT" -ForegroundColor Gray

# 启动应用程序并检查监听配置
Write-Host "`n2️⃣ 启动应用程序测试默认配置:" -ForegroundColor Yellow
$process = Start-Process -FilePath ".\OWhisper.NET\bin\Debug\net48\OWhisper.NET.exe" -ArgumentList "--debug" -PassThru -WindowStyle Minimized

Start-Sleep -Seconds 3

# 检查端口监听状态
$listening = netstat -an | findstr "11899.*LISTENING"
if ($listening) {
    Write-Host "✅ 默认端口11899正在监听" -ForegroundColor Green
    Write-Host "   监听状态: $listening" -ForegroundColor Gray
} else {
    Write-Host "❌ 默认端口11899未监听" -ForegroundColor Red
}

# 测试API连接
Write-Host "`n3️⃣ 测试API连接:" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://127.0.0.1:11899/api/status" -Method GET -TimeoutSec 10
    Write-Host "✅ API连接成功" -ForegroundColor Green
    Write-Host "   响应状态: $($response.Status)" -ForegroundColor Gray
} catch {
    Write-Host "❌ API连接失败: $($_.Exception.Message)" -ForegroundColor Red
    
    # 尝试其他地址
    Write-Host "🔄 尝试其他地址..." -ForegroundColor Cyan
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11899/api/status" -Method GET -TimeoutSec 5
        Write-Host "✅ localhost连接成功" -ForegroundColor Green
        Write-Host "   响应状态: $($response.Status)" -ForegroundColor Gray
    } catch {
        Write-Host "❌ localhost连接也失败" -ForegroundColor Red
    }
}

# 清理
Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue

Write-Host "`n4️⃣ 测试自定义环境变量:" -ForegroundColor Yellow
$env:OWHISPER_HOST = "127.0.0.1"
$env:OWHISPER_PORT = "13000"

Write-Host "✅ 设置环境变量" -ForegroundColor Green
Write-Host "   OWHISPER_HOST: $env:OWHISPER_HOST" -ForegroundColor Gray
Write-Host "   OWHISPER_PORT: $env:OWHISPER_PORT" -ForegroundColor Gray

$process2 = Start-Process -FilePath ".\OWhisper.NET\bin\Debug\net48\OWhisper.NET.exe" -ArgumentList "--debug" -PassThru -WindowStyle Minimized

Start-Sleep -Seconds 3

# 检查新端口
$customListening = netstat -an | findstr "13000.*LISTENING"
if ($customListening) {
    Write-Host "✅ 自定义端口13000正在监听" -ForegroundColor Green
    Write-Host "   监听状态: $customListening" -ForegroundColor Gray
} else {
    Write-Host "❌ 自定义端口13000未监听" -ForegroundColor Red
}

# 测试自定义端口API
try {
    $response2 = Invoke-RestMethod -Uri "http://127.0.0.1:13000/api/status" -Method GET -TimeoutSec 10
    Write-Host "✅ 自定义端口API连接成功" -ForegroundColor Green
    Write-Host "   响应状态: $($response2.Status)" -ForegroundColor Gray
} catch {
    Write-Host "❌ 自定义端口API连接失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 清理
Stop-Process -Id $process2.Id -Force -ErrorAction SilentlyContinue
Remove-Item Env:OWHISPER_HOST -ErrorAction SilentlyContinue
Remove-Item Env:OWHISPER_PORT -ErrorAction SilentlyContinue

Write-Host "`n🎯 总结:" -ForegroundColor Green
Write-Host "   默认配置应该使用 0.0.0.0:11899" -ForegroundColor Gray
Write-Host "   环境变量配置应该覆盖默认值" -ForegroundColor Gray
Write-Host "   如果任一测试失败，则存在环境变量默认值处理问题" -ForegroundColor Gray 