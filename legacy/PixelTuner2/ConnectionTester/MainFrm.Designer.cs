namespace ConnectionTester
{
    partial class MainFrm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.boxPortNum = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.boxConnect = new System.Windows.Forms.LinkLabel();
            this.boxDisconnect = new System.Windows.Forms.LinkLabel();
            this.boxPathFW = new System.Windows.Forms.TextBox();
            this.boxChoosePath = new System.Windows.Forms.LinkLabel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.boxStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.boxLog = new System.Windows.Forms.RichTextBox();
            this.boxLoadFW = new System.Windows.Forms.LinkLabel();
            this.boxLoadProj = new System.Windows.Forms.LinkLabel();
            this.boxChooseProj = new System.Windows.Forms.LinkLabel();
            this.boxPathProject = new System.Windows.Forms.TextBox();
            this.boxStateQuery = new System.Windows.Forms.LinkLabel();
            this.boxClear = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.boxPortNum)).BeginInit();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // boxPortNum
            // 
            this.boxPortNum.Location = new System.Drawing.Point(43, 12);
            this.boxPortNum.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.boxPortNum.Name = "boxPortNum";
            this.boxPortNum.Size = new System.Drawing.Size(49, 20);
            this.boxPortNum.TabIndex = 0;
            this.boxPortNum.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(31, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "COM";
            // 
            // boxConnect
            // 
            this.boxConnect.AutoSize = true;
            this.boxConnect.Location = new System.Drawing.Point(98, 14);
            this.boxConnect.Name = "boxConnect";
            this.boxConnect.Size = new System.Drawing.Size(69, 13);
            this.boxConnect.TabIndex = 2;
            this.boxConnect.TabStop = true;
            this.boxConnect.Text = "Подключить";
            this.boxConnect.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxConnect_LinkClicked);
            // 
            // boxDisconnect
            // 
            this.boxDisconnect.AutoSize = true;
            this.boxDisconnect.Location = new System.Drawing.Point(173, 14);
            this.boxDisconnect.Name = "boxDisconnect";
            this.boxDisconnect.Size = new System.Drawing.Size(62, 13);
            this.boxDisconnect.TabIndex = 3;
            this.boxDisconnect.TabStop = true;
            this.boxDisconnect.Text = "Отключить";
            this.boxDisconnect.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxDisconnect_LinkClicked);
            // 
            // boxPathFW
            // 
            this.boxPathFW.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.boxPathFW.Location = new System.Drawing.Point(124, 64);
            this.boxPathFW.Name = "boxPathFW";
            this.boxPathFW.Size = new System.Drawing.Size(414, 20);
            this.boxPathFW.TabIndex = 4;
            // 
            // boxChoosePath
            // 
            this.boxChoosePath.AutoSize = true;
            this.boxChoosePath.Location = new System.Drawing.Point(12, 67);
            this.boxChoosePath.Name = "boxChoosePath";
            this.boxChoosePath.Size = new System.Drawing.Size(106, 13);
            this.boxChoosePath.TabIndex = 5;
            this.boxChoosePath.TabStop = true;
            this.boxChoosePath.Text = "Выбрать прошивку:";
            this.boxChoosePath.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxChoosePath_LinkClicked);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.boxStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 304);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(622, 22);
            this.statusStrip1.TabIndex = 6;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // boxStatus
            // 
            this.boxStatus.Name = "boxStatus";
            this.boxStatus.Size = new System.Drawing.Size(0, 17);
            // 
            // boxLog
            // 
            this.boxLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.boxLog.Location = new System.Drawing.Point(0, 90);
            this.boxLog.Name = "boxLog";
            this.boxLog.ReadOnly = true;
            this.boxLog.Size = new System.Drawing.Size(622, 214);
            this.boxLog.TabIndex = 7;
            this.boxLog.Text = "";
            // 
            // boxLoadFW
            // 
            this.boxLoadFW.AutoSize = true;
            this.boxLoadFW.Location = new System.Drawing.Point(551, 67);
            this.boxLoadFW.Name = "boxLoadFW";
            this.boxLoadFW.Size = new System.Drawing.Size(59, 13);
            this.boxLoadFW.TabIndex = 8;
            this.boxLoadFW.TabStop = true;
            this.boxLoadFW.Text = "Загрузить";
            this.boxLoadFW.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxLoadFW_LinkClicked);
            // 
            // boxLoadProj
            // 
            this.boxLoadProj.AutoSize = true;
            this.boxLoadProj.Location = new System.Drawing.Point(551, 41);
            this.boxLoadProj.Name = "boxLoadProj";
            this.boxLoadProj.Size = new System.Drawing.Size(59, 13);
            this.boxLoadProj.TabIndex = 11;
            this.boxLoadProj.TabStop = true;
            this.boxLoadProj.Text = "Загрузить";
            this.boxLoadProj.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxLoadProj_LinkClicked);
            // 
            // boxChooseProj
            // 
            this.boxChooseProj.AutoSize = true;
            this.boxChooseProj.Location = new System.Drawing.Point(12, 41);
            this.boxChooseProj.Name = "boxChooseProj";
            this.boxChooseProj.Size = new System.Drawing.Size(92, 13);
            this.boxChooseProj.TabIndex = 10;
            this.boxChooseProj.TabStop = true;
            this.boxChooseProj.Text = "Выбрать проект:";
            this.boxChooseProj.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxChooseProj_LinkClicked);
            // 
            // boxPathProject
            // 
            this.boxPathProject.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.boxPathProject.Location = new System.Drawing.Point(124, 38);
            this.boxPathProject.Name = "boxPathProject";
            this.boxPathProject.Size = new System.Drawing.Size(414, 20);
            this.boxPathProject.TabIndex = 9;
            // 
            // boxStateQuery
            // 
            this.boxStateQuery.AutoSize = true;
            this.boxStateQuery.Location = new System.Drawing.Point(276, 14);
            this.boxStateQuery.Name = "boxStateQuery";
            this.boxStateQuery.Size = new System.Drawing.Size(100, 13);
            this.boxStateQuery.TabIndex = 12;
            this.boxStateQuery.TabStop = true;
            this.boxStateQuery.Text = "Запрос состояния";
            this.boxStateQuery.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxStateQuery_LinkClicked);
            // 
            // boxClear
            // 
            this.boxClear.AutoSize = true;
            this.boxClear.Location = new System.Drawing.Point(541, 96);
            this.boxClear.Name = "boxClear";
            this.boxClear.Size = new System.Drawing.Size(54, 13);
            this.boxClear.TabIndex = 13;
            this.boxClear.TabStop = true;
            this.boxClear.Text = "Очистить";
            this.boxClear.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxClear_LinkClicked);
            // 
            // MainFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(622, 326);
            this.Controls.Add(this.boxClear);
            this.Controls.Add(this.boxStateQuery);
            this.Controls.Add(this.boxLoadProj);
            this.Controls.Add(this.boxChooseProj);
            this.Controls.Add(this.boxPathProject);
            this.Controls.Add(this.boxLoadFW);
            this.Controls.Add(this.boxLog);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.boxChoosePath);
            this.Controls.Add(this.boxPathFW);
            this.Controls.Add(this.boxDisconnect);
            this.Controls.Add(this.boxConnect);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.boxPortNum);
            this.Name = "MainFrm";
            this.Text = "Связь";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFrm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.boxPortNum)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown boxPortNum;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.LinkLabel boxConnect;
        private System.Windows.Forms.LinkLabel boxDisconnect;
        private System.Windows.Forms.TextBox boxPathFW;
        private System.Windows.Forms.LinkLabel boxChoosePath;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel boxStatus;
        private System.Windows.Forms.RichTextBox boxLog;
        private System.Windows.Forms.LinkLabel boxLoadFW;
        private System.Windows.Forms.LinkLabel boxLoadProj;
        private System.Windows.Forms.LinkLabel boxChooseProj;
        private System.Windows.Forms.TextBox boxPathProject;
        private System.Windows.Forms.LinkLabel boxStateQuery;
        private System.Windows.Forms.LinkLabel boxClear;
    }
}

