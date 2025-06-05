using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text;
using Newtonsoft.Json;
using OWhisper.Core.Services;
using Serilog;
using Whisper.net.Ggml;
using System.Security.Cryptography;

namespace OWhisper.NET
{
    public partial class ModelManagerForm : Form
    {
        private readonly IPlatformPathService _pathService;
        private readonly WhisperManager _whisperManager;
        private readonly System.Windows.Forms.Timer _statusUpdateTimer;
        private readonly Dictionary<string, ModelInfo> _availableModels;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDownloading = false;

        // 可用模型配置
        private readonly Dictionary<string, ModelConfig> _modelConfigs = new Dictionary<string, ModelConfig>
        {
            ["ggml-large-v3-turbo.bin"] = new ModelConfig 
            { 
                Name = "Large V3 Turbo", 
                FileName = "ggml-large-v3-turbo.bin",
                GgmlType = GgmlType.LargeV3Turbo,
                Size = "809MB",
                Description = "最新的Turbo版本，速度最快，推荐使用",
                Sha256 = "1fc70f774d38eb169993ac391eea357ef47c88757ef72ee5943879b7e8e2bc69",
                DownloadUrl = "https://velopack.miaostay.com/models/ggml-large-v3-turbo.bin"
            },
            ["ggml-large-v3.bin"] = new ModelConfig 
            { 
                Name = "Large V3", 
                FileName = "ggml-large-v3.bin",
                GgmlType = GgmlType.LargeV3,
                Size = "1.5GB",
                Description = "最高精度版本，适合对准确性要求极高的场景",
                Sha256 = "",
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin"
            },
            ["ggml-medium.bin"] = new ModelConfig 
            { 
                Name = "Medium", 
                FileName = "ggml-medium.bin",
                GgmlType = GgmlType.Medium,
                Size = "769MB",
                Description = "平衡版本，性能和准确性的良好平衡",
                Sha256 = "",
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin"
            },
            ["ggml-small.bin"] = new ModelConfig 
            { 
                Name = "Small", 
                FileName = "ggml-small.bin",
                GgmlType = GgmlType.Small,
                Size = "244MB",
                Description = "轻量级版本，适合资源受限的环境",
                Sha256 = "",
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin"
            }
        };

        public ModelManagerForm()
        {
            InitializeComponent();
            
            _pathService = new PlatformPathService();
            _whisperManager = new WhisperManager(_pathService);
            _availableModels = new Dictionary<string, ModelInfo>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 设置状态更新定时器
            _statusUpdateTimer = new System.Windows.Forms.Timer();
            _statusUpdateTimer.Interval = 1000; // 每秒更新一次
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
            
            // 绑定事件
            this.FormClosing += ModelManagerForm_FormClosing;
            this.Load += ModelManagerForm_Load;
            
            // 初始化界面数据
            LoadModelsList();
            UpdateCurrentModelStatus();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // 窗体设置
            this.Text = "模型管理";
            this.Size = new Size(800, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(700, 600);
            this.Icon = LoadAppIcon();
            
            // 创建主要控件
            CreateControls();
            
            this.ResumeLayout(false);
        }

        private void CreateControls()
        {
            // 主面板
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            
            // 设置行高
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // 当前模型状态
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // 操作按钮
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 可用模型列表
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // 进度信息
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // 底部按钮
            
            // 当前模型状态面板
            var statusPanel = CreateCurrentModelStatusPanel();
            mainPanel.Controls.Add(statusPanel, 0, 0);
            
            // 操作按钮面板
            var actionPanel = CreateActionButtonsPanel();
            mainPanel.Controls.Add(actionPanel, 0, 1);
            
            // 可用模型列表面板
            var modelsPanel = CreateModelsListPanel();
            mainPanel.Controls.Add(modelsPanel, 0, 2);
            
            // 进度信息面板
            var progressPanel = CreateProgressPanel();
            mainPanel.Controls.Add(progressPanel, 0, 3);
            
            // 底部控制按钮面板
            var controlPanel = CreateControlPanel();
            mainPanel.Controls.Add(controlPanel, 0, 4);
            
            this.Controls.Add(mainPanel);
        }

        private Panel CreateCurrentModelStatusPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            
            var titleLabel = new Label
            {
                Text = "当前模型状态",
                Location = new Point(10, 10),
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                AutoSize = true
            };
            
            lblCurrentModel = new Label
            {
                Location = new Point(10, 35),
                Size = new Size(400, 20),
                Text = "正在检查..."
            };
            
            lblModelStatus = new Label
            {
                Location = new Point(10, 55),
                Size = new Size(400, 20),
                Text = ""
            };
            
            panel.Controls.AddRange(new Control[] { titleLabel, lblCurrentModel, lblModelStatus });
            
            return panel;
        }

