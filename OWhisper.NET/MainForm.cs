using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using OWhisper.Core.Models;
using OWhisper.Core.Services;
using Newtonsoft.Json;
using System.Threading;
using System.Text;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using TaskStatus = OWhisper.Core.Models.TaskStatus;
using OWhisper.NET.Services; // 添加UrlAclHelper和CredentialService的命名空间

namespace OWhisper.NET
{
    public partial class MainForm : Form
    {
        private string _selectedAudioFile;
        private string _outputFilePath;
        private readonly HttpClient _httpClient;
        private string _currentTaskId;
        private CancellationTokenSource _sseCancellationToken;
        private TaskStatus? _lastStatus = null; // 使用nullable类型来跟踪状态
        
        // 文本润色相关字段
        private TextPolishingHttpClient _polishingHttpClient;
        private NET.Services.TemplateManagerService _templateManager;
        private List<PolishingTemplate> _availableTemplates = new List<PolishingTemplate>();
        private SimplePolishingConfig _polishingConfig = new SimplePolishingConfig();
        
        // 动态获取API基础URL
        private string ApiBaseUrl
        {
            get
            {
                var listenUrl = Program._webApiService?.ListenUrl ?? "http://localhost:11899";
                
                // 如果监听地址是0.0.0.0，客户端应该使用127.0.0.1连接
                if (listenUrl.Contains("://0.0.0.0:"))
                {
                    listenUrl = listenUrl.Replace("://0.0.0.0:", "://127.0.0.1:");
                }
                // 如果监听地址是+，客户端应该使用127.0.0.1连接
                if (listenUrl.Contains("://+:")) {
                    listenUrl = listenUrl.Replace("://+:", "://127.0.0.1:");
                }

                return listenUrl;
            }
        }

        public MainForm()
        {
            InitializeComponent();
            
            // 设置窗体图标
            this.Icon = LoadAppIcon();
            
            // 初始化HTTP客户端
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(30); // 设置较长的超时时间
            
            // 绑定事件处理程序
            btnSelectFile.Click += BtnSelectFile_Click;
            btnSelectFolder.Click += BtnSelectOutput_Click;
            btnProcess.Click += BtnProcess_Click;
            
            // 绑定菜单事件处理程序
            checkUrlAclMenuItem.Click += CheckUrlAclMenuItem_Click;
            setupUrlAclMenuItem.Click += SetupUrlAclMenuItem_Click;
            exitMenuItem.Click += ExitMenuItem_Click;
            
            // 绑定文本润色相关事件
            chkEnablePolishing.CheckedChanged += ChkEnablePolishing_CheckedChanged;
            btnTestConnection.Click += BtnTestConnection_Click;
            btnConfigurePolishing.Click += BtnConfigurePolishing_Click;
            cmbPolishingTemplate.SelectedIndexChanged += CmbPolishingTemplate_SelectedIndexChanged;
            
            // 设置初始placeholder文本
            txtSelectedFile.Text = "请选择音频文件...";
            txtSelectedFile.ForeColor = System.Drawing.SystemColors.GrayText;
            txtOutputPath.Text = "请选择保存位置...";
            txtOutputPath.ForeColor = System.Drawing.SystemColors.GrayText;
            
            // 默认窗口状态为最小化
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            
            // 初始化文本润色服务
            InitializePolishingService();
            
            // 绑定Disposed事件来清理资源
            this.Disposed += (s, e) => {
                _httpClient?.Dispose();
                _sseCancellationToken?.Cancel();
                _sseCancellationToken?.Dispose();
            };
        }

