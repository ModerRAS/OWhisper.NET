using System.Windows.Forms;

namespace OWhisper.NET
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            // 默认窗口状态为最小化
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
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
    }
}
