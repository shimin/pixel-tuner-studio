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
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.boxStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.boxTab = new System.Windows.Forms.TabControl();
            this.boxTabPage1 = new System.Windows.Forms.TabPage();
            this.boxLogToFile = new System.Windows.Forms.CheckBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.boxDelay = new System.Windows.Forms.NumericUpDown();
            this.boxRepeatCount = new System.Windows.Forms.NumericUpDown();
            this.label8 = new System.Windows.Forms.Label();
            this.boxSendRequest = new System.Windows.Forms.LinkLabel();
            this.boxDeleteRequest = new System.Windows.Forms.LinkLabel();
            this.boxRequest = new System.Windows.Forms.ComboBox();
            this.boxClear = new System.Windows.Forms.LinkLabel();
            this.boxStateQuery = new System.Windows.Forms.LinkLabel();
            this.boxLoadProj = new System.Windows.Forms.LinkLabel();
            this.boxChooseProj = new System.Windows.Forms.LinkLabel();
            this.boxPathProject = new System.Windows.Forms.TextBox();
            this.boxLoadFW = new System.Windows.Forms.LinkLabel();
            this.boxLog = new System.Windows.Forms.RichTextBox();
            this.boxChoosePath = new System.Windows.Forms.LinkLabel();
            this.boxPathFW = new System.Windows.Forms.TextBox();
            this.boxDisconnect = new System.Windows.Forms.LinkLabel();
            this.boxConnect = new System.Windows.Forms.LinkLabel();
            this.label1 = new System.Windows.Forms.Label();
            this.boxPortNum = new System.Windows.Forms.NumericUpDown();
            this.boxTabPage2 = new System.Windows.Forms.TabPage();
            this.boxActualBnum = new System.Windows.Forms.NumericUpDown();
            this.boxActualGnum = new System.Windows.Forms.NumericUpDown();
            this.boxActualRnum = new System.Windows.Forms.NumericUpDown();
            this.boxSampleBnum = new System.Windows.Forms.NumericUpDown();
            this.boxSampleGnum = new System.Windows.Forms.NumericUpDown();
            this.boxSampleRnum = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.boxActualB = new System.Windows.Forms.TrackBar();
            this.boxActualG = new System.Windows.Forms.TrackBar();
            this.boxActualR = new System.Windows.Forms.TrackBar();
            this.boxSampleB = new System.Windows.Forms.TrackBar();
            this.boxSampleG = new System.Windows.Forms.TrackBar();
            this.boxSampleR = new System.Windows.Forms.TrackBar();
            this.boxActualColor = new System.Windows.Forms.Panel();
            this.boxSampleColor = new System.Windows.Forms.Panel();
            this.boxLedIndex = new System.Windows.Forms.NumericUpDown();
            this.label11 = new System.Windows.Forms.Label();
            this.statusStrip1.SuspendLayout();
            this.boxTab.SuspendLayout();
            this.boxTabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boxDelay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxRepeatCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxPortNum)).BeginInit();
            this.boxTabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualBnum)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualGnum)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualRnum)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleBnum)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleGnum)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleRnum)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualB)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualG)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualR)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleB)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleG)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleR)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxLedIndex)).BeginInit();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.boxStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 394);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(621, 22);
            this.statusStrip1.TabIndex = 6;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // boxStatus
            // 
            this.boxStatus.Name = "boxStatus";
            this.boxStatus.Size = new System.Drawing.Size(0, 17);
            // 
            // boxTab
            // 
            this.boxTab.Controls.Add(this.boxTabPage1);
            this.boxTab.Controls.Add(this.boxTabPage2);
            this.boxTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boxTab.Location = new System.Drawing.Point(0, 0);
            this.boxTab.Name = "boxTab";
            this.boxTab.SelectedIndex = 0;
            this.boxTab.Size = new System.Drawing.Size(621, 394);
            this.boxTab.TabIndex = 7;
            // 
            // boxTabPage1
            // 
            this.boxTabPage1.Controls.Add(this.boxLogToFile);
            this.boxTabPage1.Controls.Add(this.label10);
            this.boxTabPage1.Controls.Add(this.label9);
            this.boxTabPage1.Controls.Add(this.boxDelay);
            this.boxTabPage1.Controls.Add(this.boxRepeatCount);
            this.boxTabPage1.Controls.Add(this.label8);
            this.boxTabPage1.Controls.Add(this.boxSendRequest);
            this.boxTabPage1.Controls.Add(this.boxDeleteRequest);
            this.boxTabPage1.Controls.Add(this.boxRequest);
            this.boxTabPage1.Controls.Add(this.boxClear);
            this.boxTabPage1.Controls.Add(this.boxStateQuery);
            this.boxTabPage1.Controls.Add(this.boxLoadProj);
            this.boxTabPage1.Controls.Add(this.boxChooseProj);
            this.boxTabPage1.Controls.Add(this.boxPathProject);
            this.boxTabPage1.Controls.Add(this.boxLoadFW);
            this.boxTabPage1.Controls.Add(this.boxLog);
            this.boxTabPage1.Controls.Add(this.boxChoosePath);
            this.boxTabPage1.Controls.Add(this.boxPathFW);
            this.boxTabPage1.Controls.Add(this.boxDisconnect);
            this.boxTabPage1.Controls.Add(this.boxConnect);
            this.boxTabPage1.Controls.Add(this.label1);
            this.boxTabPage1.Controls.Add(this.boxPortNum);
            this.boxTabPage1.Location = new System.Drawing.Point(4, 22);
            this.boxTabPage1.Name = "boxTabPage1";
            this.boxTabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.boxTabPage1.Size = new System.Drawing.Size(613, 368);
            this.boxTabPage1.TabIndex = 0;
            this.boxTabPage1.Text = "Прошивка и загрузка проекта";
            this.boxTabPage1.UseVisualStyleBackColor = true;
            // 
            // boxLogToFile
            // 
            this.boxLogToFile.AutoSize = true;
            this.boxLogToFile.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.boxLogToFile.Location = new System.Drawing.Point(524, 128);
            this.boxLogToFile.Name = "boxLogToFile";
            this.boxLogToFile.Size = new System.Drawing.Size(81, 17);
            this.boxLogToFile.TabIndex = 36;
            this.boxLogToFile.Text = "лог в файл";
            this.boxLogToFile.UseVisualStyleBackColor = true;
            this.boxLogToFile.CheckedChanged += new System.EventHandler(this.boxLogToFile_CheckedChanged);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(428, 123);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(21, 13);
            this.label10.TabIndex = 35;
            this.label10.Text = "мс";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(222, 123);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(75, 13);
            this.label9.TabIndex = 34;
            this.label9.Text = "с задержкой:";
            // 
            // boxDelay
            // 
            this.boxDelay.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.boxDelay.Location = new System.Drawing.Point(302, 121);
            this.boxDelay.Maximum = new decimal(new int[] {
            20000,
            0,
            0,
            0});
            this.boxDelay.Minimum = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.boxDelay.Name = "boxDelay";
            this.boxDelay.Size = new System.Drawing.Size(120, 20);
            this.boxDelay.TabIndex = 33;
            this.boxDelay.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            // 
            // boxRepeatCount
            // 
            this.boxRepeatCount.Location = new System.Drawing.Point(77, 121);
            this.boxRepeatCount.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.boxRepeatCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.boxRepeatCount.Name = "boxRepeatCount";
            this.boxRepeatCount.Size = new System.Drawing.Size(120, 20);
            this.boxRepeatCount.TabIndex = 32;
            this.boxRepeatCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(12, 123);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(64, 13);
            this.label8.TabIndex = 31;
            this.label8.Text = "Повторить:";
            // 
            // boxSendRequest
            // 
            this.boxSendRequest.AutoSize = true;
            this.boxSendRequest.Location = new System.Drawing.Point(12, 94);
            this.boxSendRequest.Name = "boxSendRequest";
            this.boxSendRequest.Size = new System.Drawing.Size(64, 13);
            this.boxSendRequest.TabIndex = 30;
            this.boxSendRequest.TabStop = true;
            this.boxSendRequest.Text = "Отправить:";
            this.boxSendRequest.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxSendRequest_LinkClicked);
            // 
            // boxDeleteRequest
            // 
            this.boxDeleteRequest.AutoSize = true;
            this.boxDeleteRequest.Location = new System.Drawing.Point(551, 94);
            this.boxDeleteRequest.Name = "boxDeleteRequest";
            this.boxDeleteRequest.Size = new System.Drawing.Size(50, 13);
            this.boxDeleteRequest.TabIndex = 29;
            this.boxDeleteRequest.TabStop = true;
            this.boxDeleteRequest.Text = "Удалить";
            this.boxDeleteRequest.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxDeleteRequest_LinkClicked);
            // 
            // boxRequest
            // 
            this.boxRequest.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.boxRequest.FormattingEnabled = true;
            this.boxRequest.Location = new System.Drawing.Point(77, 91);
            this.boxRequest.Name = "boxRequest";
            this.boxRequest.Size = new System.Drawing.Size(460, 21);
            this.boxRequest.TabIndex = 28;
            // 
            // boxClear
            // 
            this.boxClear.AutoSize = true;
            this.boxClear.Location = new System.Drawing.Point(551, 159);
            this.boxClear.Name = "boxClear";
            this.boxClear.Size = new System.Drawing.Size(54, 13);
            this.boxClear.TabIndex = 26;
            this.boxClear.TabStop = true;
            this.boxClear.Text = "Очистить";
            this.boxClear.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxClear_LinkClicked);
            // 
            // boxStateQuery
            // 
            this.boxStateQuery.AutoSize = true;
            this.boxStateQuery.Location = new System.Drawing.Point(276, 10);
            this.boxStateQuery.Name = "boxStateQuery";
            this.boxStateQuery.Size = new System.Drawing.Size(100, 13);
            this.boxStateQuery.TabIndex = 25;
            this.boxStateQuery.TabStop = true;
            this.boxStateQuery.Text = "Запрос состояния";
            this.boxStateQuery.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxStateQuery_LinkClicked);
            // 
            // boxLoadProj
            // 
            this.boxLoadProj.AutoSize = true;
            this.boxLoadProj.Location = new System.Drawing.Point(551, 37);
            this.boxLoadProj.Name = "boxLoadProj";
            this.boxLoadProj.Size = new System.Drawing.Size(59, 13);
            this.boxLoadProj.TabIndex = 24;
            this.boxLoadProj.TabStop = true;
            this.boxLoadProj.Text = "Загрузить";
            this.boxLoadProj.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxLoadProj_LinkClicked);
            // 
            // boxChooseProj
            // 
            this.boxChooseProj.AutoSize = true;
            this.boxChooseProj.Location = new System.Drawing.Point(12, 37);
            this.boxChooseProj.Name = "boxChooseProj";
            this.boxChooseProj.Size = new System.Drawing.Size(92, 13);
            this.boxChooseProj.TabIndex = 23;
            this.boxChooseProj.TabStop = true;
            this.boxChooseProj.Text = "Выбрать проект:";
            this.boxChooseProj.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxChooseProj_LinkClicked);
            // 
            // boxPathProject
            // 
            this.boxPathProject.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.boxPathProject.Location = new System.Drawing.Point(124, 34);
            this.boxPathProject.Name = "boxPathProject";
            this.boxPathProject.Size = new System.Drawing.Size(413, 20);
            this.boxPathProject.TabIndex = 22;
            // 
            // boxLoadFW
            // 
            this.boxLoadFW.AutoSize = true;
            this.boxLoadFW.Location = new System.Drawing.Point(551, 63);
            this.boxLoadFW.Name = "boxLoadFW";
            this.boxLoadFW.Size = new System.Drawing.Size(59, 13);
            this.boxLoadFW.TabIndex = 21;
            this.boxLoadFW.TabStop = true;
            this.boxLoadFW.Text = "Загрузить";
            this.boxLoadFW.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxLoadFW_LinkClicked);
            // 
            // boxLog
            // 
            this.boxLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.boxLog.Location = new System.Drawing.Point(3, 151);
            this.boxLog.Name = "boxLog";
            this.boxLog.ReadOnly = true;
            this.boxLog.Size = new System.Drawing.Size(607, 214);
            this.boxLog.TabIndex = 20;
            this.boxLog.Text = "";
            // 
            // boxChoosePath
            // 
            this.boxChoosePath.AutoSize = true;
            this.boxChoosePath.Location = new System.Drawing.Point(12, 63);
            this.boxChoosePath.Name = "boxChoosePath";
            this.boxChoosePath.Size = new System.Drawing.Size(106, 13);
            this.boxChoosePath.TabIndex = 19;
            this.boxChoosePath.TabStop = true;
            this.boxChoosePath.Text = "Выбрать прошивку:";
            this.boxChoosePath.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxChooseFW_LinkClicked);
            // 
            // boxPathFW
            // 
            this.boxPathFW.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.boxPathFW.Location = new System.Drawing.Point(124, 60);
            this.boxPathFW.Name = "boxPathFW";
            this.boxPathFW.Size = new System.Drawing.Size(413, 20);
            this.boxPathFW.TabIndex = 18;
            // 
            // boxDisconnect
            // 
            this.boxDisconnect.AutoSize = true;
            this.boxDisconnect.Location = new System.Drawing.Point(173, 10);
            this.boxDisconnect.Name = "boxDisconnect";
            this.boxDisconnect.Size = new System.Drawing.Size(62, 13);
            this.boxDisconnect.TabIndex = 17;
            this.boxDisconnect.TabStop = true;
            this.boxDisconnect.Text = "Отключить";
            this.boxDisconnect.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxDisconnect_LinkClicked);
            // 
            // boxConnect
            // 
            this.boxConnect.AutoSize = true;
            this.boxConnect.Location = new System.Drawing.Point(98, 10);
            this.boxConnect.Name = "boxConnect";
            this.boxConnect.Size = new System.Drawing.Size(69, 13);
            this.boxConnect.TabIndex = 16;
            this.boxConnect.TabStop = true;
            this.boxConnect.Text = "Подключить";
            this.boxConnect.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxConnect_LinkClicked);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(31, 13);
            this.label1.TabIndex = 15;
            this.label1.Text = "COM";
            // 
            // boxPortNum
            // 
            this.boxPortNum.Location = new System.Drawing.Point(43, 8);
            this.boxPortNum.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.boxPortNum.Name = "boxPortNum";
            this.boxPortNum.Size = new System.Drawing.Size(49, 20);
            this.boxPortNum.TabIndex = 14;
            this.boxPortNum.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // boxTabPage2
            // 
            this.boxTabPage2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.boxTabPage2.Controls.Add(this.label11);
            this.boxTabPage2.Controls.Add(this.boxLedIndex);
            this.boxTabPage2.Controls.Add(this.boxActualBnum);
            this.boxTabPage2.Controls.Add(this.boxActualGnum);
            this.boxTabPage2.Controls.Add(this.boxActualRnum);
            this.boxTabPage2.Controls.Add(this.boxSampleBnum);
            this.boxTabPage2.Controls.Add(this.boxSampleGnum);
            this.boxTabPage2.Controls.Add(this.boxSampleRnum);
            this.boxTabPage2.Controls.Add(this.label5);
            this.boxTabPage2.Controls.Add(this.label6);
            this.boxTabPage2.Controls.Add(this.label7);
            this.boxTabPage2.Controls.Add(this.label4);
            this.boxTabPage2.Controls.Add(this.label3);
            this.boxTabPage2.Controls.Add(this.label2);
            this.boxTabPage2.Controls.Add(this.boxActualB);
            this.boxTabPage2.Controls.Add(this.boxActualG);
            this.boxTabPage2.Controls.Add(this.boxActualR);
            this.boxTabPage2.Controls.Add(this.boxSampleB);
            this.boxTabPage2.Controls.Add(this.boxSampleG);
            this.boxTabPage2.Controls.Add(this.boxSampleR);
            this.boxTabPage2.Controls.Add(this.boxActualColor);
            this.boxTabPage2.Controls.Add(this.boxSampleColor);
            this.boxTabPage2.Location = new System.Drawing.Point(4, 22);
            this.boxTabPage2.Name = "boxTabPage2";
            this.boxTabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.boxTabPage2.Size = new System.Drawing.Size(613, 368);
            this.boxTabPage2.TabIndex = 1;
            this.boxTabPage2.Text = "Калибровка цвета";
            // 
            // boxActualBnum
            // 
            this.boxActualBnum.Location = new System.Drawing.Point(435, 221);
            this.boxActualBnum.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.boxActualBnum.Name = "boxActualBnum";
            this.boxActualBnum.Size = new System.Drawing.Size(44, 20);
            this.boxActualBnum.TabIndex = 19;
            this.boxActualBnum.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.boxActualBnum.ValueChanged += new System.EventHandler(this.boxActualBnum_ValueChanged);
            // 
            // boxActualGnum
            // 
            this.boxActualGnum.Location = new System.Drawing.Point(385, 221);
            this.boxActualGnum.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.boxActualGnum.Name = "boxActualGnum";
            this.boxActualGnum.Size = new System.Drawing.Size(44, 20);
            this.boxActualGnum.TabIndex = 18;
            this.boxActualGnum.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.boxActualGnum.ValueChanged += new System.EventHandler(this.boxActualGnum_ValueChanged);
            // 
            // boxActualRnum
            // 
            this.boxActualRnum.Location = new System.Drawing.Point(335, 221);
            this.boxActualRnum.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.boxActualRnum.Name = "boxActualRnum";
            this.boxActualRnum.Size = new System.Drawing.Size(44, 20);
            this.boxActualRnum.TabIndex = 17;
            this.boxActualRnum.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.boxActualRnum.ValueChanged += new System.EventHandler(this.boxActualRnum_ValueChanged);
            // 
            // boxSampleBnum
            // 
            this.boxSampleBnum.Location = new System.Drawing.Point(233, 221);
            this.boxSampleBnum.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.boxSampleBnum.Name = "boxSampleBnum";
            this.boxSampleBnum.Size = new System.Drawing.Size(44, 20);
            this.boxSampleBnum.TabIndex = 16;
            this.boxSampleBnum.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.boxSampleBnum.ValueChanged += new System.EventHandler(this.boxSampleBnum_ValueChanged);
            // 
            // boxSampleGnum
            // 
            this.boxSampleGnum.Location = new System.Drawing.Point(183, 221);
            this.boxSampleGnum.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.boxSampleGnum.Name = "boxSampleGnum";
            this.boxSampleGnum.Size = new System.Drawing.Size(44, 20);
            this.boxSampleGnum.TabIndex = 15;
            this.boxSampleGnum.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.boxSampleGnum.ValueChanged += new System.EventHandler(this.boxSampleGnum_ValueChanged);
            // 
            // boxSampleRnum
            // 
            this.boxSampleRnum.Location = new System.Drawing.Point(133, 221);
            this.boxSampleRnum.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.boxSampleRnum.Name = "boxSampleRnum";
            this.boxSampleRnum.Size = new System.Drawing.Size(44, 20);
            this.boxSampleRnum.TabIndex = 14;
            this.boxSampleRnum.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.boxSampleRnum.ValueChanged += new System.EventHandler(this.boxSampleRnum_ValueChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.ForeColor = System.Drawing.SystemColors.Info;
            this.label5.Location = new System.Drawing.Point(444, 203);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(14, 13);
            this.label5.TabIndex = 13;
            this.label5.Text = "B";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.ForeColor = System.Drawing.SystemColors.Info;
            this.label6.Location = new System.Drawing.Point(392, 203);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(15, 13);
            this.label6.TabIndex = 12;
            this.label6.Text = "G";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.ForeColor = System.Drawing.SystemColors.Info;
            this.label7.Location = new System.Drawing.Point(342, 203);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(15, 13);
            this.label7.TabIndex = 11;
            this.label7.Text = "R";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.ForeColor = System.Drawing.SystemColors.Info;
            this.label4.Location = new System.Drawing.Point(244, 203);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(14, 13);
            this.label4.TabIndex = 10;
            this.label4.Text = "B";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.ForeColor = System.Drawing.SystemColors.Info;
            this.label3.Location = new System.Drawing.Point(192, 203);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(15, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "G";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.SystemColors.Info;
            this.label2.Location = new System.Drawing.Point(142, 203);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(15, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "R";
            // 
            // boxActualB
            // 
            this.boxActualB.Location = new System.Drawing.Point(441, 95);
            this.boxActualB.Maximum = 255;
            this.boxActualB.Name = "boxActualB";
            this.boxActualB.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.boxActualB.Size = new System.Drawing.Size(45, 104);
            this.boxActualB.TabIndex = 7;
            this.boxActualB.TickStyle = System.Windows.Forms.TickStyle.None;
            this.boxActualB.Scroll += new System.EventHandler(this.boxActualB_Scroll);
            // 
            // boxActualG
            // 
            this.boxActualG.Location = new System.Drawing.Point(390, 95);
            this.boxActualG.Maximum = 255;
            this.boxActualG.Name = "boxActualG";
            this.boxActualG.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.boxActualG.Size = new System.Drawing.Size(45, 104);
            this.boxActualG.TabIndex = 6;
            this.boxActualG.TickStyle = System.Windows.Forms.TickStyle.None;
            this.boxActualG.Scroll += new System.EventHandler(this.boxActualG_Scroll);
            // 
            // boxActualR
            // 
            this.boxActualR.Location = new System.Drawing.Point(339, 95);
            this.boxActualR.Maximum = 255;
            this.boxActualR.Name = "boxActualR";
            this.boxActualR.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.boxActualR.Size = new System.Drawing.Size(45, 104);
            this.boxActualR.TabIndex = 5;
            this.boxActualR.TickStyle = System.Windows.Forms.TickStyle.None;
            this.boxActualR.Scroll += new System.EventHandler(this.boxActualR_Scroll);
            // 
            // boxSampleB
            // 
            this.boxSampleB.Location = new System.Drawing.Point(240, 95);
            this.boxSampleB.Maximum = 255;
            this.boxSampleB.Name = "boxSampleB";
            this.boxSampleB.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.boxSampleB.Size = new System.Drawing.Size(45, 104);
            this.boxSampleB.TabIndex = 4;
            this.boxSampleB.TickStyle = System.Windows.Forms.TickStyle.None;
            this.boxSampleB.Scroll += new System.EventHandler(this.boxSampleB_Scroll);
            // 
            // boxSampleG
            // 
            this.boxSampleG.Location = new System.Drawing.Point(189, 95);
            this.boxSampleG.Maximum = 255;
            this.boxSampleG.Name = "boxSampleG";
            this.boxSampleG.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.boxSampleG.Size = new System.Drawing.Size(45, 104);
            this.boxSampleG.TabIndex = 3;
            this.boxSampleG.TickStyle = System.Windows.Forms.TickStyle.None;
            this.boxSampleG.Scroll += new System.EventHandler(this.boxSampleG_Scroll);
            // 
            // boxSampleR
            // 
            this.boxSampleR.Location = new System.Drawing.Point(138, 95);
            this.boxSampleR.Maximum = 255;
            this.boxSampleR.Name = "boxSampleR";
            this.boxSampleR.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.boxSampleR.Size = new System.Drawing.Size(45, 104);
            this.boxSampleR.TabIndex = 2;
            this.boxSampleR.TickStyle = System.Windows.Forms.TickStyle.None;
            this.boxSampleR.Scroll += new System.EventHandler(this.boxSampleR_Scroll);
            // 
            // boxActualColor
            // 
            this.boxActualColor.ForeColor = System.Drawing.Color.Transparent;
            this.boxActualColor.Location = new System.Drawing.Point(339, 44);
            this.boxActualColor.Name = "boxActualColor";
            this.boxActualColor.Size = new System.Drawing.Size(147, 45);
            this.boxActualColor.TabIndex = 1;
            // 
            // boxSampleColor
            // 
            this.boxSampleColor.ForeColor = System.Drawing.Color.Transparent;
            this.boxSampleColor.Location = new System.Drawing.Point(138, 44);
            this.boxSampleColor.Name = "boxSampleColor";
            this.boxSampleColor.Size = new System.Drawing.Size(147, 45);
            this.boxSampleColor.TabIndex = 0;
            // 
            // boxLedIndex
            // 
            this.boxLedIndex.Location = new System.Drawing.Point(435, 257);
            this.boxLedIndex.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.boxLedIndex.Name = "boxLedIndex";
            this.boxLedIndex.Size = new System.Drawing.Size(44, 20);
            this.boxLedIndex.TabIndex = 20;
            this.boxLedIndex.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.ForeColor = System.Drawing.SystemColors.Info;
            this.label11.Location = new System.Drawing.Point(342, 259);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(88, 13);
            this.label11.TabIndex = 21;
            this.label11.Text = "LED index to set:";
            // 
            // MainFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(621, 416);
            this.Controls.Add(this.boxTab);
            this.Controls.Add(this.statusStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MainFrm";
            this.Text = "Связь";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFrm_FormClosing);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.boxTab.ResumeLayout(false);
            this.boxTabPage1.ResumeLayout(false);
            this.boxTabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boxDelay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxRepeatCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxPortNum)).EndInit();
            this.boxTabPage2.ResumeLayout(false);
            this.boxTabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualBnum)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualGnum)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualRnum)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleBnum)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleGnum)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleRnum)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualB)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualG)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxActualR)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleB)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleG)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSampleR)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxLedIndex)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel boxStatus;
        private System.Windows.Forms.TabControl boxTab;
        private System.Windows.Forms.TabPage boxTabPage1;
        private System.Windows.Forms.LinkLabel boxClear;
        private System.Windows.Forms.LinkLabel boxStateQuery;
        private System.Windows.Forms.LinkLabel boxLoadProj;
        private System.Windows.Forms.LinkLabel boxChooseProj;
        private System.Windows.Forms.TextBox boxPathProject;
        private System.Windows.Forms.LinkLabel boxLoadFW;
        private System.Windows.Forms.RichTextBox boxLog;
        private System.Windows.Forms.LinkLabel boxChoosePath;
        private System.Windows.Forms.TextBox boxPathFW;
        private System.Windows.Forms.LinkLabel boxDisconnect;
        private System.Windows.Forms.LinkLabel boxConnect;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown boxPortNum;
        private System.Windows.Forms.TabPage boxTabPage2;
        private System.Windows.Forms.TrackBar boxActualB;
        private System.Windows.Forms.TrackBar boxActualG;
        private System.Windows.Forms.TrackBar boxActualR;
        private System.Windows.Forms.TrackBar boxSampleB;
        private System.Windows.Forms.TrackBar boxSampleG;
        private System.Windows.Forms.TrackBar boxSampleR;
        private System.Windows.Forms.Panel boxActualColor;
        private System.Windows.Forms.Panel boxSampleColor;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown boxActualBnum;
        private System.Windows.Forms.NumericUpDown boxActualGnum;
        private System.Windows.Forms.NumericUpDown boxActualRnum;
        private System.Windows.Forms.NumericUpDown boxSampleBnum;
        private System.Windows.Forms.NumericUpDown boxSampleGnum;
        private System.Windows.Forms.NumericUpDown boxSampleRnum;
        private System.Windows.Forms.ComboBox boxRequest;
        private System.Windows.Forms.LinkLabel boxDeleteRequest;
        private System.Windows.Forms.LinkLabel boxSendRequest;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.NumericUpDown boxDelay;
        private System.Windows.Forms.NumericUpDown boxRepeatCount;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.CheckBox boxLogToFile;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.NumericUpDown boxLedIndex;
    }
}

