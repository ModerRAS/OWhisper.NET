using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using OWhisper.NET.Models;
using Newtonsoft.Json.Linq;

namespace OWhisper.NET
{
    public partial class MainForm : Form
    {
        private string _selectedAudioFile;
        private string _outputFilePath;
        private readonly HttpClient _httpClient;
        
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
            
            // 绑定Disposed事件来清理HttpClient
            this.Disposed += (s, e) => _httpClient?.Dispose();
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

            try
            {
                // 检查API服务是否可用
                await CheckApiStatus();

                // 读取音频文件
                byte[] audioBytes = File.ReadAllBytes(_selectedAudioFile);
                string fileName = Path.GetFileName(_selectedAudioFile);

                // 调用API进行转写
                var result = await CallTranscribeApi(audioBytes, fileName);

                if (result != null)
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

                    MessageBox.Show($"处理完成！\n耗时: {result.ProcessingTime:F1}秒\n保存到: {_outputFilePath}", 
                        "处理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            }
        }

        private async Task CheckApiStatus()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/api/status");
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

        private async Task<TranscriptionResult> CallTranscribeApi(byte[] audioData, string fileName)
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
                    return ParseTranscriptionResponse(responseContent);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API调用失败: {response.StatusCode}\n{errorContent}");
                }
            }
        }

        private TranscriptionResult ParseTranscriptionResponse(string jsonResponse)
        {
            try
            {
                // 使用Newtonsoft.Json解析响应
                var jsonObject = JObject.Parse(jsonResponse);
                
                // 检查状态 - API使用大写字段名
                var status = jsonObject["Status"]?.ToString();
                if (status != "success")
                {
                    var errorMessage = jsonObject["Error"]?.ToString() ?? 
                                     jsonObject["ErrorCode"]?.ToString() ?? "未知错误";
                    throw new Exception($"API返回错误: {errorMessage}");
                }
                
                // 提取数据 - API使用大写字段名
                var data = jsonObject["Data"];
                if (data == null)
                {
                    throw new Exception("API响应中缺少数据字段");
                }
                
                // 修复字段名：API返回的TranscriptionResult使用大写字段名
                var text = data["Text"]?.ToString() ?? "";
                var srtContent = data["SrtContent"]?.ToString() ?? "";
                var processingTimeValue = data["ProcessingTime"]?.Value<double>() ?? 0.0;
                
                return new TranscriptionResult
                {
                    Text = text,
                    SrtContent = srtContent,
                    ProcessingTime = processingTimeValue
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
