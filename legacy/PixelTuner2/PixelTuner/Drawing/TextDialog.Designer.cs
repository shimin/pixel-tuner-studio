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
            this.boxLetters = new System.Windows.Forms.TextBox();
            this.boxPlaceButton = new System.Windows.Forms.Button();
            this.boxSpacing = new System.Windows.Forms.NumericUpDown();
            this.boxHeight = new System.Windows.Forms.NumericUpDown();
            this.boxFontType = new System.Windows.Forms.ComboBox();
            this.boxFontFamily = new System.Windows.Forms.ComboBox();
            this.boxCanvas = new PixelCanvas();
            this.cursorTimer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
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
            this.splitContainer.Panel1.Controls.Add(this.boxLetters);
            this.splitContainer.Panel1.Controls.Add(this.boxPlaceButton);
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
            this.splitContainer.SplitterDistance = 183;
            this.splitContainer.SplitterWidth = 2;
            this.splitContainer.TabIndex = 0;
            // 
            // boxLetters
            // 
            this.boxLetters.Location = new System.Drawing.Point(4, 65);
            this.boxLetters.Name = "boxLetters";
            this.boxLetters.Size = new System.Drawing.Size(175, 20);
            this.boxLetters.TabIndex = 0;
            this.boxLetters.TextChanged += new System.EventHandler(this.boxString_TextChanged);
            // 
            // boxPlaceButton
            // 
            this.boxPlaceButton.BackColor = System.Drawing.Color.WhiteSmoke;
            this.boxPlaceButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.boxPlaceButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxPlaceButton.Location = new System.Drawing.Point(5, 89);
            this.boxPlaceButton.Name = "boxPlaceButton";
            this.boxPlaceButton.Size = new System.Drawing.Size(172, 35);
            this.boxPlaceButton.TabIndex = 6;
            this.boxPlaceButton.TabStop = false;
            this.boxPlaceButton.Text = "Done";
            this.boxPlaceButton.UseVisualStyleBackColor = false;
            this.boxPlaceButton.Click += new System.EventHandler(this.boxPlaceButton_Click);
            // 
            // boxSpacing
            // 
            this.boxSpacing.Location = new System.Drawing.Point(132, 30);
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
            this.boxHeight.Location = new System.Drawing.Point(87, 30);
            this.boxHeight.Minimum = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.boxHeight.Name = "boxHeight";
            this.boxHeight.Size = new System.Drawing.Size(42, 20);
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
            this.boxFontType.Location = new System.Drawing.Point(4, 29);
            this.boxFontType.Name = "boxFontType";
            this.boxFontType.Size = new System.Drawing.Size(79, 21);
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
            this.boxFontFamily.Size = new System.Drawing.Size(177, 21);
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
            this.boxCanvas.Size = new System.Drawing.Size(1130, 301);
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
            this.Text = "Pixelate";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TextDialog_FormClosing);
            this.Click += new System.EventHandler(this.TextDialog_Click);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.TextDialog_MouseDown);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.boxSpacing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxHeight)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.NumericUpDown boxSpacing;
        private System.Windows.Forms.NumericUpDown boxHeight;
        private System.Windows.Forms.ComboBox boxFontType;
        private System.Windows.Forms.ComboBox boxFontFamily;
        private PixelCanvas boxCanvas;
        private System.Windows.Forms.Button boxPlaceButton;
        private System.Windows.Forms.Timer cursorTimer;
        private System.Windows.Forms.TextBox boxLetters;
    }
}