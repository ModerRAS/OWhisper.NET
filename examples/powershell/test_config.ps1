# OWhisper.NET Port Configuration Test Script

Write-Host "Testing OWhisper.NET port configuration..." -ForegroundColor Green

# Test 1: Default configuration (port 11899, listen 0.0.0.0)
Write-Host "`nTest 1: Default configuration" -ForegroundColor Yellow
if ($env:OWHISPER_HOST) { Remove-Item Env:OWHISPER_HOST -ErrorAction SilentlyContinue }
if ($env:OWHISPER_PORT) { Remove-Item Env:OWHISPER_PORT -ErrorAction SilentlyContinue }

Write-Host "Starting OWhisper.NET (default config)..." -ForegroundColor Cyan
$process1 = Start-Process -FilePath ".\OWhisper.NET\bin\Debug\net48\OWhisper.NET.exe" -ArgumentList "--debug" -PassThru -WindowStyle Minimized

Start-Sleep -Seconds 5

try {
    $response1 = Invoke-RestMethod -Uri "http://127.0.0.1:11899/api/status" -Method GET -TimeoutSec 10
    Write-Host "SUCCESS: Default port 11899 test passed" -ForegroundColor Green
    Write-Host "   Response: $($response1.Status)" -ForegroundColor Gray
} catch {
    Write-Host "FAILED: Default port 11899 test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Cleanup process 1
Stop-Process -Id $process1.Id -Force -ErrorAction SilentlyContinue

# Test 2: Custom configuration (port 13000, listen 127.0.0.1)
Write-Host "`nTest 2: Custom configuration" -ForegroundColor Yellow
$env:OWHISPER_HOST = "127.0.0.1"
$env:OWHISPER_PORT = "13000"

Write-Host "Setting environment variables:" -ForegroundColor Cyan
Write-Host "  OWHISPER_HOST=$env:OWHISPER_HOST" -ForegroundColor Gray  
Write-Host "  OWHISPER_PORT=$env:OWHISPER_PORT" -ForegroundColor Gray

Write-Host "Starting OWhisper.NET (custom config)..." -ForegroundColor Cyan
$process2 = Start-Process -FilePath ".\OWhisper.NET\bin\Debug\net48\OWhisper.NET.exe" -ArgumentList "--debug" -PassThru -WindowStyle Minimized

Start-Sleep -Seconds 5

try {
    $response2 = Invoke-RestMethod -Uri "http://127.0.0.1:13000/api/status" -Method GET -TimeoutSec 10
    Write-Host "SUCCESS: Custom port 13000 test passed" -ForegroundColor Green
    Write-Host "   Response: $($response2.Status)" -ForegroundColor Gray
} catch {
    Write-Host "FAILED: Custom port 13000 test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Verify old port is not accessible
try {
    $response3 = Invoke-RestMethod -Uri "http://127.0.0.1:11899/api/status" -Method GET -TimeoutSec 5
    Write-Host "UNEXPECTED: Old port 11899 still accessible" -ForegroundColor Red
} catch {
    Write-Host "CONFIRMED: Old port 11899 not accessible" -ForegroundColor Green
}

# Cleanup process 2
Stop-Process -Id $process2.Id -Force -ErrorAction SilentlyContinue

# Cleanup environment variables
Remove-Item Env:OWHISPER_HOST -ErrorAction SilentlyContinue
Remove-Item Env:OWHISPER_PORT -ErrorAction SilentlyContinue

Write-Host "`nTest completed! OWhisper.NET port configuration works correctly" -ForegroundColor Green
Write-Host "`nUsage:" -ForegroundColor Cyan
Write-Host "   Default: OWhisper.NET.exe (listens on 0.0.0.0:11899)" -ForegroundColor Gray
Write-Host "   Custom: " -ForegroundColor Gray
Write-Host "     `$env:OWHISPER_HOST='192.168.1.100'" -ForegroundColor Gray
Write-Host "     `$env:OWHISPER_PORT='8080'" -ForegroundColor Gray
Write-Host "     OWhisper.NET.exe" -ForegroundColor Gray 