        // 添加加载应用图标的方法（优化版本，专门为MainForm任务栏优化）
        private Icon LoadAppIcon() {
            try {
                // 首先尝试直接从超高分辨率PNG图标创建Icon（任务栏专用）
                string[] highResPngPaths = {
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_super_512x512.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_super_256x256.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_512x512.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_256x256.png")
                };

                foreach (string pngPath in highResPngPaths) {
                    if (File.Exists(pngPath)) {
                        using (var bitmap = new Bitmap(pngPath)) {
                            // 为任务栏创建高分辨率图标，指定较大尺寸
                            var largeIcon = new Icon(Icon.FromHandle(bitmap.GetHicon()), 64, 64);
                            return largeIcon;
                        }
                    }
                }

                // 回退到ICO文件
                string iconPath = Path.Combine(Application.StartupPath, "Resources", "app_icon.ico");
                if (File.Exists(iconPath)) {
                    // 从ICO文件创建较大尺寸的图标
                    using (var icon = new Icon(iconPath)) {
                        return new Icon(icon, 64, 64);
                    }
                }

                // 如果文件不存在，尝试从嵌入的资源加载超高分辨率版本
                var assembly = Assembly.GetExecutingAssembly();
                
                string[] embeddedResources = {
                    "OWhisper.NET.Resources.app_icon_super_512x512.png",
                    "OWhisper.NET.Resources.app_icon_super_256x256.png",
                    "OWhisper.NET.Resources.app_icon_256x256.png"
                };

                foreach (string resourceName in embeddedResources) {
                    using (var stream = assembly.GetManifestResourceStream(resourceName)) {
                        if (stream != null) {
                            using (var bitmap = new Bitmap(stream)) {
                                var largeIcon = new Icon(Icon.FromHandle(bitmap.GetHicon()), 64, 64);
                                return largeIcon;
                            }
                        }
                    }
                }

                // 最后尝试普通ICO资源
                using (var stream = assembly.GetManifestResourceStream("OWhisper.NET.Resources.app_icon.ico")) {
                    if (stream != null) {
                        using (var icon = new Icon(stream)) {
                            return new Icon(icon, 64, 64);
                        }
                    }
                }

                // 如果都失败了，尝试加载其他PNG图标并转换
                string[] fallbackPngPaths = {
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_128x128.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_64x64.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_32x32.png")
                };

                foreach (string pngPath in fallbackPngPaths) {
                    if (File.Exists(pngPath)) {
                        using (var bitmap = new Bitmap(pngPath)) {
                            return new Icon(Icon.FromHandle(bitmap.GetHicon()), 48, 48);
                        }
                    }
                }
            }
            catch (Exception ex) {
                // 记录错误但不中断应用
                Console.WriteLine($"加载自定义图标失败: {ex.Message}");
            }

            // 回退到系统默认图标，但使用较大尺寸
            return new Icon(SystemIcons.Application, 48, 48);
        }

        /// <summary>
        /// 调试模式下显示窗口
        /// </summary>
        public void ShowForDebug()
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Show();
            Activate();
        }