        private Panel CreateActionButtonsPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var btnImportModel = new Button
            {
                Text = "导入模型文件",
                Location = new Point(10, 10),
                Size = new Size(120, 30),
                BackColor = Color.LightBlue
            };
            btnImportModel.Click += BtnImportModel_Click;
            
            var btnDeleteModel = new Button
            {
                Text = "删除当前模型",
                Location = new Point(140, 10),
                Size = new Size(120, 30),
                BackColor = Color.LightCoral
            };
            btnDeleteModel.Click += BtnDeleteModel_Click;
            
            var btnRefreshStatus = new Button
            {
                Text = "刷新状态",
                Location = new Point(270, 10),
                Size = new Size(100, 30)
            };
            btnRefreshStatus.Click += BtnRefreshStatus_Click;
            
            var btnOpenModelFolder = new Button
            {
                Text = "打开模型文件夹",
                Location = new Point(380, 10),
                Size = new Size(120, 30)
            };
            btnOpenModelFolder.Click += BtnOpenModelFolder_Click;
            
            panel.Controls.AddRange(new Control[] { btnImportModel, btnDeleteModel, btnRefreshStatus, btnOpenModelFolder });
            
            return panel;
        }

        private Panel CreateModelsListPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var titleLabel = new Label
            {
                Text = "可用模型下载",
                Location = new Point(10, 5),
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                AutoSize = true
            };
            
