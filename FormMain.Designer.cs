namespace TCS.ServerV2
{
    partial class FormMain
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다.
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마십시오.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.tssFMQConnection = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel4 = new System.Windows.Forms.ToolStripStatusLabel();
            this.tssActive = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.tssWaitTr = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new System.Windows.Forms.ToolStripStatusLabel();
            this.tssRunTr = new System.Windows.Forms.ToolStripStatusLabel();
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.mnuShow = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.tssLog = new System.Windows.Forms.ToolStripButton();
            this.tssClear = new System.Windows.Forms.ToolStripButton();
            this.toolStripBtnTest = new System.Windows.Forms.ToolStripButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtLog = new System.Windows.Forms.RichTextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dg1 = new System.Windows.Forms.DataGridView();
            this.statusStrip1.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg1)).BeginInit();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.tssFMQConnection,
            this.toolStripStatusLabel4,
            this.tssActive,
            this.toolStripStatusLabel2,
            this.tssWaitTr,
            this.toolStripStatusLabel3,
            this.tssRunTr});
            this.statusStrip1.Location = new System.Drawing.Point(0, 382);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 12, 0);
            this.statusStrip1.Size = new System.Drawing.Size(519, 25);
            this.statusStrip1.TabIndex = 0;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(36, 20);
            this.toolStripStatusLabel1.Text = "FMQ:";
            // 
            // tssFMQConnection
            // 
            this.tssFMQConnection.AutoSize = false;
            this.tssFMQConnection.Name = "tssFMQConnection";
            this.tssFMQConnection.Size = new System.Drawing.Size(20, 20);
            this.tssFMQConnection.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // toolStripStatusLabel4
            // 
            this.toolStripStatusLabel4.Name = "toolStripStatusLabel4";
            this.toolStripStatusLabel4.Size = new System.Drawing.Size(51, 20);
            this.toolStripStatusLabel4.Text = "STATE : ";
            // 
            // tssActive
            // 
            this.tssActive.AutoSize = false;
            this.tssActive.Name = "tssActive";
            this.tssActive.Size = new System.Drawing.Size(80, 20);
            this.tssActive.Text = "DEACTIVE";
            this.tssActive.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // toolStripStatusLabel2
            // 
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new System.Drawing.Size(56, 20);
            this.toolStripStatusLabel2.Text = "WAIT-TR:";
            // 
            // tssWaitTr
            // 
            this.tssWaitTr.Name = "tssWaitTr";
            this.tssWaitTr.Size = new System.Drawing.Size(21, 20);
            this.tssWaitTr.Text = "00";
            // 
            // toolStripStatusLabel3
            // 
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new System.Drawing.Size(52, 20);
            this.toolStripStatusLabel3.Text = "RUN-TR:";
            // 
            // tssRunTr
            // 
            this.tssRunTr.Name = "tssRunTr";
            this.tssRunTr.Size = new System.Drawing.Size(21, 20);
            this.tssRunTr.Text = "00";
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList.Images.SetKeyName(0, "Stop.png");
            this.imageList.Images.SetKeyName(1, "Start.png");
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "TCS.SERVER";
            this.notifyIcon.Visible = true;
            this.notifyIcon.DoubleClick += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuShow,
            this.mnuExit});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(105, 48);
            // 
            // mnuShow
            // 
            this.mnuShow.Name = "mnuShow";
            this.mnuShow.Size = new System.Drawing.Size(104, 22);
            this.mnuShow.Text = "Show";
            this.mnuShow.Click += new System.EventHandler(this.mnuShow_Click);
            // 
            // mnuExit
            // 
            this.mnuExit.Name = "mnuExit";
            this.mnuExit.Size = new System.Drawing.Size(104, 22);
            this.mnuExit.Text = "Exit";
            this.mnuExit.Click += new System.EventHandler(this.mnuExit_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tssLog,
            this.tssClear,
            this.toolStripBtnTest});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(519, 27);
            this.toolStrip1.Stretch = true;
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // tssLog
            // 
            this.tssLog.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tssLog.Image = global::TCS.ServerV2.Properties.Resources.folder;
            this.tssLog.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tssLog.Name = "tssLog";
            this.tssLog.Size = new System.Drawing.Size(24, 24);
            this.tssLog.Text = "LOG";
            this.tssLog.ToolTipText = "LOG OPEN";
            this.tssLog.Click += new System.EventHandler(this.tssLog_Click);
            // 
            // tssClear
            // 
            this.tssClear.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tssClear.Image = global::TCS.ServerV2.Properties.Resources.clear;
            this.tssClear.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tssClear.Name = "tssClear";
            this.tssClear.Size = new System.Drawing.Size(24, 24);
            this.tssClear.Text = "LOG";
            this.tssClear.ToolTipText = "LOG Clear";
            this.tssClear.Click += new System.EventHandler(this.tssClear_Click);
            // 
            // toolStripBtnTest
            // 
            this.toolStripBtnTest.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripBtnTest.Image = global::TCS.ServerV2.Properties.Resources.test;
            this.toolStripBtnTest.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripBtnTest.Name = "toolStripBtnTest";
            this.toolStripBtnTest.Size = new System.Drawing.Size(24, 24);
            this.toolStripBtnTest.Text = "TEST FORM";
            this.toolStripBtnTest.Click += new System.EventHandler(this.toolStripBtnTest_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(0, 27);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(519, 63);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "SECS";
            // 
            // txtLog
            // 
            this.txtLog.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.txtLog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Font = new System.Drawing.Font("돋움체", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.txtLog.ForeColor = System.Drawing.Color.Yellow;
            this.txtLog.Location = new System.Drawing.Point(0, 0);
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.Size = new System.Drawing.Size(519, 169);
            this.txtLog.TabIndex = 5;
            this.txtLog.Text = "";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 90);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.txtLog);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dg1);
            this.splitContainer1.Size = new System.Drawing.Size(519, 292);
            this.splitContainer1.SplitterDistance = 169;
            this.splitContainer1.TabIndex = 6;
            // 
            // dg1
            // 
            this.dg1.AllowUserToAddRows = false;
            this.dg1.AllowUserToDeleteRows = false;
            this.dg1.AllowUserToResizeRows = false;
            this.dg1.BackgroundColor = System.Drawing.Color.White;
            this.dg1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg1.Location = new System.Drawing.Point(0, 0);
            this.dg1.Name = "dg1";
            this.dg1.RowHeadersVisible = false;
            this.dg1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dg1.Size = new System.Drawing.Size(519, 119);
            this.dg1.TabIndex = 9;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(519, 407);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.statusStrip1);
            this.Font = new System.Drawing.Font("돋움체", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TCS Server";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Resize += new System.EventHandler(this.FormMain_Resize);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.contextMenuStrip.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dg1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel tssFMQConnection;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel2;
        private System.Windows.Forms.ToolStripStatusLabel tssWaitTr;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel3;
        private System.Windows.Forms.ToolStripStatusLabel tssRunTr;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem mnuShow;
        private System.Windows.Forms.ToolStripMenuItem mnuExit;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tssClear;
        private System.Windows.Forms.ToolStripButton tssLog;
        private System.Windows.Forms.ToolStripButton toolStripBtnTest;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel4;
        private System.Windows.Forms.ToolStripStatusLabel tssActive;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dg1;
    }
}

