namespace OWhisper.NET
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnProcess;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
        private System.Windows.Forms.TextBox txtOutputPath;
        private System.Windows.Forms.TextBox txtSelectedFile;
        private System.Windows.Forms.Button btnSelectFile;
        private System.Windows.Forms.Button btnSelectFolder;
        private System.Windows.Forms.Label lblFile;
        private System.Windows.Forms.Label lblOutput;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem urlAclMenuItem;
        private System.Windows.Forms.ToolStripMenuItem checkUrlAclMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setupUrlAclMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;

        // 文本润色相关控件
        private System.Windows.Forms.GroupBox grpPolishing = null!;
        private System.Windows.Forms.CheckBox chkEnablePolishing = null!;
        private System.Windows.Forms.Label lblPolishingModel = null!;
        private System.Windows.Forms.ComboBox cmbPolishingModel = null!;
        private System.Windows.Forms.Label lblPolishingTemplate = null!;
        private System.Windows.Forms.ComboBox cmbPolishingTemplate = null!;
        private System.Windows.Forms.Label lblApiKey = null!;
        private System.Windows.Forms.TextBox txtApiKey = null!;
        private System.Windows.Forms.Button btnTestConnection = null!;
        private System.Windows.Forms.Button btnConfigurePolishing = null!;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.lblFile = new System.Windows.Forms.Label();
            this.txtSelectedFile = new System.Windows.Forms.TextBox();
            this.btnSelectFile = new System.Windows.Forms.Button();
            this.lblOutput = new System.Windows.Forms.Label();
            this.txtOutputPath = new System.Windows.Forms.TextBox();
            this.btnSelectFolder = new System.Windows.Forms.Button();
            this.grpPolishing = new System.Windows.Forms.GroupBox();
            this.chkEnablePolishing = new System.Windows.Forms.CheckBox();
            this.lblPolishingModel = new System.Windows.Forms.Label();
            this.cmbPolishingModel = new System.Windows.Forms.ComboBox();
            this.lblPolishingTemplate = new System.Windows.Forms.Label();
            this.cmbPolishingTemplate = new System.Windows.Forms.ComboBox();
            this.lblApiKey = new System.Windows.Forms.Label();
            this.txtApiKey = new System.Windows.Forms.TextBox();
            this.btnTestConnection = new System.Windows.Forms.Button();
            this.btnConfigurePolishing = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnProcess = new System.Windows.Forms.Button();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.urlAclMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.checkUrlAclMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setupUrlAclMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanel1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanel1
            //
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.Controls.Add(this.lblFile, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.txtSelectedFile, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnSelectFile, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.lblOutput, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.txtOutputPath, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnSelectFolder, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.grpPolishing, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.progressBar, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.btnProcess, 0, 4);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 28);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(10);
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(800, 422);
            this.tableLayoutPanel1.TabIndex = 0;
            //
            // lblFile
            //
            this.lblFile.AutoSize = true;
            this.lblFile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFile.Location = new System.Drawing.Point(13, 10);
            this.lblFile.Name = "lblFile";
            this.lblFile.Size = new System.Drawing.Size(114, 40);
            this.lblFile.TabIndex = 0;
            this.lblFile.Text = "音频文件:";
            this.lblFile.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtSelectedFile
            //
            this.txtSelectedFile.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.txtSelectedFile.Location = new System.Drawing.Point(133, 16);
            this.txtSelectedFile.Name = "txtSelectedFile";
            this.txtSelectedFile.ReadOnly = true;
            this.txtSelectedFile.Size = new System.Drawing.Size(544, 27);
            this.txtSelectedFile.TabIndex = 1;
            //
            // btnSelectFile
            //
            this.btnSelectFile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSelectFile.Location = new System.Drawing.Point(683, 13);
            this.btnSelectFile.Name = "btnSelectFile";
            this.btnSelectFile.Size = new System.Drawing.Size(114, 34);
            this.btnSelectFile.TabIndex = 2;
            this.btnSelectFile.Text = "选择文件";
            this.btnSelectFile.UseVisualStyleBackColor = true;
            //
            // lblOutput
            //
            this.lblOutput.AutoSize = true;
            this.lblOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblOutput.Location = new System.Drawing.Point(13, 50);
            this.lblOutput.Name = "lblOutput";
            this.lblOutput.Size = new System.Drawing.Size(114, 40);
            this.lblOutput.TabIndex = 3;
            this.lblOutput.Text = "保存为:";
            this.lblOutput.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtOutputPath
            //
            this.txtOutputPath.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.txtOutputPath.Location = new System.Drawing.Point(133, 56);
            this.txtOutputPath.Name = "txtOutputPath";
            this.txtOutputPath.ReadOnly = true;
            this.txtOutputPath.Size = new System.Drawing.Size(544, 27);
            this.txtOutputPath.TabIndex = 4;
            //
            // btnSelectFolder
            //
            this.btnSelectFolder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSelectFolder.Location = new System.Drawing.Point(683, 53);
            this.btnSelectFolder.Name = "btnSelectFolder";
            this.btnSelectFolder.Size = new System.Drawing.Size(114, 34);
            this.btnSelectFolder.TabIndex = 5;
            this.btnSelectFolder.Text = "另存为";
            this.btnSelectFolder.UseVisualStyleBackColor = true;
            //
            // grpPolishing
            //
            this.tableLayoutPanel1.SetColumnSpan(this.grpPolishing, 3);
            this.grpPolishing.Controls.Add(this.chkEnablePolishing);
            this.grpPolishing.Controls.Add(this.lblPolishingModel);
            this.grpPolishing.Controls.Add(this.cmbPolishingModel);
            this.grpPolishing.Controls.Add(this.lblPolishingTemplate);
            this.grpPolishing.Controls.Add(this.cmbPolishingTemplate);
            this.grpPolishing.Controls.Add(this.lblApiKey);
            this.grpPolishing.Controls.Add(this.txtApiKey);
            this.grpPolishing.Controls.Add(this.btnTestConnection);
            this.grpPolishing.Controls.Add(this.btnConfigurePolishing);
            this.grpPolishing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpPolishing.Location = new System.Drawing.Point(13, 93);
            this.grpPolishing.Name = "grpPolishing";
            this.grpPolishing.Size = new System.Drawing.Size(774, 114);
            this.grpPolishing.TabIndex = 6;
            this.grpPolishing.TabStop = false;
            this.grpPolishing.Text = "文本润色设置";
            //
            // chkEnablePolishing
            //
            this.chkEnablePolishing.AutoSize = true;
            this.chkEnablePolishing.Location = new System.Drawing.Point(15, 25);
            this.chkEnablePolishing.Name = "chkEnablePolishing";
            this.chkEnablePolishing.Size = new System.Drawing.Size(91, 21);
            this.chkEnablePolishing.TabIndex = 0;
            this.chkEnablePolishing.Text = "启用文本润色";
            this.chkEnablePolishing.UseVisualStyleBackColor = true;
            //
            // lblPolishingModel
            //
            this.lblPolishingModel.AutoSize = true;
            this.lblPolishingModel.Location = new System.Drawing.Point(15, 55);
            this.lblPolishingModel.Name = "lblPolishingModel";
            this.lblPolishingModel.Size = new System.Drawing.Size(39, 17);
            this.lblPolishingModel.TabIndex = 1;
            this.lblPolishingModel.Text = "模型:";
            //
            // cmbPolishingModel
            //
            this.cmbPolishingModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPolishingModel.FormattingEnabled = true;
            this.cmbPolishingModel.Location = new System.Drawing.Point(60, 52);
            this.cmbPolishingModel.Name = "cmbPolishingModel";
            this.cmbPolishingModel.Size = new System.Drawing.Size(140, 25);
            this.cmbPolishingModel.TabIndex = 2;
            //
            // lblPolishingTemplate
            //
            this.lblPolishingTemplate.AutoSize = true;
            this.lblPolishingTemplate.Location = new System.Drawing.Point(220, 55);
            this.lblPolishingTemplate.Name = "lblPolishingTemplate";
            this.lblPolishingTemplate.Size = new System.Drawing.Size(39, 17);
            this.lblPolishingTemplate.TabIndex = 3;
            this.lblPolishingTemplate.Text = "模板:";
            //
            // cmbPolishingTemplate
            //
            this.cmbPolishingTemplate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPolishingTemplate.FormattingEnabled = true;
            this.cmbPolishingTemplate.Location = new System.Drawing.Point(265, 52);
            this.cmbPolishingTemplate.Name = "cmbPolishingTemplate";
            this.cmbPolishingTemplate.Size = new System.Drawing.Size(180, 25);
            this.cmbPolishingTemplate.TabIndex = 4;
            //
            // lblApiKey
            //
            this.lblApiKey.AutoSize = true;
            this.lblApiKey.Location = new System.Drawing.Point(15, 85);
            this.lblApiKey.Name = "lblApiKey";
            this.lblApiKey.Size = new System.Drawing.Size(57, 17);
            this.lblApiKey.TabIndex = 5;
            this.lblApiKey.Text = "API Key:";
            //
            // txtApiKey
            //
            this.txtApiKey.Location = new System.Drawing.Point(78, 82);
            this.txtApiKey.Name = "txtApiKey";
            this.txtApiKey.PasswordChar = '*';
            this.txtApiKey.Size = new System.Drawing.Size(367, 23);
            this.txtApiKey.TabIndex = 6;
            //
            // btnTestConnection
            //
            this.btnTestConnection.Location = new System.Drawing.Point(460, 80);
            this.btnTestConnection.Name = "btnTestConnection";
            this.btnTestConnection.Size = new System.Drawing.Size(80, 27);
            this.btnTestConnection.TabIndex = 7;
            this.btnTestConnection.Text = "测试连接";
            this.btnTestConnection.UseVisualStyleBackColor = true;
            //
            // btnConfigurePolishing
            //
            this.btnConfigurePolishing.Location = new System.Drawing.Point(560, 50);
            this.btnConfigurePolishing.Name = "btnConfigurePolishing";
            this.btnConfigurePolishing.Size = new System.Drawing.Size(80, 27);
            this.btnConfigurePolishing.TabIndex = 8;
            this.btnConfigurePolishing.Text = "高级设置";
            this.btnConfigurePolishing.UseVisualStyleBackColor = true;
            //
            // progressBar
            //
            this.tableLayoutPanel1.SetColumnSpan(this.progressBar, 3);
            this.progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressBar.Location = new System.Drawing.Point(13, 213);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(774, 34);
            this.progressBar.TabIndex = 7;
            //
            // btnProcess
            //
            this.tableLayoutPanel1.SetColumnSpan(this.btnProcess, 3);
            this.btnProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnProcess.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.btnProcess.Location = new System.Drawing.Point(13, 253);
            this.btnProcess.Name = "btnProcess";
            this.btnProcess.Size = new System.Drawing.Size(774, 156);
            this.btnProcess.TabIndex = 8;
            this.btnProcess.Text = "开始处理";
            this.btnProcess.UseVisualStyleBackColor = true;
            //
            // openFileDialog
            //
            this.openFileDialog.Filter = "音频文件 (*.mp3;*.wav;*.aac;*.m4a)|*.mp3;*.wav;*.aac;*.m4a|MP3文件 (*.mp3)|*.mp3|WAV文件 (*.wav)|*.wav|AAC文件 (*.aac)|*.aac|M4A文件 (*.m4a)|*.m4a|所有文件 (*.*)|*.*";
            this.openFileDialog.Title = "选择音频文件";
            this.openFileDialog.DefaultExt = "mp3";
            this.openFileDialog.CheckFileExists = true;
            this.openFileDialog.CheckPathExists = true;
            //
            // saveFileDialog
            //
            this.saveFileDialog.Filter = "字幕文件 (*.srt)|*.srt|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
            this.saveFileDialog.Title = "保存转写结果";
            this.saveFileDialog.DefaultExt = "srt";
            this.saveFileDialog.AddExtension = true;
            this.saveFileDialog.CheckPathExists = true;
            //
            // menuStrip1
            //
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(800, 28);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            //
            // toolStripMenuItem1
            //
            this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.urlAclMenuItem,
            this.toolStripSeparator1,
            this.exitMenuItem});
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(53, 24);
            this.toolStripMenuItem1.Text = "工具";
            //
            // urlAclMenuItem
            //
            this.urlAclMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.checkUrlAclMenuItem,
            this.setupUrlAclMenuItem});
            this.urlAclMenuItem.Name = "urlAclMenuItem";
            this.urlAclMenuItem.Size = new System.Drawing.Size(164, 26);
            this.urlAclMenuItem.Text = "URL ACL管理";
            //
            // checkUrlAclMenuItem
            //
            this.checkUrlAclMenuItem.Name = "checkUrlAclMenuItem";
            this.checkUrlAclMenuItem.Size = new System.Drawing.Size(182, 26);
            this.checkUrlAclMenuItem.Text = "检查ACL权限";
            //
            // setupUrlAclMenuItem
            //
            this.setupUrlAclMenuItem.Name = "setupUrlAclMenuItem";
            this.setupUrlAclMenuItem.Size = new System.Drawing.Size(182, 26);
            this.setupUrlAclMenuItem.Text = "设置ACL权限";
            //
            // toolStripSeparator1
            //
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(161, 6);
            //
            // exitMenuItem
            //
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(164, 26);
            this.exitMenuItem.Text = "退出";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.MinimumSize = new System.Drawing.Size(600, 300);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OWhisper.NET - 音频转写工具";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