        private void BtnSelectFile_Click(object sender, EventArgs e)
        {
            try
            {
                // 确保对话框在前台显示
                this.TopMost = true;
                this.TopMost = false;
                
                // 设置初始目录
                if (string.IsNullOrEmpty(openFileDialog.InitialDirectory))
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                
                DialogResult result = openFileDialog.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    _selectedAudioFile = openFileDialog.FileName;
                    txtSelectedFile.Text = Path.GetFileName(_selectedAudioFile);
                    txtSelectedFile.ForeColor = System.Drawing.SystemColors.WindowText;
                    
                    // 检查文件是否存在且可读
                    if (!File.Exists(_selectedAudioFile))
                    {
                        MessageBox.Show("所选文件不存在！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _selectedAudioFile = null;
                        ResetFileSelection();
                        return;
                    }
                    
                    // 检查文件大小是否合理（小于500MB）
                    var fileInfo = new FileInfo(_selectedAudioFile);
                    if (fileInfo.Length > 500 * 1024 * 1024)
                    {
                        var result2 = MessageBox.Show("文件过大（超过500MB），可能会影响处理速度。是否继续？", 
                            "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (result2 == DialogResult.No)
                        {
                            _selectedAudioFile = null;
                            ResetFileSelection();
                            return;
                        }
                    }
                    
                    // 自动设置默认的输出文件名
                    SetDefaultOutputPath();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择文件时发生错误: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _selectedAudioFile = null;
                ResetFileSelection();
            }
        }

        private void SetDefaultOutputPath()
        {
            if (!string.IsNullOrEmpty(_selectedAudioFile))
            {
                var directory = Path.GetDirectoryName(_selectedAudioFile);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_selectedAudioFile);
                var defaultPath = Path.Combine(directory, fileNameWithoutExt + ".srt");
                
                txtOutputPath.Text = defaultPath;
                txtOutputPath.ForeColor = System.Drawing.SystemColors.WindowText;
                _outputFilePath = defaultPath;
            }
        }

        private void ResetFileSelection()
        {
            txtSelectedFile.Text = "请选择音频文件...";
            txtSelectedFile.ForeColor = System.Drawing.SystemColors.GrayText;
        }

        private void BtnSelectOutput_Click(object sender, EventArgs e)
        {
            try
            {
                // 确保对话框在前台显示
                this.TopMost = true;
                this.TopMost = false;
                
                // 设置默认文件名和目录
                if (!string.IsNullOrEmpty(_selectedAudioFile))
                {
                    saveFileDialog.InitialDirectory = Path.GetDirectoryName(_selectedAudioFile);
                    saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_selectedAudioFile);
                }
                else
                {
                    saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                
                if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    _outputFilePath = saveFileDialog.FileName;
                    txtOutputPath.Text = _outputFilePath;
                    txtOutputPath.ForeColor = System.Drawing.SystemColors.WindowText;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择保存位置时发生错误: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _outputFilePath = null;
                txtOutputPath.Text = "请选择保存位置...";
                txtOutputPath.ForeColor = System.Drawing.SystemColors.GrayText;
            }
        }

        private async void BtnProcess_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedAudioFile))
            {
                MessageBox.Show("请先选择音频文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(_outputFilePath))
            {
                MessageBox.Show("请先选择保存位置", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnProcess.Enabled = false;
            progressBar.Value = 0;
            btnProcess.Text = "正在提交任务..."; // 只在开始时设置一次
            _lastStatus = null; // 重置状态跟踪

            try
            {
                // 检查API服务是否可用
                await CheckApiStatus();

                // 读取音频文件
                byte[] audioBytes = File.ReadAllBytes(_selectedAudioFile);
                string fileName = Path.GetFileName(_selectedAudioFile);

                // 提交转录任务
                var taskResponse = await SubmitTranscribeTask(audioBytes, fileName);
                if (taskResponse != null)
                {
                    _currentTaskId = taskResponse.TaskId;
                    // 移除这里的按钮文字设置，让SSE更新来处理
                    // btnProcess.Text = $"队列位置: {taskResponse.QueuePosition}";
                    
                    // 开始监听任务进度
                    await MonitorTaskProgress(_currentTaskId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnProcess.Enabled = true;
                progressBar.Value = 0;
                btnProcess.Text = "开始处理";
                _currentTaskId = null;
                _lastStatus = null; // 重置状态跟踪
            }
        }

        private async Task CheckApiStatus()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/api/model/status");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API服务不可用，状态码: {response.StatusCode}");
                }

                // 解析API响应
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<object>>(responseContent);
                
                if (apiResponse?.Status != "success")
                {
                    var errorMessage = apiResponse?.Error ?? apiResponse?.ErrorCode ?? "API状态检查失败";
                    throw new Exception($"API服务状态异常: {errorMessage}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"无法连接到API服务: {ex.Message}");
            }
            catch (JsonException ex)
            {
                throw new Exception($"API响应解析失败: {ex.Message}");
            }
        }

        private async Task<TaskCreationResponse> SubmitTranscribeTask(byte[] audioData, string fileName)
        {
            using (var form = new MultipartFormDataContent())
            {
                // 创建文件内容
                var fileContent = new ByteArrayContent(audioData);
                
                // 设置Content-Type
                var extension = Path.GetExtension(fileName).ToLower();
                switch (extension)
                {
                    case ".mp3":
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
                        break;
                    case ".wav":
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                        break;
                    case ".aac":
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/aac");
                        break;
                    case ".m4a":
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/aac");
                        break;
                    default:
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        break;
                }

                form.Add(fileContent, "file", fileName);
                
                // 添加VAD参数（默认启用）
                form.Add(new StringContent("true"), "enable_vad");

                // 发送请求
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/api/transcribe", form);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return ParseTaskCreationResponse(responseContent);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"提交任务失败: {response.StatusCode}\n{errorContent}");
                }
            }
        }

        private async Task MonitorTaskProgress(string taskId)
        {
            _sseCancellationToken = new CancellationTokenSource();
            
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(60); // 设置较长的超时时间
                
                var response = await client.GetAsync($"{ApiBaseUrl}/api/tasks/{taskId}/progress", 
                    HttpCompletionOption.ResponseHeadersRead, _sseCancellationToken.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"无法连接到SSE服务: {response.StatusCode}");
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (!_sseCancellationToken.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (line.StartsWith("data: "))
                    {
                        var jsonData = line.Substring(6);
                        await ProcessProgressUpdate(jsonData);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消操作
            }
            catch (Exception ex)
            {
                throw new Exception($"监听任务进度失败: {ex.Message}");
            }
        }

        private async Task ProcessProgressUpdate(string jsonData)
        {
            try
            {
                var progress = JsonConvert.DeserializeObject<TranscriptionProgress>(jsonData);
                
                // 更新UI (确保在UI线程中执行)
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => UpdateProgressUI(progress)));
                }
                else
                {
                    UpdateProgressUI(progress);
                }
                
                // 如果任务完成，处理结果
                if (progress.Status == TaskStatus.Completed && progress.Result != null)
                {
                    await HandleTaskCompletion(progress.Result);
                }
                else if (progress.Status == TaskStatus.Failed)
                {
                    HandleTaskFailure(progress.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理进度更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新进度条UI，将进度条和状态文字更新分离以减少闪烁
        /// </summary>
        private void UpdateProgressUI(TranscriptionProgress progress)
        {
            
            // 根据状态决定如何更新按钮文字
            var shouldUpdateText = false;
            var newText = string.Empty;
            
            switch (progress.Status)
            {
                case TaskStatus.Queued:
                    newText = $"队列位置: {progress.QueuePosition}";
                    shouldUpdateText = progress.Status != _lastStatus || btnProcess.Text != newText;
                    break;
                    
                case TaskStatus.Processing:
                    // 对于Processing状态，包含进度百分比
                    var currentPercent = (int)progress.Progress;
                    newText = $"处理中... {currentPercent}%";
                    shouldUpdateText = progress.Status != _lastStatus || btnProcess.Text != newText;
                    // 始终更新进度条
                    UpdateProgressBar(progress.Progress);
                    break;
                    
                case TaskStatus.Completed:
                    newText = "处理完成";
                    shouldUpdateText = progress.Status != _lastStatus;
                    UpdateProgressBar(progress.Progress);
                    break;
                    
                case TaskStatus.Failed:
                    newText = "处理失败";
                    shouldUpdateText = progress.Status != _lastStatus;
                    break;
                    
                case TaskStatus.Cancelled:
                    newText = "已取消";
                    shouldUpdateText = progress.Status != _lastStatus;
                    break;
            }
            
            // 只有在需要时才更新按钮文字
            if (shouldUpdateText && !string.IsNullOrEmpty(newText))
            {
                btnProcess.Text = newText;
                _lastStatus = progress.Status;
            }
        }

        /// <summary>
        /// 更新进度条
        /// </summary>
        private void UpdateProgressBar(double progress)
        {
            var progressValue = Math.Min(100, Math.Max(0, (int)progress));
            if (progressBar.Value != progressValue)
            {
                progressBar.Value = progressValue;
            }
        }

        /// <summary>
        /// 更新状态文字（仅在状态改变时调用）
        /// 注意：这个方法现在已被UpdateProgressUI替代，保留以防需要
        /// </summary>
        private void UpdateStatusText(TranscriptionProgress progress)
        {
            // 这个方法现在不应该被调用，所有逻辑已移到UpdateProgressUI中
            System.Diagnostics.Debug.WriteLine("UpdateStatusText被调用，但应该使用UpdateProgressUI");
        }

        /// <summary>
        /// 更新处理中的状态（降低更新频率）
        /// 注意：这个方法现在已被UpdateProgressUI替代，保留以防需要
        /// </summary>
        private void UpdateProcessingStatus(double progress)
        {
            // 这个方法现在不应该被调用，所有逻辑已移到UpdateProgressUI中
            System.Diagnostics.Debug.WriteLine("UpdateProcessingStatus被调用，但应该使用UpdateProgressUI");
        }

        private async Task HandleTaskCompletion(TranscriptionResult result)
        {
            try
            {
                // 检查是否需要进行文本润色
                if (chkEnablePolishing.Checked && !string.IsNullOrWhiteSpace(txtApiKey.Text) && !string.IsNullOrWhiteSpace(result.Text))
                {
                    await PerformTextPolishingAsync(result);
                }

                // 根据文件扩展名保存不同格式
                string content;
                string polishedContent = null;
                string extension = Path.GetExtension(_outputFilePath).ToLower();
                
                if (extension == ".srt")
                {
                    content = result.SrtContent;
                    polishedContent = result.PolishingSrtContent;
                }
                else
                {
                    content = result.Text;
                    polishedContent = result.PolishedText;
                }
                
                // 如果有润色结果且用户启用了润色，保存润色后的内容
                var finalContent = (!string.IsNullOrEmpty(polishedContent) && result.PolishingEnabled) ? polishedContent : content;
                File.WriteAllText(_outputFilePath, finalContent, System.Text.Encoding.UTF8);
                
                // 如果有润色结果，同时保存原始文件
                if (!string.IsNullOrEmpty(polishedContent) && result.PolishingEnabled)
                {
                    var originalFilePath = Path.ChangeExtension(_outputFilePath, $".original{Path.GetExtension(_outputFilePath)}");
                    File.WriteAllText(originalFilePath, content, System.Text.Encoding.UTF8);
                }
                
                // 构建完成消息
                var message = new StringBuilder();
                message.AppendLine($"处理完成！");
                message.AppendLine($"转录耗时: {result.ProcessingTime:F1}秒");
                
                if (result.PolishingEnabled && result.PolishingResult != null)
                {
                    if (result.PolishingResult.IsSuccess)
                    {
                        message.AppendLine($"润色耗时: {result.PolishingProcessingTime:F1}秒");
                        message.AppendLine($"使用模型: {result.PolishingModel}");
                        message.AppendLine($"使用模板: {result.PolishingTemplateName}");
                        message.AppendLine($"Token消耗: {result.PolishingResult.TokensUsed}");
                        message.AppendLine($"润色文件: {_outputFilePath}");
                        if (!string.IsNullOrEmpty(polishedContent))
                        {
                            var originalFilePath = Path.ChangeExtension(_outputFilePath, $".original{Path.GetExtension(_outputFilePath)}");
                            message.AppendLine($"原始文件: {originalFilePath}");
                        }
                    }
                    else
                    {
                        message.AppendLine($"润色失败: {result.PolishingResult.ErrorMessage}");
                        message.AppendLine($"已保存原始转录结果");
                    }
                }
                else
                {
                    message.AppendLine($"保存到: {_outputFilePath}");
                }

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => {
                        MessageBox.Show(message.ToString(), "处理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                else
                {
                    MessageBox.Show(message.ToString(), "处理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                
                // 取消SSE连接
                _sseCancellationToken?.Cancel();
            }
            catch (Exception ex)
            {
                HandleTaskFailure($"保存文件失败: {ex.Message}");
            }
        }

        private void HandleTaskFailure(string errorMessage)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => {
                    MessageBox.Show($"处理失败: {errorMessage}", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            else
            {
                MessageBox.Show($"处理失败: {errorMessage}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
            // 取消SSE连接
            _sseCancellationToken?.Cancel();
        }

        private TaskCreationResponse ParseTaskCreationResponse(string jsonResponse)
        {
            try
            {
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<TaskCreationResponse>>(jsonResponse);
                
                if (apiResponse.Status != "success")
                {
                    var errorMessage = apiResponse.Error ?? apiResponse.ErrorCode ?? "未知错误";
                    throw new Exception($"API返回错误: {errorMessage}");
                }
                
                if (apiResponse.Data == null)
                {
                    throw new Exception("API响应中缺少数据字段");
                }
                
                return apiResponse.Data;
            }
            catch (JsonException ex)
            {
                throw new Exception($"JSON解析失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"解析响应失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查URL ACL权限菜单项点击事件
        /// </summary>
        private async void CheckUrlAclMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var listenUrl = Program._webApiService?.ListenUrl ?? "http://+:11899/";
                var formattedUrl = UrlAclHelper.FormatUrlForAcl(listenUrl);
                
                var aclExists = await UrlAclHelper.CheckUrlAclAsync(formattedUrl);
                var isAdmin = UrlAclHelper.IsRunningAsAdministrator();
                var currentUser = UrlAclHelper.GetCurrentUserName();
                
                var message = new StringBuilder();
                message.AppendLine($"URL ACL检查结果:");
                message.AppendLine($"监听地址: {listenUrl}");
                message.AppendLine($"格式化URL: {formattedUrl}");
                message.AppendLine($"ACL权限状态: {(aclExists ? "已设置" : "未设置")}");
                message.AppendLine($"管理员权限: {(isAdmin ? "是" : "否")}");
                message.AppendLine($"当前用户: {currentUser}");
                
                if (!aclExists)
                {
                    message.AppendLine();
                    message.AppendLine("推荐的设置命令:");
                    message.AppendLine($"netsh http add urlacl url={formattedUrl} user={currentUser}");
                }
                
                MessageBox.Show(message.ToString(), "URL ACL权限检查", 
                    MessageBoxButtons.OK, aclExists ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检查URL ACL权限时出错: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 设置URL ACL权限菜单项点击事件
        /// </summary>
        private async void SetupUrlAclMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var listenUrl = Program._webApiService?.ListenUrl ?? "http://+:11899/";
                var formattedUrl = UrlAclHelper.FormatUrlForAcl(listenUrl);
                var currentUser = UrlAclHelper.GetCurrentUserName();
                
                if (!UrlAclHelper.IsRunningAsAdministrator())
                {
                    var message = new StringBuilder();
                    message.AppendLine("需要管理员权限才能设置URL ACL。");
                    message.AppendLine();
                    message.AppendLine("请以管理员身份运行此程序，或手动执行以下命令：");
                    message.AppendLine();
                    message.AppendLine($"netsh http add urlacl url={formattedUrl} user={currentUser}");
                    message.AppendLine();
                    message.AppendLine("或者使用Everyone用户：");
                    message.AppendLine($"netsh http add urlacl url={formattedUrl} user=Everyone");
                    
                    MessageBox.Show(message.ToString(), "需要管理员权限", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // 首先检查是否已经设置
                var aclExists = await UrlAclHelper.CheckUrlAclAsync(formattedUrl);
                if (aclExists)
                {
                    var result = MessageBox.Show("URL ACL权限已经设置。是否要重新设置？", "权限已存在", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                    if (result == DialogResult.No)
                        return;
                        
                    // 先删除现有的ACL
                    await UrlAclHelper.RemoveUrlAclAsync(formattedUrl);
                }
                
                // 显示用户选择对话框
                var userChoice = MessageBox.Show($"选择用户类型：\n\n是(Y) - 使用当前用户 ({currentUser})\n否(N) - 使用Everyone用户", 
                    "选择用户类型", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                
                if (userChoice == DialogResult.Cancel)
                    return;
                
                var user = userChoice == DialogResult.Yes ? currentUser : "Everyone";
                
                // 设置URL ACL
                var (success, resultMessage) = await UrlAclHelper.AddUrlAclAsync(formattedUrl, user);
                
                if (success)
                {
                    MessageBox.Show($"URL ACL权限设置成功！\n\nURL: {formattedUrl}\n用户: {user}", "设置成功", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"URL ACL权限设置失败:\n\n{resultMessage}", "设置失败", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置URL ACL权限时出错: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 退出菜单项点击事件
        /// </summary>
        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            Program.ExitApplication();
        }

        #region 文本润色相关方法

        /// <summary>
        /// 初始化文本润色服务
        /// </summary>
        private async void InitializePolishingService()
        {
            try
            {
                // 初始化HTTP客户端
                _polishingHttpClient = new TextPolishingHttpClient(ApiBaseUrl);
                
                // 初始化模板管理器
                _templateManager = new NET.Services.TemplateManagerService();

                // 加载模型列表（使用默认列表）
                var models = new[] { "deepseek-chat", "deepseek-coder", "gpt-4o", "gpt-4", "gpt-3.5-turbo" };
                cmbPolishingModel.Items.Clear();
                cmbPolishingModel.Items.AddRange(models);
                if (models.Length > 0)
                {
                    cmbPolishingModel.SelectedIndex = 0;
                }

                // 加载模板列表
                await LoadPolishingTemplatesAsync();

                // 初始化控件状态
                UpdatePolishingControlsState();

                // 加载保存的设置
                LoadPolishingSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化文本润色服务失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 加载润色模板
        /// </summary>
        private async Task LoadPolishingTemplatesAsync()
        {
            try
            {
                _availableTemplates = await _templateManager.GetAllTemplatesAsync();
                
                cmbPolishingTemplate.Items.Clear();
                foreach (var template in _availableTemplates)
                {
                    cmbPolishingTemplate.Items.Add($"{template.Name} ({template.Category})");
                }

                if (_availableTemplates.Count > 0)
                {
                    cmbPolishingTemplate.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载润色模板失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 更新润色控件状态
        /// </summary>
        private void UpdatePolishingControlsState()
        {
            bool enabled = chkEnablePolishing.Checked;
            
            lblPolishingModel.Enabled = enabled;
            cmbPolishingModel.Enabled = enabled;
            lblPolishingTemplate.Enabled = enabled;
            cmbPolishingTemplate.Enabled = enabled;
            lblApiKey.Enabled = enabled;
            txtApiKey.Enabled = enabled;
            btnTestConnection.Enabled = enabled && !string.IsNullOrWhiteSpace(txtApiKey.Text);
            btnConfigurePolishing.Enabled = enabled;
        }

        /// <summary>
        /// 加载润色设置
        /// </summary>
        private void LoadPolishingSettings()
        {
            try
            {
                // 使用简单的内存配置
                chkEnablePolishing.Checked = _polishingConfig.EnablePolishing;
                
                // 从凭据管理器加载API Key（默认为DeepSeek）
                string apiKey = CredentialService.GetApiKey("DeepSeek");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    txtApiKey.Text = apiKey;
                    _polishingConfig.ApiKey = apiKey;
                }
                else
                {
                    txtApiKey.Text = "";
                    _polishingConfig.ApiKey = "";
                }
                
                // 设置选中的模型
                if (cmbPolishingModel.Items.Contains(_polishingConfig.SelectedModel))
                {
                    cmbPolishingModel.SelectedItem = _polishingConfig.SelectedModel;
                }
                
                // 设置选中的模板
                var templateIndex = _availableTemplates.FindIndex(t => t.Name == _polishingConfig.SelectedTemplateName);
                if (templateIndex >= 0 && templateIndex < cmbPolishingTemplate.Items.Count)
                {
                    cmbPolishingTemplate.SelectedIndex = templateIndex;
                }
            }
            catch (Exception ex)
            {
                // 忽略加载设置的错误
                Console.WriteLine($"加载润色设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存润色设置
        /// </summary>
        private void SavePolishingSettings()
        {
            try
            {
                // 更新内存配置
                _polishingConfig.EnablePolishing = chkEnablePolishing.Checked;
                _polishingConfig.ApiKey = txtApiKey.Text;
                _polishingConfig.SelectedModel = cmbPolishingModel.SelectedItem?.ToString() ?? "deepseek-chat";
                _polishingConfig.SelectedTemplateName = GetSelectedTemplateName();
                _polishingConfig.ApiBaseUrl = "https://api.deepseek.com/v1";
                _polishingConfig.MaxTokens = 4000;
                _polishingConfig.Temperature = 0.7;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存润色设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前选中的模板名称
        /// </summary>
        private string GetSelectedTemplateName()
        {
            var selectedIndex = cmbPolishingTemplate.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _availableTemplates.Count)
            {
                return _availableTemplates[selectedIndex].Name;
            }
            return "default";
        }

        /// <summary>
        /// 启用润色复选框状态改变事件
        /// </summary>
        private void ChkEnablePolishing_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePolishingControlsState();
            SavePolishingSettings();
        }

        /// <summary>
        /// 测试连接按钮点击事件
        /// </summary>
        private async void BtnTestConnection_Click(object sender, EventArgs e)
        {
            // 从凭据管理器获取API Key
            string apiKey = CredentialService.GetApiKey("DeepSeek");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("请先配置API Key。\n点击\"高级设置\"按钮进行配置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTestConnection.Enabled = false;
            btnTestConnection.Text = "测试中...";

            try
            {
                var testRequest = new ApiConnectionTestRequest
                {
                    ApiKey = apiKey,
                    ApiProvider = "deepseek",
                    ApiBaseUrl = "https://api.deepseek.com/v1",
                    Model = cmbPolishingModel.SelectedItem?.ToString() ?? "deepseek-chat"
                };

                var result = await _polishingHttpClient.TestApiConnectionAsync(testRequest);

                if (result.Success)
                {
                    MessageBox.Show("API连接测试成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"API连接测试失败: {result.ErrorMessage}", "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试连接时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestConnection.Text = "测试连接";
                UpdatePolishingControlsState();
            }
        }

        /// <summary>
        /// 高级设置按钮点击事件 - 打开API配置窗体
        /// </summary>
        private void BtnConfigurePolishing_Click(object sender, EventArgs e)
        {
            try
            {
                using (var configForm = new ApiConfigForm())
                {
                    if (configForm.ShowDialog(this) == DialogResult.OK)
                    {
                        // 重新加载API Key配置
                        LoadPolishingSettings();
                        
                        // 刷新UI显示
                        UpdatePolishingControlsState();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开API配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 模板选择改变事件
        /// </summary>
        private void CmbPolishingTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            SavePolishingSettings();
        }

        /// <summary>
        /// 创建润色请求
        /// </summary>
        private TextPolishingRequest CreatePolishingRequest(string originalText)
        {
            if (!chkEnablePolishing.Checked)
            {
                return null;
            }

            // 从凭据管理器获取API Key
            string apiKey = CredentialService.GetApiKey("DeepSeek");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var selectedTemplateIndex = cmbPolishingTemplate.SelectedIndex;
            if (selectedTemplateIndex < 0 || selectedTemplateIndex >= _availableTemplates.Count)
            {
                return null;
            }

            var selectedTemplate = _availableTemplates[selectedTemplateIndex];
            var selectedModel = cmbPolishingModel.SelectedItem?.ToString() ?? "deepseek-chat";

            return new TextPolishingRequest
            {
                OriginalText = originalText,
                EnablePolishing = true,
                Model = selectedModel,
                TemplateName = selectedTemplate.Name,
                ApiKey = apiKey,
                ApiBaseUrl = "https://api.deepseek.com/v1",
                MaxTokens = 4000,
                Temperature = 0.7
            };
        }

        /// <summary>
        /// 执行文本润色
        /// </summary>
        private async Task PerformTextPolishingAsync(TranscriptionResult result)
        {
            try
            {
                var polishingRequest = CreatePolishingRequest(result.Text);
                if (polishingRequest == null)
                {
                    return;
                }

                // 更新UI状态
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => {
                        btnProcess.Text = "正在润色文本...";
                    }));
                }
                else
                {
                    btnProcess.Text = "正在润色文本...";
                }

                var polishingStartTime = DateTime.UtcNow;

                // 构建HTTP请求
                var selectedTemplate = _availableTemplates[cmbPolishingTemplate.SelectedIndex];
                var httpRequest = new TextPolishingHttpRequest
                {
                    SrtContent = result.SrtContent,
                    TemplateContent = _templateManager.SerializeTemplate(selectedTemplate),
                    ApiKey = polishingRequest.ApiKey,
                    ApiProvider = "deepseek",
                    ApiBaseUrl = polishingRequest.ApiBaseUrl,
                    Model = polishingRequest.Model,
                    MaxTokens = polishingRequest.MaxTokens,
                    Temperature = polishingRequest.Temperature,
                    WindowSize = 2000,
                    OverlapSize = 200
                };

                // 执行文本润色
                var httpResponse = await _polishingHttpClient.PolishSrtAsync(httpRequest);
                
                // 转换为旧的响应格式
                var polishingResult = httpResponse.Success 
                    ? TextPolishingResult.Success(
                        result.Text, 
                        httpResponse.PolishedSrtContent, 
                        polishingRequest.Model, 
                        selectedTemplate.Name, 
                        httpResponse.Statistics?.TotalTokensUsed ?? 0, 
                        httpResponse.Statistics?.TotalProcessingTimeMs ?? 0)
                    : TextPolishingResult.Failure(result.Text, httpResponse.ErrorMessage, polishingRequest.Model, selectedTemplate.Name);

                // 更新转录结果
                result.PolishingEnabled = true;
                result.PolishingResult = polishingResult;
                result.PolishingTemplateName = polishingRequest.TemplateName;
                result.PolishingModel = polishingRequest.Model;
                result.PolishingProcessingTime = (DateTime.UtcNow - polishingStartTime).TotalSeconds;

                if (polishingResult.IsSuccess)
                {
                    result.PolishedText = polishingResult.PolishedText;
                    
                    // 生成润色后的SRT内容
                    if (!string.IsNullOrEmpty(result.SrtContent))
                    {
                        result.PolishingSrtContent = await GeneratePolishedSrtAsync(
                            result.SrtContent, 
                            result.Text, 
                            polishingResult.PolishedText);
                    }
                }

                // 恢复UI状态
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => {
                        btnProcess.Text = "开始处理";
                    }));
                }
                else
                {
                    btnProcess.Text = "开始处理";
                }
            }
            catch (Exception ex)
            {
                // 润色失败，记录错误但不影响主流程
                result.PolishingEnabled = true;
                result.PolishingResult = TextPolishingResult.Failure(
                    result.Text, 
                    ex.Message, 
                    cmbPolishingModel.SelectedItem?.ToString() ?? "deepseek-chat", 
                    _availableTemplates.FirstOrDefault()?.Name ?? "default");
                
                // 恢复UI状态
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => {
                        btnProcess.Text = "开始处理";
                    }));
                }
                else
                {
                    btnProcess.Text = "开始处理";
                }
            }
        }

        /// <summary>
        /// 生成润色后的SRT内容（简化版本）
        /// </summary>
        private async Task<string> GeneratePolishedSrtAsync(string originalSrt, string originalText, string polishedText)
        {
            try
            {
                // 这是一个简化的实现，直接替换文本内容
                // 实际应用中可能需要更复杂的逻辑来处理时间戳对齐
                
                var lines = originalSrt.Split('\n');
                var result = new List<string>();
                
                // 分割原始文本和润色文本为句子
                var originalSentences = SplitIntoSentences(originalText);
                var polishedSentences = SplitIntoSentences(polishedText);
                
                int sentenceIndex = 0;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    
                    // 跳过序号行和时间戳行
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+$") || 
                        System.Text.RegularExpressions.Regex.IsMatch(line, @"\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}"))
                    {
                        result.Add(line);
                    }
                    // 空行
                    else if (string.IsNullOrWhiteSpace(line))
                    {
                        result.Add(line);
                    }
                    // 文本行
                    else
                    {
                        if (sentenceIndex < polishedSentences.Count)
                        {
                            result.Add(polishedSentences[sentenceIndex]);
                            sentenceIndex++;
                        }
                        else
                        {
                            result.Add(line); // 保持原文本
                        }
                    }
                }
                
                return string.Join("\n", result);
            }
            catch (Exception)
            {
                // 如果生成失败，返回原始SRT
                return originalSrt;
            }
        }

        /// <summary>
        /// 将文本分割成句子
        /// </summary>
        private List<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            
            // 使用正则表达式分割句子
            var sentences = System.Text.RegularExpressions.Regex.Split(text, @"[。！？!?]\s*")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            
            return sentences;
        }

        #endregion
    }

    // 简单的配置类
    public class SimplePolishingConfig
    {
        public bool EnablePolishing { get; set; } = false;
        public string ApiKey { get; set; } = string.Empty;
        public string SelectedModel { get; set; } = "deepseek-chat";
        public string SelectedTemplateName { get; set; } = "通用润色";
        public string ApiBaseUrl { get; set; } = "https://api.deepseek.com/v1";
        public int MaxTokens { get; set; } = 4000;
        public double Temperature { get; set; } = 0.7;
    }

    // 本地ApiResponse类定义用于反序列化
    public class ApiResponse<T>
    {
        public string Status { get; set; } = string.Empty;
        public T Data { get; set; }
        public string Error { get; set; }
        public string ErrorCode { get; set; }
    }
}
