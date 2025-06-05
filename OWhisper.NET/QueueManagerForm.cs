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
using OWhisper.Core.Models;
using OWhisper.Core.Services;
using Serilog;
using TaskStatus = OWhisper.Core.Models.TaskStatus;

namespace OWhisper.NET
{
    public partial class QueueManagerForm : Form
    {
        private readonly HttpClient _httpClient;
        private readonly ITranscriptionQueueService _queueService;
        private readonly System.Windows.Forms.Timer _statusUpdateTimer;
        private readonly Dictionary<string, QueueTaskItem> _queueTasks;
        private CancellationTokenSource _cancellationTokenSource;
        private string _outputBaseFolder;

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

        public QueueManagerForm()
        {
            InitializeComponent();
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            _queueService = TranscriptionQueueService.Instance;
            _queueTasks = new Dictionary<string, QueueTaskItem>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 启动队列服务
            _queueService.Start();
            
            // 设置状态更新定时器
            _statusUpdateTimer = new System.Windows.Forms.Timer();
            _statusUpdateTimer.Interval = 1000; // 每秒更新一次
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
            
            // 订阅队列服务的进度更新事件
            _queueService.ProgressUpdated += OnQueueProgressUpdated;
            
            // 设置默认输出文件夹
            _outputBaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OWhisper转录结果");
            if (!Directory.Exists(_outputBaseFolder))
            {
                Directory.CreateDirectory(_outputBaseFolder);
            }
            txtOutputFolder.Text = _outputBaseFolder;
            
            // 绑定事件
            this.FormClosing += QueueManagerForm_FormClosing;
            this.Load += QueueManagerForm_Load;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // 窗体设置
            this.Text = "队列识别管理";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(700, 500);
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
                RowCount = 4,
                Padding = new Padding(10)
            };
            
            // 设置行高
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            
            // 文件选择面板
            var filePanel = CreateFileSelectionPanel();
            mainPanel.Controls.Add(filePanel, 0, 0);
            
            // 输出文件夹面板
            var outputPanel = CreateOutputFolderPanel();
            mainPanel.Controls.Add(outputPanel, 0, 1);
            
            // 任务列表面板
            var taskPanel = CreateTaskListPanel();
            mainPanel.Controls.Add(taskPanel, 0, 2);
            
            // 控制按钮面板
            var controlPanel = CreateControlPanel();
            mainPanel.Controls.Add(controlPanel, 0, 3);
            
            this.Controls.Add(mainPanel);
        }

        private Panel CreateFileSelectionPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var label = new Label
            {
                Text = "选择音频文件:",
                Location = new Point(0, 15),
                AutoSize = true
            };
            
            var btnAddFiles = new Button
            {
                Text = "添加文件",
                Location = new Point(100, 10),
                Size = new Size(80, 30)
            };
            btnAddFiles.Click += BtnAddFiles_Click;
            
            var btnAddFolder = new Button
            {
                Text = "添加文件夹",
                Location = new Point(190, 10),
                Size = new Size(80, 30)
            };
            btnAddFolder.Click += BtnAddFolder_Click;
            
            var btnClearAll = new Button
            {
                Text = "清空列表",
                Location = new Point(280, 10),
                Size = new Size(80, 30)
            };
            btnClearAll.Click += BtnClearAll_Click;
            
            panel.Controls.AddRange(new Control[] { label, btnAddFiles, btnAddFolder, btnClearAll });
            
