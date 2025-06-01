using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using OWhisper.Core.Models;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Text;
using Newtonsoft.Json;
using TaskStatus = OWhisper.Core.Models.TaskStatus;

namespace OWhisper.NET
{
    public partial class MainForm : Form
    {
        private string _selectedAudioFile;
        private string _outputFilePath;
        private readonly HttpClient _httpClient;
        private string _currentTaskId;
        private CancellationTokenSource _sseCancellationToken;
        
        // 动态获取API基础URL
        private string ApiBaseUrl => $"http://localhost:{Program.GetListenPort()}";

        public MainForm()
        {
            InitializeComponent();
            
            // 初始化HTTP客户端
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(30); // 设置较长的超时时间
            
            // 绑定事件处理程序
            btnSelectFile.Click += BtnSelectFile_Click;
            btnSelectFolder.Click += BtnSelectOutput_Click;
            btnProcess.Click += BtnProcess_Click;
            
            // 设置初始placeholder文本
            txtSelectedFile.Text = "请选择音频文件...";
            txtSelectedFile.ForeColor = System.Drawing.SystemColors.GrayText;
            txtOutputPath.Text = "请选择保存位置...";
            txtOutputPath.ForeColor = System.Drawing.SystemColors.GrayText;
            
            // 默认窗口状态为最小化
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            
            // 绑定Disposed事件来清理资源
            this.Disposed += (s, e) => {
                _httpClient?.Dispose();
                _sseCancellationToken?.Cancel();
                _sseCancellationToken?.Dispose();
            };
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
            btnProcess.Text = "处理中...";

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
                    btnProcess.Text = $"队列位置: {taskResponse.QueuePosition}";
                    
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
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"无法连接到API服务: {ex.Message}");
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
                    default:
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        break;
                }

                form.Add(fileContent, "file", fileName);

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

        private void UpdateProgressUI(TranscriptionProgress progress)
        {
            progressBar.Value = Math.Min(100, Math.Max(0, (int)progress.Progress));
            
            switch (progress.Status)
            {
                case TaskStatus.Queued:
                    btnProcess.Text = $"队列位置: {progress.QueuePosition}";
                    break;
                case TaskStatus.Processing:
                    btnProcess.Text = $"处理中... {progress.Progress:F1}%";
                    break;
                case TaskStatus.Completed:
                    btnProcess.Text = "处理完成";
                    break;
                case TaskStatus.Failed:
                    btnProcess.Text = "处理失败";
                    break;
                case TaskStatus.Cancelled:
                    btnProcess.Text = "已取消";
                    break;
            }
        }

        private async Task HandleTaskCompletion(TranscriptionResult result)
        {
            try
            {
                // 根据文件扩展名保存不同格式
                string content;
                string extension = Path.GetExtension(_outputFilePath).ToLower();
                
                if (extension == ".srt")
                {
                    content = result.SrtContent;
                }
                else
                {
                    content = result.Text;
                }
                
                // 保存转写结果
                File.WriteAllText(_outputFilePath, content, System.Text.Encoding.UTF8);

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => {
                        MessageBox.Show($"处理完成！\n耗时: {result.ProcessingTime:F1}秒\n保存到: {_outputFilePath}", 
                            "处理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                else
                {
                    MessageBox.Show($"处理完成！\n耗时: {result.ProcessingTime:F1}秒\n保存到: {_outputFilePath}", 
                        "处理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                var jsonObject = JObject.Parse(jsonResponse);
                
                var status = jsonObject["Status"]?.ToString();
                if (status != "success")
                {
                    var errorMessage = jsonObject["Error"]?.ToString() ?? 
                                     jsonObject["ErrorCode"]?.ToString() ?? "未知错误";
                    throw new Exception($"API返回错误: {errorMessage}");
                }
                
                var data = jsonObject["Data"];
                if (data == null)
                {
                    throw new Exception("API响应中缺少数据字段");
                }
                
                return new TaskCreationResponse
                {
                    TaskId = data["TaskId"]?.ToString(),
                    QueuePosition = data["QueuePosition"]?.Value<int>() ?? 0
                };
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new Exception($"JSON解析失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"解析响应失败: {ex.Message}");
            }
        }
    }
}
