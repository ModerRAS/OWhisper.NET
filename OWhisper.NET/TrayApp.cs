using System;
using System.Windows.Forms;
using System.Drawing;

namespace OWhisper.NET {
    public class TrayApp : ApplicationContext {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private WhisperService whisperService;
        private Form1 debugForm;

        public TrayApp() : this(null) {
        }

        public TrayApp(WhisperService service) {
            whisperService = service;
            debugForm = new Form1();

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
            trayMenu.Items.Add("调试窗口", null, OnShowDebug);
            trayMenu.Items.Add("-"); // 分隔线
            trayMenu.Items.Add("退出", null, OnExit);

            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += (s, e) => OnShowDebug(s, e);

            // 初始化服务控制器
            whisperService = WhisperService.Instance;
        }

        private void OnStartService(object sender, EventArgs e) {
            whisperService.Start();
            trayIcon.ShowBalloonTip(1000, "服务状态", "服务已启动", ToolTipIcon.Info);
        }

        private void OnStopService(object sender, EventArgs e) {
            whisperService.Stop();
            trayIcon.ShowBalloonTip(1000, "服务状态", "服务已停止", ToolTipIcon.Info);
        }

        private void OnShowDebug(object sender, EventArgs e) {
            debugForm.ShowForDebug();
        }

        private void OnExit(object sender, EventArgs e) {
            trayIcon.Visible = false;
            Program.ExitApplication();
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
                    if (whisperService != null) {
                        whisperService.Dispose();
                        whisperService = null;
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