            return panel;
        }

        private Panel CreateOutputFolderPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var label = new Label
            {
                Text = "输出文件夹:",
                Location = new Point(0, 15),
                AutoSize = true
            };
            
            txtOutputFolder = new TextBox
            {
                Location = new Point(100, 12),
                Size = new Size(400, 25),
                ReadOnly = true
            };
            
            var btnBrowseOutput = new Button
            {
                Text = "浏览",
                Location = new Point(510, 10),
                Size = new Size(60, 30)
            };
            btnBrowseOutput.Click += BtnBrowseOutput_Click;
            
            panel.Controls.AddRange(new Control[] { label, txtOutputFolder, btnBrowseOutput });
            
            return panel;
        }

        private Panel CreateTaskListPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            
            // 创建ListView来显示任务
            listViewTasks = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            
            // 添加列
            listViewTasks.Columns.Add("文件名", 200);
            listViewTasks.Columns.Add("状态", 80);
            listViewTasks.Columns.Add("进度", 80);
            listViewTasks.Columns.Add("队列位置", 80);
            listViewTasks.Columns.Add("添加时间", 120);
            listViewTasks.Columns.Add("完成时间", 120);
            listViewTasks.Columns.Add("输出文件", 150);
            
            panel.Controls.Add(listViewTasks);
            
            return panel;
        }

        private Panel CreateControlPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var btnStartQueue = new Button
            {
                Text = "开始处理",
                Location = new Point(10, 10),
                Size = new Size(80, 30),
                BackColor = Color.LightGreen
            };
            btnStartQueue.Click += BtnStartQueue_Click;
            
            var btnPauseQueue = new Button
            {
                Text = "暂停处理",
                Location = new Point(100, 10),
                Size = new Size(80, 30),
                BackColor = Color.LightYellow
            };
            btnPauseQueue.Click += BtnPauseQueue_Click;
            
            var btnRemoveSelected = new Button
            {
                Text = "移除选中",
                Location = new Point(190, 10),
                Size = new Size(80, 30),
                BackColor = Color.LightCoral
            };
            btnRemoveSelected.Click += BtnRemoveSelected_Click;
            
            // 状态标签
            lblStatus = new Label
            {
                Text = "就绪",
                Location = new Point(10, 50),
                AutoSize = true,
                ForeColor = Color.Blue
            };
            
            // 进度信息
            lblProgress = new Label
            {
                Text = "队列: 0/0",
                Location = new Point(300, 50),
                AutoSize = true
            };
            
            panel.Controls.AddRange(new Control[] { btnStartQueue, btnPauseQueue, btnRemoveSelected, lblStatus, lblProgress });
            
            return panel;
        }

        // 控件字段
        private TextBox txtOutputFolder;
        private ListView listViewTasks;
        private Label lblStatus;
        private Label lblProgress;

        private void BtnAddFiles_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择音频文件";
                dialog.Filter = "音频文件|*.wav;*.mp3;*.flac;*.ogg;*.m4a;*.wma;*.aac|所有文件|*.*";
                dialog.Multiselect = true;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (var filePath in dialog.FileNames)
                    {
                        AddFileToQueue(filePath);
                    }
                }
            }
        }

        private void BtnAddFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择包含音频文件的文件夹";
                dialog.ShowNewFolderButton = false;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var audioExtensions = new[] { ".wav", ".mp3", ".flac", ".ogg", ".m4a", ".wma", ".aac" };
                    var files = Directory.GetFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToList();
                    
                    foreach (var filePath in files)
                    {
                        AddFileToQueue(filePath);
                    }
                    
                    MessageBox.Show($"已添加 {files.Count} 个音频文件到队列", "添加完成", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnClearAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要清空所有任务吗？", "确认", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _queueTasks.Clear();
                listViewTasks.Items.Clear();
                UpdateStatusDisplay();
            }
        }

        private void BtnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择输出文件夹";
                dialog.SelectedPath = _outputBaseFolder;
                dialog.ShowNewFolderButton = true;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _outputBaseFolder = dialog.SelectedPath;
                    txtOutputFolder.Text = _outputBaseFolder;
                }
            }
        }

        private void BtnStartQueue_Click(object sender, EventArgs e)
        {
            if (_queueTasks.Count == 0)
            {
                MessageBox.Show("请先添加音频文件到队列", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            _ = StartProcessingQueueAsync();
        }

        private void BtnPauseQueue_Click(object sender, EventArgs e)
        {
            _queueService.Stop();
            lblStatus.Text = "已暂停";
            lblStatus.ForeColor = Color.Orange;
        }

        private void BtnRemoveSelected_Click(object sender, EventArgs e)
        {
            if (listViewTasks.SelectedItems.Count > 0)
            {
                var selectedItem = listViewTasks.SelectedItems[0];
                var taskId = selectedItem.Tag as string;
                
                if (taskId != null && _queueTasks.ContainsKey(taskId))
                {
                    _queueService.CancelTask(taskId);
                    _queueTasks.Remove(taskId);
                    listViewTasks.Items.Remove(selectedItem);
                    UpdateStatusDisplay();
                }
            }
        }

        private void AddFileToQueue(string filePath)
        {
            // 检查文件是否已经在队列中
            if (_queueTasks.Values.Any(t => t.FilePath == filePath))
            {
                return; // 文件已存在，跳过
            }
            
            var fileName = Path.GetFileName(filePath);
            var taskItem = new QueueTaskItem
            {
                FilePath = filePath,
                FileName = fileName,
                Status = TaskStatus.Queued,
                AddedTime = DateTime.Now,
                OutputPath = GenerateOutputPath(filePath)
            };
            
            // 添加到ListView
            var listItem = new ListViewItem(fileName);
            listItem.SubItems.Add("等待中");
            listItem.SubItems.Add("0%");
            listItem.SubItems.Add("-");
            listItem.SubItems.Add(taskItem.AddedTime.ToString("HH:mm:ss"));
            listItem.SubItems.Add("-");
            listItem.SubItems.Add(taskItem.OutputPath);
            listItem.Tag = taskItem.TaskId;
            
            listViewTasks.Items.Add(listItem);
            _queueTasks[taskItem.TaskId] = taskItem;
            
            UpdateStatusDisplay();
        }

        private string GenerateOutputPath(string inputFilePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(inputFilePath);
            var outputDir = Path.Combine(_outputBaseFolder, DateTime.Now.ToString("yyyy-MM-dd"));
            
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            return Path.Combine(outputDir, $"{fileName}.txt");
        }

        private async Task StartProcessingQueueAsync()
        {
            try
            {
                lblStatus.Text = "正在处理...";
                lblStatus.ForeColor = Color.Green;
                
                // 启动队列服务
                _queueService.Start();
                
                // 提交未处理的任务
                foreach (var taskItem in _queueTasks.Values.Where(t => t.Status == TaskStatus.Queued))
                {
                    await SubmitTaskAsync(taskItem);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "启动队列处理失败");
                lblStatus.Text = "启动失败";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"启动处理失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SubmitTaskAsync(QueueTaskItem taskItem)
        {
            try
            {
                // 读取文件
                var audioData = File.ReadAllBytes(taskItem.FilePath);
                
                // 提交到队列服务
                var taskId = _queueService.EnqueueTask(audioData, taskItem.FileName);
                
                // 更新任务ID
                taskItem.TaskId = taskId;
                taskItem.Status = TaskStatus.Queued;
                
                // 更新ListView中的Tag
                var listItem = listViewTasks.Items.Cast<ListViewItem>()
                    .FirstOrDefault(item => item.Text == taskItem.FileName);
                if (listItem != null)
                {
                    listItem.Tag = taskId;
                }
                
                Log.Information("任务已提交: {FileName} -> {TaskId}", taskItem.FileName, taskId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "提交任务失败: {FileName}", taskItem.FileName);
                taskItem.Status = TaskStatus.Failed;
                taskItem.ErrorMessage = ex.Message;
            }
        }

        private void OnQueueProgressUpdated(object sender, TranscriptionTask task)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, TranscriptionTask>(OnQueueProgressUpdated), sender, task);
                return;
            }
            
            // 查找对应的任务项
            if (!_queueTasks.TryGetValue(task.Id, out var taskItem))
            {
                return;
            }
            
            // 更新任务状态
            taskItem.Status = task.Status;
            taskItem.Progress = task.Progress;
            taskItem.QueuePosition = task.QueuePosition;
            taskItem.ErrorMessage = task.ErrorMessage;
            
            if (task.Status == TaskStatus.Completed)
            {
                taskItem.CompletedTime = DateTime.Now;
                taskItem.Result = task.Result;
                
                // 保存转录结果
                _ = SaveTranscriptionResultAsync(taskItem);
            }
            
            // 更新ListView显示
            UpdateTaskListView(taskItem);
            UpdateStatusDisplay();
        }

        private async Task SaveTranscriptionResultAsync(QueueTaskItem taskItem)
        {
            try
            {
                if (taskItem.Result?.Text != null)
                {
                    File.WriteAllText(taskItem.OutputPath, taskItem.Result.Text, Encoding.UTF8);
                    Log.Information("转录结果已保存: {OutputPath}", taskItem.OutputPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存转录结果失败: {OutputPath}", taskItem.OutputPath);
                taskItem.ErrorMessage = $"保存失败: {ex.Message}";
            }
        }

        private void UpdateTaskListView(QueueTaskItem taskItem)
        {
            var listItem = listViewTasks.Items.Cast<ListViewItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == taskItem.TaskId);
            
            if (listItem != null)
            {
                listItem.SubItems[1].Text = GetStatusText(taskItem.Status);
                listItem.SubItems[2].Text = $"{taskItem.Progress:F1}%";
                listItem.SubItems[3].Text = taskItem.QueuePosition > 0 ? taskItem.QueuePosition.ToString() : "-";
                listItem.SubItems[5].Text = taskItem.CompletedTime?.ToString("HH:mm:ss") ?? "-";
                
                // 设置行颜色
                switch (taskItem.Status)
                {
                    case TaskStatus.Completed:
                        listItem.BackColor = Color.LightGreen;
                        break;
                    case TaskStatus.Failed:
                        listItem.BackColor = Color.LightCoral;
                        break;
                    case TaskStatus.Processing:
                        listItem.BackColor = Color.LightBlue;
                        break;
                    default:
                        listItem.BackColor = Color.White;
                        break;
                }
            }
        }

        private string GetStatusText(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Queued => "等待中",
                TaskStatus.Processing => "处理中",
                TaskStatus.Completed => "已完成",
                TaskStatus.Failed => "失败",
                TaskStatus.Cancelled => "已取消",
                _ => "未知"
            };
        }

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateStatusDisplay));
                return;
            }
            
            var totalTasks = _queueTasks.Count;
            var completedTasks = _queueTasks.Values.Count(t => t.Status == TaskStatus.Completed);
            var failedTasks = _queueTasks.Values.Count(t => t.Status == TaskStatus.Failed);
            var processingTasks = _queueTasks.Values.Count(t => t.Status == TaskStatus.Processing);
            var queuedTasks = _queueTasks.Values.Count(t => t.Status == TaskStatus.Queued);
            
            lblProgress.Text = $"总计: {totalTasks} | 已完成: {completedTasks} | 失败: {failedTasks} | 处理中: {processingTasks} | 等待: {queuedTasks}";
            
            if (processingTasks == 0 && queuedTasks == 0 && totalTasks > 0)
            {
                lblStatus.Text = "全部处理完成";
                lblStatus.ForeColor = Color.Blue;
            }
        }

        private void QueueManagerForm_Load(object sender, EventArgs e)
        {
            // 窗体加载时的初始化
            UpdateStatusDisplay();
        }

        private void QueueManagerForm_FormClosing(object sender, FormClosingEventArgs e)
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
                _httpClient?.Dispose();
                
                if (_queueService != null)
                {
                    _queueService.ProgressUpdated -= OnQueueProgressUpdated;
                }
            }
            
            base.Dispose(disposing);
        }
    }

    // 队列任务项数据模型
    public class QueueTaskItem
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public TaskStatus Status { get; set; } = TaskStatus.Queued;
        public float Progress { get; set; } = 0;
        public int QueuePosition { get; set; } = 0;
        public DateTime AddedTime { get; set; } = DateTime.Now;
        public DateTime? CompletedTime { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public TranscriptionResult? Result { get; set; }
        public string? ErrorMessage { get; set; }
    }
} 