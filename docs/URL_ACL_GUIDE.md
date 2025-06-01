# OWhisper.NET URL ACL 权限管理指南

## 概述

URL ACL（Access Control List）是Windows HTTP.SYS的安全功能，用于控制哪些用户可以绑定到特定的URL前缀。当应用程序尝试监听HTTP端口时，如果没有正确的URL ACL权限，可能会遇到权限问题。

## 问题症状

如果您遇到以下错误，很可能是URL ACL权限问题：
- `HttpListenerException: 拒绝访问`
- `Access denied`
- Web服务无法启动
- 端口监听失败

## 解决方案

### 自动解决方案

OWhisper.NET现在包含了自动URL ACL管理功能：

1. **启动时自动检查**: 程序启动时会自动检查URL ACL权限
2. **自动设置**: 如果以管理员身份运行，程序会尝试自动设置权限
3. **友好提示**: 如果没有管理员权限，会显示详细的手动设置指令

### 手动解决方案

#### 方法1: 以管理员身份运行程序
1. 右键点击 OWhisper.NET.exe
2. 选择"以管理员身份运行"
3. 程序会自动检测并设置URL ACL权限

#### 方法2: 手动执行netsh命令

打开**管理员权限**的命令提示符或PowerShell，执行以下命令：

```bash
# 使用当前用户
netsh http add urlacl url=http://+:11899/ user=%USERNAME%

# 或使用Everyone用户（适用于多用户环境）
netsh http add urlacl url=http://+:11899/ user=Everyone
```

#### 方法3: 使用WinForm界面

1. 启动OWhisper.NET（调试模式）
2. 在菜单栏选择 **工具 > URL ACL管理**
3. 点击 **检查ACL权限** 查看当前状态
4. 点击 **设置ACL权限** 进行配置

## URL ACL管理界面功能

### 检查ACL权限
- 显示当前监听URL
- 检查ACL权限状态
- 显示管理员权限状态
- 提供推荐的设置命令

### 设置ACL权限
- 自动检测管理员权限
- 支持选择用户类型（当前用户或Everyone）
- 自动处理已存在的权限
- 详细的错误信息和建议

## 常见问题

### Q: 为什么需要URL ACL权限？
A: Windows HTTP.SYS要求非管理员用户明确获得绑定到特定URL的权限，这是一种安全措施。

### Q: 设置后还是无法启动？
A: 请检查：
1. 端口是否被其他程序占用
2. 防火墙设置
3. URL格式是否正确
4. 是否有其他网络限制

### Q: 如何查看已设置的URL ACL？
A: 运行命令：`netsh http show urlacl`

### Q: 如何删除URL ACL？
A: 运行命令：`netsh http delete urlacl url=http://+:11899/`

## 技术细节

### 默认配置
- **监听地址**: `http://+:11899/`
- **端口**: 11899
- **协议**: HTTP

### 环境变量配置
您可以通过环境变量自定义监听配置：
```bash
set OWHISPER_HOST=localhost
set OWHISPER_PORT=8080
```

### URL格式说明
- `+` 表示监听所有可用的IP地址
- `0.0.0.0` 表示监听所有IPv4地址
- `localhost` 仅监听本地回环地址

## 安全建议

1. **最小权限原则**: 优先使用当前用户而不是Everyone
2. **定期检查**: 定期检查不需要的URL ACL权限
3. **监控访问**: 在生产环境中监控HTTP访问日志
4. **防火墙配置**: 配置适当的防火墙规则

## 故障排除

### 步骤1: 验证权限
```bash
netsh http show urlacl | findstr "11899"
```

### 步骤2: 检查端口占用
```bash
netstat -an | findstr "11899"
```

### 步骤3: 测试连接
```bash
curl http://localhost:11899/
```

### 步骤4: 查看日志
检查应用程序日志文件 `Logs/log-*.txt`

## 相关资源

- [Microsoft HTTP.SYS 文档](https://docs.microsoft.com/en-us/windows/win32/http/http-api-start-page)
- [Netsh HTTP Commands](https://docs.microsoft.com/en-us/windows-server/networking/technologies/netsh/netsh-http)

---

如果您仍然遇到问题，请提供详细的错误信息和系统环境，以便获得更好的技术支持。 