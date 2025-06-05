using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OWhisper.NET.Services;

namespace OWhisper.NET
{
    public partial class ApiConfigForm : Form
    {
        private ComboBox cmbProvider;
        private TextBox txtApiKey;
        private TextBox txtBaseUrl;
        private Button btnSave;
        private Button btnTest;
        private Button btnDelete;
        private Button btnCancel;
        private Label lblStatus;

        // 支持的AI提供商
        private readonly string[] SupportedProviders = { "DeepSeek", "OpenAI", "Azure OpenAI", "Claude", "Gemini" };

        public ApiConfigForm()
        {
            InitializeComponent();
            LoadExistingData();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 窗体设置
            this.Text = "AI 模型配置";
            this.Size = new Size(500, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 10F);

            // 提供商选择
            var lblProvider = new Label
            {
                Text = "AI 提供商:",
                Location = new Point(20, 25),
                Size = new Size(80, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            cmbProvider = new ComboBox
            {
                Location = new Point(110, 22),
                Size = new Size(200, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            cmbProvider.Items.AddRange(SupportedProviders);
            cmbProvider.SelectedIndexChanged += CmbProvider_SelectedIndexChanged;

            // API Key输入
            var lblApiKey = new Label
            {
                Text = "API Key:",
                Location = new Point(20, 70),
                Size = new Size(80, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtApiKey = new TextBox
            {
                Location = new Point(110, 67),
                Size = new Size(340, 30),
                PasswordChar = '*',
                Font = new Font("Microsoft YaHei UI", 10F)
            };

            // Base URL输入（可选）
            var lblBaseUrl = new Label
            {
                Text = "Base URL:",
                Location = new Point(20, 115),
                Size = new Size(80, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtBaseUrl = new TextBox
            {
                Location = new Point(110, 112),
                Size = new Size(340, 30),
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = Color.Gray,
                Text = "可选，留空使用默认地址"
            };
            
            // 添加焦点事件处理以模拟PlaceholderText效果
            txtBaseUrl.Enter += (s, e) => 
            {
                if (txtBaseUrl.ForeColor == Color.Gray)
                {
                    txtBaseUrl.Text = "";
                    txtBaseUrl.ForeColor = Color.Black;
                }
            };
            
            txtBaseUrl.Leave += (s, e) => 
            {
                if (string.IsNullOrWhiteSpace(txtBaseUrl.Text))
                {
                    SetPlaceholderText(txtBaseUrl, "可选，留空使用默认地址");
                }
            };

            // 按钮区域
            btnSave = new Button
            {
                Text = "保存",
                Location = new Point(110, 170),
                Size = new Size(80, 35),
                Font = new Font("Microsoft YaHei UI", 10F),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.Click += BtnSave_Click;

            btnTest = new Button
            {
                Text = "测试连接",
                Location = new Point(200, 170),
                Size = new Size(80, 35),
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            btnTest.Click += BtnTest_Click;

            btnDelete = new Button
            {
                Text = "删除",
                Location = new Point(290, 170),
                Size = new Size(80, 35),
                Font = new Font("Microsoft YaHei UI", 10F),
                BackColor = Color.FromArgb(196, 43, 28),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDelete.Click += BtnDelete_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(380, 170),
                Size = new Size(70, 35),
                Font = new Font("Microsoft YaHei UI", 10F),
                DialogResult = DialogResult.Cancel
            };

            // 状态标签
            lblStatus = new Label
            {
                Location = new Point(20, 220),
                Size = new Size(430, 60),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = Color.Gray,
                Text = "选择AI提供商并输入API Key。数据将安全存储在Windows凭据管理器中。"
            };

            // 添加控件到窗体
            this.Controls.AddRange(new Control[] {
                lblProvider, cmbProvider,
                lblApiKey, txtApiKey,
                lblBaseUrl, txtBaseUrl,
                btnSave, btnTest, btnDelete, btnCancel,
                lblStatus
            });

            this.ResumeLayout(false);
        }

        private void LoadExistingData()
        {
            // 默认选择DeepSeek
            if (cmbProvider.Items.Count > 0)
            {
                cmbProvider.SelectedIndex = 0;
            }
        }

        private void CmbProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbProvider.SelectedItem != null)
            {
                string selectedProvider = cmbProvider.SelectedItem.ToString();
                
                // 尝试加载已保存的API Key
                string existingApiKey = CredentialService.GetApiKey(selectedProvider);
                if (!string.IsNullOrEmpty(existingApiKey))
                {
                    txtApiKey.Text = existingApiKey;
                    lblStatus.Text = $"已找到 {selectedProvider} 的已保存配置";
                    lblStatus.ForeColor = Color.Green;
                }
                else
                {
                    txtApiKey.Clear();
                    lblStatus.Text = $"未找到 {selectedProvider} 的配置，请输入API Key";
                    lblStatus.ForeColor = Color.Gray;
                }

                // 根据提供商设置默认Base URL
                SetDefaultBaseUrl(selectedProvider);
            }
        }

        private void SetPlaceholderText(TextBox textBox, string placeholderText)
        {
            textBox.Text = placeholderText;
            textBox.ForeColor = Color.Gray;
        }
        
        private void SetDefaultBaseUrl(string provider)
        {
            string placeholder;
            switch (provider)
            {
                case "DeepSeek":
                    placeholder = "https://api.deepseek.com/v1";
                    break;
                case "OpenAI":
                    placeholder = "https://api.openai.com/v1";
                    break;
                case "Azure OpenAI":
                    placeholder = "https://your-resource.openai.azure.com";
                    break;
                case "Claude":
                    placeholder = "https://api.anthropic.com/v1";
                    break;
                case "Gemini":
                    placeholder = "https://generativelanguage.googleapis.com/v1";
                    break;
                default:
                    placeholder = "可选，留空使用默认地址";
                    break;
            }
            
            // 如果当前是占位符文本或为空，则设置新的占位符
            if (txtBaseUrl.ForeColor == Color.Gray || string.IsNullOrWhiteSpace(txtBaseUrl.Text))
            {
                SetPlaceholderText(txtBaseUrl, placeholder);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (cmbProvider.SelectedItem == null)
            {
                MessageBox.Show("请选择AI提供商", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtApiKey.Text))
            {
                MessageBox.Show("请输入API Key", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string provider = cmbProvider.SelectedItem.ToString();
            string apiKey = txtApiKey.Text.Trim();

            try
            {
                bool success = CredentialService.SaveApiKey(provider, apiKey);
                if (success)
                {
                    // 如果有Base URL也保存（排除占位符文本）
                    if (!string.IsNullOrWhiteSpace(txtBaseUrl.Text) && txtBaseUrl.ForeColor != Color.Gray)
                    {
                        CredentialService.SaveApiKey($"{provider}_BaseUrl", txtBaseUrl.Text.Trim());
                    }

                    lblStatus.Text = $"{provider} API Key 保存成功！";
                    lblStatus.ForeColor = Color.Green;
                    
                    MessageBox.Show($"{provider} 配置保存成功！", "成功", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = "保存失败，请检查权限设置";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show("保存失败，请确保有足够的权限访问Windows凭据管理器", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"保存出错: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"保存出错: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtApiKey.Text))
            {
                MessageBox.Show("请先输入API Key", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lblStatus.Text = "正在测试连接...";
            lblStatus.ForeColor = Color.Blue;
            btnTest.Enabled = false;

            // TODO: 实现实际的API连接测试
            // 这里可以根据不同的提供商实现相应的API测试逻辑
            
            // 模拟测试过程
            Timer timer = new Timer { Interval = 2000 };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                timer.Dispose();
                
                // 简单的格式验证
                if (txtApiKey.Text.Length < 10)
                {
                    lblStatus.Text = "API Key 格式可能不正确";
                    lblStatus.ForeColor = Color.Orange;
                }
                else
                {
                    lblStatus.Text = "API Key 格式验证通过（建议保存后在主界面进行实际测试）";
                    lblStatus.ForeColor = Color.Green;
                }
                
                btnTest.Enabled = true;
            };
            timer.Start();
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (cmbProvider.SelectedItem == null)
            {
                MessageBox.Show("请选择要删除的AI提供商", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string provider = cmbProvider.SelectedItem.ToString();
            
            var result = MessageBox.Show(
                $"确定要删除 {provider} 的配置吗？\n\n这将从Windows凭据管理器中删除相关的API Key。", 
                "确认删除", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    bool success = CredentialService.DeleteApiKey(provider);
                    // 同时删除BaseUrl（如果有）
                    CredentialService.DeleteApiKey($"{provider}_BaseUrl");

                    if (success)
                    {
                        txtApiKey.Clear();
                        txtBaseUrl.Clear();
                        lblStatus.Text = $"{provider} 配置删除成功";
                        lblStatus.ForeColor = Color.Green;
                    }
                    else
                    {
                        lblStatus.Text = "删除失败";
                        lblStatus.ForeColor = Color.Red;
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = $"删除出错: {ex.Message}";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show($"删除出错: {ex.Message}", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
} 