            // 创建ListView来显示可用模型
            listViewModels = new ListView
            {
                Location = new Point(10, 30),
                Size = new Size(panel.Width - 20, panel.Height - 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            
            // 添加列
            listViewModels.Columns.Add("模型名称", 150);
            listViewModels.Columns.Add("文件名", 180);
            listViewModels.Columns.Add("大小", 80);
            listViewModels.Columns.Add("状态", 100);
            listViewModels.Columns.Add("描述", 250);
            
            var btnDownloadSelected = new Button
            {
                Text = "下载选中模型",
                Location = new Point(10, panel.Height - 40),
                Size = new Size(120, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.LightGreen
            };
            btnDownloadSelected.Click += BtnDownloadSelected_Click;
            
            var btnDownloadInBrowser = new Button
            {
                Text = "在浏览器中下载",
                Location = new Point(140, panel.Height - 40),
                Size = new Size(130, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.LightSkyBlue
            };
            btnDownloadInBrowser.Click += BtnDownloadInBrowser_Click;
            
            panel.Controls.AddRange(new Control[] { titleLabel, listViewModels, btnDownloadSelected, btnDownloadInBrowser });
            
            return panel;
        }

        private Panel CreateProgressPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            
            var titleLabel = new Label
            {
                Text = "下载进度",
                Location = new Point(10, 5),
                Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
                AutoSize = true
            };
            
            progressBar = new ProgressBar
            {
                Location = new Point(10, 25),
                Size = new Size(400, 20),
                Style = ProgressBarStyle.Continuous
            };
            
            lblProgress = new Label
            {
                Location = new Point(420, 25),
                Size = new Size(200, 20),
                Text = "就绪"
            };
            
            lblDownloadSpeed = new Label
            {
                Location = new Point(10, 50),
                Size = new Size(300, 20),
                Text = ""
            };
            
            var btnCancelDownload = new Button
            {
                Text = "取消下载",
                Location = new Point(650, 25),
                Size = new Size(80, 30),
                BackColor = Color.LightYellow,
                Enabled = false
            };
            btnCancelDownload.Click += BtnCancelDownload_Click;
            
            this.btnCancelDownload = btnCancelDownload; // 保存引用
            
            panel.Controls.AddRange(new Control[] { titleLabel, progressBar, lblProgress, lblDownloadSpeed, btnCancelDownload });
            
            return panel;
        }

        private Panel CreateControlPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var btnClose = new Button
            {
                Text = "关闭",
                Location = new Point(panel.Width - 80, 15),
                Size = new Size(60, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => this.Hide();
            
            panel.Controls.Add(btnClose);
            
            return panel;
        }

        // 控件字段
        private Label lblCurrentModel;
        private Label lblModelStatus;
        private ListView listViewModels;
        private ProgressBar progressBar;
        private Label lblProgress;
        private Label lblDownloadSpeed;
        private Button btnCancelDownload;

        private void LoadModelsList()
        {
            listViewModels.Items.Clear();
            
            foreach (var config in _modelConfigs.Values)
            {
                var item = new ListViewItem(config.Name);
                item.SubItems.Add(config.FileName);
                item.SubItems.Add(config.Size);
                item.SubItems.Add(GetModelLocalStatus(config.FileName));
                item.SubItems.Add(config.Description);
                item.Tag = config;
                
                // 设置颜色
                if (IsModelInstalled(config.FileName))
                {
                    item.BackColor = Color.LightGreen;
                }
                
                listViewModels.Items.Add(item);
            }
        }

        private string GetModelLocalStatus(string fileName)
        {
            var modelPath = Path.Combine(_pathService.GetModelsPath(), fileName);
            if (!File.Exists(modelPath))
            {
                return "未安装";
            }
            
            var fileInfo = new FileInfo(modelPath);
            if (fileInfo.Length < 1024 * 1024) // 小于1MB认为是损坏的
            {
                return "文件损坏";
            }
            
            return "已安装";
        }

        private bool IsModelInstalled(string fileName)
        {
            var modelPath = Path.Combine(_pathService.GetModelsPath(), fileName);
            return File.Exists(modelPath) && new FileInfo(modelPath).Length > 1024 * 1024;
        }

        private void UpdateCurrentModelStatus()
        {
            try
            {
                var (exists, valid, size, path) = _whisperManager.CheckModelStatus();
                
                if (exists)
                {
                    var fileName = Path.GetFileName(path);
                    lblCurrentModel.Text = $"当前模型: {fileName}";
                    
                    if (valid)
                    {
                        lblModelStatus.Text = $"状态: 有效 | 大小: {FormatFileSize(size)}";
                        lblModelStatus.ForeColor = Color.Green;
                    }
                    else
                    {
                        lblModelStatus.Text = $"状态: 无效或损坏 | 大小: {FormatFileSize(size)}";
                        lblModelStatus.ForeColor = Color.Red;
                    }
                }
                else
                {
                    lblCurrentModel.Text = "当前模型: 未安装";
                    lblModelStatus.Text = "状态: 无模型文件";
                    lblModelStatus.ForeColor = Color.Orange;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "更新模型状态失败");
                lblCurrentModel.Text = "当前模型: 检查失败";
                lblModelStatus.Text = $"状态: 错误 - {ex.Message}";
                lblModelStatus.ForeColor = Color.Red;
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        private void BtnImportModel_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择模型文件";
                dialog.Filter = "模型文件|*.bin;*.gguf|所有文件|*.*";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _ = ImportModelAsync(dialog.FileName);
                }
            }
        }

        private async Task ImportModelAsync(string sourceFilePath)
        {
            try
            {
                progressBar.Style = ProgressBarStyle.Marquee;
                lblProgress.Text = "正在导入模型...";
                btnCancelDownload.Enabled = false;
                
                var fileName = Path.GetFileName(sourceFilePath);
                var targetPath = Path.Combine(_pathService.GetModelsPath(), fileName);
                
                // 确保目标目录存在
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                
                // 检查源文件大小
                var sourceFileInfo = new FileInfo(sourceFilePath);
                if (sourceFileInfo.Length < 1024 * 1024) // 小于1MB
                {
                    MessageBox.Show("选择的文件太小，可能不是有效的模型文件", "导入失败", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // 复制文件
                await Task.Run(() =>
                {
                    File.Copy(sourceFilePath, targetPath, true);
                });
                
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 100;
                lblProgress.Text = "导入完成";
                
                MessageBox.Show($"模型文件已成功导入到:\n{targetPath}", "导入成功", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                // 更新界面
                UpdateCurrentModelStatus();
                LoadModelsList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导入模型失败");
                MessageBox.Show($"导入模型失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
                lblProgress.Text = "就绪";
            }
        }

        private void BtnDeleteModel_Click(object sender, EventArgs e)
        {
            var (exists, valid, size, path) = _whisperManager.CheckModelStatus();
            
            if (!exists)
            {
                MessageBox.Show("当前没有模型文件可以删除", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var fileName = Path.GetFileName(path);
            var result = MessageBox.Show($"确定要删除模型文件吗？\n\n文件: {fileName}\n大小: {FormatFileSize(size)}\n\n删除后需要重新下载才能使用转录功能。", 
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                try
                {
                    File.Delete(path);
                    MessageBox.Show("模型文件已删除", "删除成功", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // 更新界面
                    UpdateCurrentModelStatus();
                    LoadModelsList();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "删除模型文件失败");
                    MessageBox.Show($"删除失败: {ex.Message}", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnRefreshStatus_Click(object sender, EventArgs e)
        {
            UpdateCurrentModelStatus();
            LoadModelsList();
        }

        private void BtnOpenModelFolder_Click(object sender, EventArgs e)
        {
            try
            {
                var modelsPath = _pathService.GetModelsPath();
                if (!Directory.Exists(modelsPath))
                {
                    Directory.CreateDirectory(modelsPath);
                }
                
                System.Diagnostics.Process.Start("explorer.exe", modelsPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开模型文件夹失败");
                MessageBox.Show($"打开文件夹失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDownloadSelected_Click(object sender, EventArgs e)
        {
            if (listViewModels.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要下载的模型", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            if (_isDownloading)
            {
                MessageBox.Show("正在下载模型，请等待当前下载完成", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            var selectedItem = listViewModels.SelectedItems[0];
            var config = selectedItem.Tag as ModelConfig;
            
            if (IsModelInstalled(config.FileName))
            {
                var result = MessageBox.Show($"模型 {config.Name} 已安装，是否重新下载？", 
                    "确认下载", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }
            
            _ = DownloadModelAsync(config);
        }

        private void BtnDownloadInBrowser_Click(object sender, EventArgs e)
        {
            if (listViewModels.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要下载的模型", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var selectedItem = listViewModels.SelectedItems[0];
            var config = selectedItem.Tag as ModelConfig;
            
            if (string.IsNullOrEmpty(config.DownloadUrl))
            {
                MessageBox.Show("该模型没有可用的下载链接", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            try
            {
                // 在默认浏览器中打开下载链接
                System.Diagnostics.Process.Start(config.DownloadUrl);
                
                // 显示提示信息
                var message = $"已在浏览器中打开模型下载页面：\n\n" +
                             $"模型：{config.Name}\n" +
                             $"文件名：{config.FileName}\n" +
                             $"大小：{config.Size}\n\n" +
                             $"下载完成后，请使用\"导入模型文件\"功能将文件导入到应用程序中。";
                
                MessageBox.Show(message, "浏览器下载", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开浏览器下载链接失败");
                
                // 如果无法打开浏览器，显示下载链接供用户复制
                var fallbackMessage = $"无法自动打开浏览器，请手动复制以下链接到浏览器中下载：\n\n" +
                                     $"{config.DownloadUrl}\n\n" +
                                     $"下载完成后，请使用\"导入模型文件\"功能导入。";
                
                MessageBox.Show(fallbackMessage, "下载链接", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task DownloadModelAsync(ModelConfig config)
        {
            _isDownloading = true;
            btnCancelDownload.Enabled = true;
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                progressBar.Value = 0;
                lblProgress.Text = $"开始下载 {config.Name}...";
                lblDownloadSpeed.Text = "";
                
                var modelsPath = _pathService.GetModelsPath();
                if (!Directory.Exists(modelsPath))
                {
                    Directory.CreateDirectory(modelsPath);
                }
                
                // 定义进度回调函数
                Action<long, long, double, double> progressCallback = (downloadedBytes, totalBytes, progressPercent, speedMBs) =>
                {
                    // 在UI线程中更新界面
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => UpdateDownloadProgress(downloadedBytes, totalBytes, progressPercent, speedMBs)));
                    }
                    else
                    {
                        UpdateDownloadProgress(downloadedBytes, totalBytes, progressPercent, speedMBs);
                    }
                };
                
                // 使用 WhisperManager 的下载功能，传入进度回调
                await _whisperManager.DownloadModelAsync(config.GgmlType, modelsPath, progressCallback, _cancellationTokenSource.Token);
                
                progressBar.Value = 100;
                lblProgress.Text = "下载完成";
                lblDownloadSpeed.Text = "";
                
                MessageBox.Show($"模型 {config.Name} 下载完成！", "下载成功", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                // 更新界面
                UpdateCurrentModelStatus();
                LoadModelsList();
            }
            catch (OperationCanceledException)
            {
                lblProgress.Text = "下载已取消";
                lblDownloadSpeed.Text = "";
                Log.Information("用户取消了模型下载");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "下载模型失败");
                lblProgress.Text = "下载失败";
                lblDownloadSpeed.Text = "";
                
                MessageBox.Show($"下载模型失败: {ex.Message}", "下载失败", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isDownloading = false;
                btnCancelDownload.Enabled = false;
                progressBar.Value = 0;
            }
        }

        private void UpdateDownloadProgress(long downloadedBytes, long totalBytes, double progressPercent, double speedMBs)
        {
            var downloadedMB = downloadedBytes / (1024.0 * 1024.0);
            
            if (totalBytes > 0)
            {
                var totalMB = totalBytes / (1024.0 * 1024.0);
                var remainingBytes = totalBytes - downloadedBytes;
                var remainingSeconds = speedMBs > 0 ? (remainingBytes / (1024.0 * 1024.0)) / speedMBs : 0;
                var remainingTime = TimeSpan.FromSeconds(remainingSeconds);
                
                progressBar.Value = Math.Min(100, (int)progressPercent);
                lblProgress.Text = $"下载中: {downloadedMB:F1}MB / {totalMB:F1}MB ({progressPercent:F1}%)";
                lblDownloadSpeed.Text = $"速度: {speedMBs:F2}MB/s | 剩余时间: {remainingTime:mm\\:ss}";
            }
            else
            {
                // 无法获取总大小的情况（如HuggingFace下载）
                lblProgress.Text = $"下载中: {downloadedMB:F1}MB";
                lblDownloadSpeed.Text = $"速度: {speedMBs:F2}MB/s";
            }
        }

        private void BtnCancelDownload_Click(object sender, EventArgs e)
        {
            if (_isDownloading && _cancellationTokenSource != null)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                    lblProgress.Text = "正在取消下载...";
                    lblDownloadSpeed.Text = "";
                    btnCancelDownload.Enabled = false;
                    Log.Information("用户请求取消下载");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "取消下载时发生错误");
                }
            }
        }

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            // 这里可以添加定期状态更新逻辑
        }

        private void ModelManagerForm_Load(object sender, EventArgs e)
        {
            // 窗体加载时的初始化
        }

        private void ModelManagerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 窗体关闭时隐藏而不是销毁
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private Icon LoadAppIcon()
        {
            try
            {
                // 使用与TrayApp相同的图标加载逻辑
                string iconPath = Path.Combine(Application.StartupPath, "Resources", "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
                
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("OWhisper.NET.Resources.app_icon.ico"))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "加载应用图标失败");
            }
            
            return new Icon(SystemIcons.Application, 32, 32);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _statusUpdateTimer?.Stop();
                _statusUpdateTimer?.Dispose();
                _whisperManager?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }

    // 模型配置信息
    public class ModelConfig
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public GgmlType GgmlType { get; set; }
        public string Size { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    // 模型信息
    public class ModelInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
} 