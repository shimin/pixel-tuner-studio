namespace PixelTuner.Drawing
{
    partial class TextDialog
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
            this.components = new System.ComponentModel.Container();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.label4 = new System.Windows.Forms.Label();
            this.boxLetters = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.boxPlaceButton = new System.Windows.Forms.Button();
            this.boxVScale = new System.Windows.Forms.NumericUpDown();
            this.boxHScale = new System.Windows.Forms.NumericUpDown();
            this.boxSpacing = new System.Windows.Forms.NumericUpDown();
            this.boxHeight = new System.Windows.Forms.NumericUpDown();
            this.boxFontType = new System.Windows.Forms.ComboBox();
            this.boxFontFamily = new System.Windows.Forms.ComboBox();
            this.boxCanvas = new System.Windows.Forms.Panel();
            this.cursorTimer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boxVScale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxHScale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSpacing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxHeight)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(0);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.label4);
            this.splitContainer.Panel1.Controls.Add(this.boxLetters);
            this.splitContainer.Panel1.Controls.Add(this.label3);
            this.splitContainer.Panel1.Controls.Add(this.label2);
            this.splitContainer.Panel1.Controls.Add(this.label1);
            this.splitContainer.Panel1.Controls.Add(this.boxPlaceButton);
            this.splitContainer.Panel1.Controls.Add(this.boxVScale);
            this.splitContainer.Panel1.Controls.Add(this.boxHScale);
            this.splitContainer.Panel1.Controls.Add(this.boxSpacing);
            this.splitContainer.Panel1.Controls.Add(this.boxHeight);
            this.splitContainer.Panel1.Controls.Add(this.boxFontType);
            this.splitContainer.Panel1.Controls.Add(this.boxFontFamily);
            this.splitContainer.Panel1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.splitContainer_Panel1_MouseDown);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.boxCanvas);
            this.splitContainer.Size = new System.Drawing.Size(1315, 301);
            this.splitContainer.SplitterDistance = 261;
            this.splitContainer.SplitterWidth = 2;
            this.splitContainer.TabIndex = 0;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(17, 25);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(114, 13);
            this.label4.TabIndex = 10;
            this.label4.Text = "Параметры в точках:";
            // 
            // boxLetters
            // 
            this.boxLetters.Location = new System.Drawing.Point(4, 84);
            this.boxLetters.Name = "boxLetters";
            this.boxLetters.Size = new System.Drawing.Size(252, 20);
            this.boxLetters.TabIndex = 0;
            this.boxLetters.TextChanged += new System.EventHandler(this.boxString_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Enabled = false;
            this.label3.Location = new System.Drawing.Point(202, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Масштаб";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(5, 65);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(75, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Промеж букв";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 43);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(45, 13);
            this.label1.TabIndex = 7;
            this.label1.Text = "Высота";
            // 
            // boxPlaceButton
            // 
            this.boxPlaceButton.BackColor = System.Drawing.Color.WhiteSmoke;
            this.boxPlaceButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.boxPlaceButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxPlaceButton.Location = new System.Drawing.Point(4, 106);
            this.boxPlaceButton.Name = "boxPlaceButton";
            this.boxPlaceButton.Size = new System.Drawing.Size(252, 35);
            this.boxPlaceButton.TabIndex = 6;
            this.boxPlaceButton.TabStop = false;
            this.boxPlaceButton.Text = "Разместить";
            this.boxPlaceButton.UseVisualStyleBackColor = false;
            this.boxPlaceButton.Click += new System.EventHandler(this.boxPlaceButton_Click);
            // 
            // boxVScale
            // 
            this.boxVScale.Enabled = false;
            this.boxVScale.Location = new System.Drawing.Point(207, 62);
            this.boxVScale.Minimum = new decimal(new int[] {
            25,
            0,
            0,
            0});
            this.boxVScale.Name = "boxVScale";
            this.boxVScale.Size = new System.Drawing.Size(45, 20);
            this.boxVScale.TabIndex = 5;
            this.boxVScale.TabStop = false;
            this.boxVScale.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.boxVScale.ValueChanged += new System.EventHandler(this.boxVScale_ValueChanged);
            // 
            // boxHScale
            // 
            this.boxHScale.Enabled = false;
            this.boxHScale.Location = new System.Drawing.Point(207, 40);
            this.boxHScale.Minimum = new decimal(new int[] {
            25,
            0,
            0,
            0});
            this.boxHScale.Name = "boxHScale";
            this.boxHScale.Size = new System.Drawing.Size(45, 20);
            this.boxHScale.TabIndex = 4;
            this.boxHScale.TabStop = false;
            this.boxHScale.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.boxHScale.ValueChanged += new System.EventHandler(this.boxHScale_ValueChanged);
            // 
            // boxSpacing
            // 
            this.boxSpacing.Location = new System.Drawing.Point(80, 63);
            this.boxSpacing.Maximum = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.boxSpacing.Name = "boxSpacing";
            this.boxSpacing.Size = new System.Drawing.Size(48, 20);
            this.boxSpacing.TabIndex = 3;
            this.boxSpacing.TabStop = false;
            this.boxSpacing.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.boxSpacing.ValueChanged += new System.EventHandler(this.boxSpacing_ValueChanged);
            // 
            // boxHeight
            // 
            this.boxHeight.Location = new System.Drawing.Point(80, 41);
            this.boxHeight.Minimum = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.boxHeight.Name = "boxHeight";
            this.boxHeight.Size = new System.Drawing.Size(48, 20);
            this.boxHeight.TabIndex = 2;
            this.boxHeight.TabStop = false;
            this.boxHeight.Value = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.boxHeight.ValueChanged += new System.EventHandler(this.boxHeight_ValueChanged);
            // 
            // boxFontType
            // 
            this.boxFontType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.boxFontType.FormattingEnabled = true;
            this.boxFontType.Location = new System.Drawing.Point(155, 3);
            this.boxFontType.Name = "boxFontType";
            this.boxFontType.Size = new System.Drawing.Size(101, 21);
            this.boxFontType.TabIndex = 1;
            this.boxFontType.TabStop = false;
            this.boxFontType.SelectedIndexChanged += new System.EventHandler(this.boxFontType_SelectedIndexChanged);
            // 
            // boxFontFamily
            // 
            this.boxFontFamily.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.boxFontFamily.FormattingEnabled = true;
            this.boxFontFamily.Location = new System.Drawing.Point(3, 3);
            this.boxFontFamily.Name = "boxFontFamily";
            this.boxFontFamily.Size = new System.Drawing.Size(146, 21);
            this.boxFontFamily.TabIndex = 0;
            this.boxFontFamily.TabStop = false;
            this.boxFontFamily.SelectedIndexChanged += new System.EventHandler(this.boxFontFamily_SelectedIndexChanged);
            // 
            // boxCanvas
            // 
            this.boxCanvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boxCanvas.Location = new System.Drawing.Point(0, 0);
            this.boxCanvas.Margin = new System.Windows.Forms.Padding(0);
            this.boxCanvas.Name = "boxCanvas";
            this.boxCanvas.Size = new System.Drawing.Size(1052, 301);
            this.boxCanvas.TabIndex = 0;
            this.boxCanvas.TabStop = true;
            this.boxCanvas.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.boxCanvas_PreviewKeyDown);
            // 
            // cursorTimer
            // 
            this.cursorTimer.Interval = 500;
            this.cursorTimer.Tick += new System.EventHandler(this.cursorTimer_Tick);
            // 
            // TextDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1315, 301);
            this.Controls.Add(this.splitContainer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "TextDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Ввод текста";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TextDialog_FormClosing);
            this.Click += new System.EventHandler(this.TextDialog_Click);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.TextDialog_MouseDown);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.boxVScale)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxHScale)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxSpacing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxHeight)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.NumericUpDown boxVScale;
        private System.Windows.Forms.NumericUpDown boxHScale;
        private System.Windows.Forms.NumericUpDown boxSpacing;
        private System.Windows.Forms.NumericUpDown boxHeight;
        private System.Windows.Forms.ComboBox boxFontType;
        private System.Windows.Forms.ComboBox boxFontFamily;
        private System.Windows.Forms.Panel boxCanvas;
        private System.Windows.Forms.Button boxPlaceButton;
        private System.Windows.Forms.Timer cursorTimer;
        private System.Windows.Forms.TextBox boxLetters;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
    }
}