using System;
using System.Windows.Forms;
using System.Drawing;
using Velopack; // 添加Velopack引用
using OWhisper.Core.Services; // 添加Core服务引用

namespace OWhisper.NET {
    public class TrayApp : ApplicationContext {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private IWebApiService webApiService;
        private UpdateManager updateManager; // 添加更新管理器字段
        private MainForm debugForm;

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
                Icon = new Icon(SystemIcons.Application, 40, 40),
                Visible = true
            };

            // 初始化右键菜单
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("启动服务", null, OnStartService);
            trayMenu.Items.Add("停止服务", null, OnStopService);
            trayMenu.Items.Add("-"); // 分隔线
            
            // 添加更新管理菜单项
            trayMenu.Items.Add("检查更新", null, OnCheckUpdates);
            trayMenu.Items.Add("下载更新", null, OnDownloadUpdates);
            trayMenu.Items.Add("重启应用", null, OnRestartApply);
            trayMenu.Items.Add("-"); // 分隔线
            
            trayMenu.Items.Add("调试窗口", null, OnShowDebug);
            trayMenu.Items.Add("-"); // 分隔线
            trayMenu.Items.Add("退出", null, OnExit);

            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += (s, e) => OnShowDebug(s, e);
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
            debugForm.ShowForDebug();
        }

        private void OnExit(object sender, EventArgs e) {
            trayIcon.Visible = false;
            Program.ExitApplication();
        }
        
        // 更新管理功能
        private void OnCheckUpdates(object sender, EventArgs e) {
            _ = Program.CheckForUpdatesAsync();
        }
        
        private async void OnDownloadUpdates(object sender, EventArgs e) {
            if (Program._updateInfo != null) {
                await Program.DownloadUpdatesAsync();
            } else {
                MessageBox.Show("请先检查更新", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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