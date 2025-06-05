using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Reflection;
using Velopack; // 添加Velopack引用
using OWhisper.Core.Services; // 添加Core服务引用

namespace OWhisper.NET {
    public class TrayApp : ApplicationContext {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private IWebApiService webApiService;
        private UpdateManager updateManager; // 添加更新管理器字段
        private MainForm debugForm;
        private QueueManagerForm queueManagerForm; // 添加队列管理窗口
        private ModelManagerForm modelManagerForm; // 添加模型管理窗口

        public TrayApp() : this(null, null) {
        }

        public TrayApp(IWebApiService service) : this(service, null) {
        }

        // 添加支持UpdateManager的新构造函数
        public TrayApp(IWebApiService service, UpdateManager updateManager) {
            webApiService = service;
            this.updateManager = updateManager;
            debugForm = new MainForm();

            // 初始化托盘图标
            trayIcon = new NotifyIcon {
                Text = "OWhisper.NET",
                Icon = LoadAppIcon(),
                Visible = true
            };

            // 初始化右键菜单
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("启动服务", null, OnStartService);
            trayMenu.Items.Add("停止服务", null, OnStopService);
            trayMenu.Items.Add("-"); // 分隔线
            
            // 添加队列识别功能
            trayMenu.Items.Add("队列识别", null, OnShowQueueManager);
            trayMenu.Items.Add("-"); // 分隔线
            
            // 添加模型管理功能
            trayMenu.Items.Add("模型管理", null, OnShowModelManager);
            trayMenu.Items.Add("-"); // 分隔线
            
            // 添加API配置功能
            trayMenu.Items.Add("AI配置", null, OnShowApiConfig);
            trayMenu.Items.Add("-"); // 分隔线
            
            // 修改更新管理菜单项 - 只保留检查更新
            trayMenu.Items.Add("检查更新", null, OnCheckUpdates);
            trayMenu.Items.Add("重启应用", null, OnRestartApply);
            trayMenu.Items.Add("-"); // 分隔线
            
            trayMenu.Items.Add("调试窗口", null, OnShowDebug);
            trayMenu.Items.Add("-"); // 分隔线
            trayMenu.Items.Add("退出", null, OnExit);

            trayIcon.ContextMenuStrip = trayMenu;

            // 双击打开调试窗口，单击打开菜单
            trayIcon.DoubleClick += (s, e) => OnShowDebug(s, e);
            trayIcon.MouseClick += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    // 显示右键菜单
                    trayMenu.Show(Control.MousePosition);
                }
            };
        }

        // 添加加载应用图标的方法
        private Icon LoadAppIcon() {
            try {
                // 首先尝试从文件系统加载高分辨率托盘图标
                string trayIconPath = Path.Combine(Application.StartupPath, "Resources", "app_tray_icon.ico");
                if (File.Exists(trayIconPath)) {
                    return new Icon(trayIconPath);
                }

                // 如果托盘图标不存在，尝试普通图标
                string iconPath = Path.Combine(Application.StartupPath, "Resources", "app_icon.ico");
                if (File.Exists(iconPath)) {
                    return new Icon(iconPath);
                }

                // 如果文件不存在，尝试从嵌入的资源加载
                var assembly = Assembly.GetExecutingAssembly();
                
                // 先尝试托盘图标资源
                using (var stream = assembly.GetManifestResourceStream("OWhisper.NET.Resources.app_tray_icon.ico")) {
                    if (stream != null) {
                        return new Icon(stream);
                    }
                }
                
                // 再尝试普通图标资源
                using (var stream = assembly.GetManifestResourceStream("OWhisper.NET.Resources.app_icon.ico")) {
                    if (stream != null) {
                        return new Icon(stream);
                    }
                }

                // 如果都失败了，尝试加载超高分辨率PNG图标并转换
                string[] pngPaths = {
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_super_256x256.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_256x256.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_128x128.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_64x64.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon_32x32.png")
                };

                foreach (string pngPath in pngPaths) {
                    if (File.Exists(pngPath)) {
                        using (var bitmap = new Bitmap(pngPath)) {
                            return Icon.FromHandle(bitmap.GetHicon());
                        }
                    }
                }
            }
            catch (Exception ex) {
                // 记录错误但不中断应用
                Console.WriteLine($"加载自定义图标失败: {ex.Message}");
            }

            // 回退到系统默认图标
            return new Icon(SystemIcons.Application, 32, 32);
        }

        private async void OnStartService(object sender, EventArgs e) {
            if (webApiService == null || webApiService.IsRunning) {
                trayIcon.ShowBalloonTip(1000, "服务状态", "服务已在运行中", ToolTipIcon.Warning);
                return;
            }
            
            try {
                await webApiService.StartAsync();
                trayIcon.ShowBalloonTip(1000, "服务状态", "服务已启动", ToolTipIcon.Info);
            } catch (Exception ex) {
                MessageBox.Show($"启动服务失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnStopService(object sender, EventArgs e) {
            if (webApiService == null || !webApiService.IsRunning) {
                trayIcon.ShowBalloonTip(1000, "服务状态", "服务未运行", ToolTipIcon.Warning);
                return;
            }
            
            try {
                await webApiService.StopAsync();
                trayIcon.ShowBalloonTip(1000, "服务状态", "服务已停止", ToolTipIcon.Info);
            } catch (Exception ex) {
                MessageBox.Show($"停止服务失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnShowDebug(object sender, EventArgs e) {
            if (debugForm == null || debugForm.IsDisposed) {
                debugForm = new MainForm();
            }
            debugForm.ShowForDebug();
        }

        // 添加显示队列管理窗口的方法
        private void OnShowQueueManager(object sender, EventArgs e) {
            if (queueManagerForm == null || queueManagerForm.IsDisposed) {
                queueManagerForm = new QueueManagerForm();
            }
            queueManagerForm.Show();
            queueManagerForm.Activate();
        }

        // 添加显示模型管理窗口的方法
        private void OnShowModelManager(object sender, EventArgs e) {
            if (modelManagerForm == null || modelManagerForm.IsDisposed) {
                modelManagerForm = new ModelManagerForm();
            }
            modelManagerForm.Show();
            modelManagerForm.Activate();
        }

        // 添加显示API配置窗口的方法
        private void OnShowApiConfig(object sender, EventArgs e) {
            try
            {
                using (var configForm = new ApiConfigForm())
                {
                    configForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开API配置失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExit(object sender, EventArgs e) {
            trayIcon.Visible = false;
            Program.ExitApplication();
        }
        
        // 更新管理功能
        private void OnCheckUpdates(object sender, EventArgs e) {
            _ = Program.CheckForUpdatesAsync();
        }
        
        private void OnRestartApply(object sender, EventArgs e) {
            if (Program._updateManager != null && Program._updateManager.UpdatePendingRestart != null) {
                Program._updateManager.ApplyUpdatesAndRestart(Program._updateInfo);
            } else {
                MessageBox.Show("没有待应用的更新", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool _disposed = false;

        protected override void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    Console.WriteLine("释放TrayApp资源...");

                    // 释放托管资源
                    trayIcon?.Dispose();
                    trayMenu?.Dispose();

                    // 清理服务实例
                    if (webApiService != null) {
                        webApiService.Dispose();
                        webApiService = null;
                    }

                    // 释放调试窗口
                    debugForm?.Dispose();
                    debugForm = null;

                    // 释放队列管理窗口
                    queueManagerForm?.Dispose();
                    queueManagerForm = null;

                    // 释放模型管理窗口
                    modelManagerForm?.Dispose();
                    modelManagerForm = null;

                    Console.WriteLine("TrayApp资源释放完成");
                }

                _disposed = true;
            }
            base.Dispose(disposing);
        }

        ~TrayApp() {
            Dispose(false);
        }
    }
}