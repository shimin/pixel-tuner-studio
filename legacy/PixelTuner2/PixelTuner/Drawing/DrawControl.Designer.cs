namespace PixelTuner.Drawing
{
    partial class DrawControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.boxPipette = new System.Windows.Forms.Button();
            this.boxLabelWidth = new System.Windows.Forms.Label();
            this.boxWidth = new System.Windows.Forms.NumericUpDown();
            this.boxScaleLabel = new System.Windows.Forms.Label();
            this.boxPointSize = new System.Windows.Forms.NumericUpDown();
            this.boxLImageLinesCountLabel = new System.Windows.Forms.Label();
            this.boxHeight = new System.Windows.Forms.NumericUpDown();
            this.boxBmpImport = new System.Windows.Forms.Button();
            this.boxSelect = new System.Windows.Forms.Button();
            this.boxTextButton = new System.Windows.Forms.Button();
            this.boxFillBG = new System.Windows.Forms.Button();
            this.boxFrontColor = new System.Windows.Forms.Button();
            this.boxDummyButton = new System.Windows.Forms.Button();
            this.boxBgColor = new System.Windows.Forms.Button();
            this.boxTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.boxCanvasHolder = new System.Windows.Forms.Panel();
            this.boxSplit = new System.Windows.Forms.SplitContainer();
            this.boxPipetteTT = new System.Windows.Forms.ToolTip(this.components);
            this.boxCanvas = new PixelTuner.Drawing.PixelCanvas();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boxWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxPointSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxHeight)).BeginInit();
            this.boxTableLayout.SuspendLayout();
            this.boxCanvasHolder.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.boxSplit)).BeginInit();
            this.boxSplit.Panel1.SuspendLayout();
            this.boxSplit.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.BackColor = System.Drawing.SystemColors.Control;
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer.IsSplitterFixed = true;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.boxPipette);
            this.splitContainer.Panel1.Controls.Add(this.boxLabelWidth);
            this.splitContainer.Panel1.Controls.Add(this.boxWidth);
            this.splitContainer.Panel1.Controls.Add(this.boxScaleLabel);
            this.splitContainer.Panel1.Controls.Add(this.boxPointSize);
            this.splitContainer.Panel1.Controls.Add(this.boxLImageLinesCountLabel);
            this.splitContainer.Panel1.Controls.Add(this.boxHeight);
            this.splitContainer.Panel1.Controls.Add(this.boxBmpImport);
            this.splitContainer.Panel1.Controls.Add(this.boxSelect);
            this.splitContainer.Panel1.Controls.Add(this.boxTextButton);
            this.splitContainer.Panel1.Controls.Add(this.boxFillBG);
            this.splitContainer.Panel1.Controls.Add(this.boxFrontColor);
            this.splitContainer.Panel1.Controls.Add(this.boxDummyButton);
            this.splitContainer.Panel1.Controls.Add(this.boxBgColor);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.boxTableLayout);
            this.splitContainer.Size = new System.Drawing.Size(872, 241);
            this.splitContainer.SplitterDistance = 33;
            this.splitContainer.SplitterWidth = 2;
            this.splitContainer.TabIndex = 0;
            // 
            // boxPipette
            // 
            this.boxPipette.BackColor = System.Drawing.SystemColors.Control;
            this.boxPipette.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.boxPipette.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.boxPipette.FlatAppearance.BorderSize = 0;
            this.boxPipette.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxPipette.Font = new System.Drawing.Font("Georgia", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.boxPipette.Location = new System.Drawing.Point(136, 5);
            this.boxPipette.Name = "boxPipette";
            this.boxPipette.Size = new System.Drawing.Size(19, 19);
            this.boxPipette.TabIndex = 37;
            this.boxPipette.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.boxPipetteTT.SetToolTip(this.boxPipette, "Color Sampler Tool (P)");
            this.boxPipette.UseVisualStyleBackColor = false;
            this.boxPipette.Click += new System.EventHandler(this.boxPipette_Click);
            // 
            // boxLabelWidth
            // 
            this.boxLabelWidth.AutoSize = true;
            this.boxLabelWidth.Location = new System.Drawing.Point(399, 7);
            this.boxLabelWidth.Name = "boxLabelWidth";
            this.boxLabelWidth.Size = new System.Drawing.Size(46, 13);
            this.boxLabelWidth.TabIndex = 36;
            this.boxLabelWidth.Text = "Ширина";
            // 
            // boxWidth
            // 
            this.boxWidth.Location = new System.Drawing.Point(444, 5);
            this.boxWidth.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.boxWidth.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.boxWidth.Name = "boxWidth";
            this.boxWidth.Size = new System.Drawing.Size(52, 20);
            this.boxWidth.TabIndex = 35;
            this.boxWidth.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.boxWidth.ValueChanged += new System.EventHandler(this.boxWidth_ValueChanged);
            // 
            // boxScaleLabel
            // 
            this.boxScaleLabel.AutoSize = true;
            this.boxScaleLabel.Location = new System.Drawing.Point(169, 7);
            this.boxScaleLabel.Name = "boxScaleLabel";
            this.boxScaleLabel.Size = new System.Drawing.Size(53, 13);
            this.boxScaleLabel.TabIndex = 34;
            this.boxScaleLabel.Text = "Масштаб";
            // 
            // boxPointSize
            // 
            this.boxPointSize.Location = new System.Drawing.Point(222, 5);
            this.boxPointSize.Maximum = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.boxPointSize.Minimum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.boxPointSize.Name = "boxPointSize";
            this.boxPointSize.Size = new System.Drawing.Size(52, 20);
            this.boxPointSize.TabIndex = 33;
            this.boxPointSize.Value = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.boxPointSize.ValueChanged += new System.EventHandler(this.boxPointSize_ValueChanged);
            // 
            // boxLImageLinesCountLabel
            // 
            this.boxLImageLinesCountLabel.AutoSize = true;
            this.boxLImageLinesCountLabel.Location = new System.Drawing.Point(285, 7);
            this.boxLImageLinesCountLabel.Name = "boxLImageLinesCountLabel";
            this.boxLImageLinesCountLabel.Size = new System.Drawing.Size(45, 13);
            this.boxLImageLinesCountLabel.TabIndex = 32;
            this.boxLImageLinesCountLabel.Text = "Высота";
            // 
            // boxHeight
            // 
            this.boxHeight.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.boxHeight.Location = new System.Drawing.Point(330, 5);
            this.boxHeight.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.boxHeight.Name = "boxHeight";
            this.boxHeight.Size = new System.Drawing.Size(52, 20);
            this.boxHeight.TabIndex = 30;
            this.boxHeight.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.boxHeight.ValueChanged += new System.EventHandler(this.boxHeight_ValueChanged);
            // 
            // boxBmpImport
            // 
            this.boxBmpImport.BackColor = System.Drawing.SystemColors.Control;
            this.boxBmpImport.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.boxBmpImport.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.boxBmpImport.FlatAppearance.BorderSize = 0;
            this.boxBmpImport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxBmpImport.Font = new System.Drawing.Font("Georgia", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.boxBmpImport.Location = new System.Drawing.Point(112, 5);
            this.boxBmpImport.Name = "boxBmpImport";
            this.boxBmpImport.Size = new System.Drawing.Size(20, 19);
            this.boxBmpImport.TabIndex = 29;
            this.boxBmpImport.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.boxPipetteTT.SetToolTip(this.boxBmpImport, "Import picture (I)");
            this.boxBmpImport.UseVisualStyleBackColor = false;
            this.boxBmpImport.Click += new System.EventHandler(this.boxBmpImport_Click);
            // 
            // boxSelect
            // 
            this.boxSelect.BackColor = System.Drawing.SystemColors.Control;
            this.boxSelect.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.boxSelect.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.boxSelect.FlatAppearance.BorderSize = 0;
            this.boxSelect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxSelect.Font = new System.Drawing.Font("Georgia", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.boxSelect.Location = new System.Drawing.Point(40, 5);
            this.boxSelect.Name = "boxSelect";
            this.boxSelect.Size = new System.Drawing.Size(20, 19);
            this.boxSelect.TabIndex = 28;
            this.boxSelect.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.boxPipetteTT.SetToolTip(this.boxSelect, "Selection Tool (M)");
            this.boxSelect.UseVisualStyleBackColor = false;
            this.boxSelect.Click += new System.EventHandler(this.buttonSelect_Click);
            // 
            // boxTextButton
            // 
            this.boxTextButton.BackColor = System.Drawing.SystemColors.Control;
            this.boxTextButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.boxTextButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.boxTextButton.FlatAppearance.BorderSize = 0;
            this.boxTextButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxTextButton.Font = new System.Drawing.Font("Georgia", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.boxTextButton.Location = new System.Drawing.Point(88, 5);
            this.boxTextButton.Name = "boxTextButton";
            this.boxTextButton.Size = new System.Drawing.Size(20, 19);
            this.boxTextButton.TabIndex = 27;
            this.boxTextButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.boxPipetteTT.SetToolTip(this.boxTextButton, "Pixelate Text (T)");
            this.boxTextButton.UseVisualStyleBackColor = false;
            this.boxTextButton.Click += new System.EventHandler(this.boxTextButton_Click);
            // 
            // boxFillBG
            // 
            this.boxFillBG.BackColor = System.Drawing.SystemColors.Control;
            this.boxFillBG.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.boxFillBG.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.boxFillBG.FlatAppearance.BorderSize = 0;
            this.boxFillBG.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxFillBG.Location = new System.Drawing.Point(64, 5);
            this.boxFillBG.Name = "boxFillBG";
            this.boxFillBG.Size = new System.Drawing.Size(20, 19);
            this.boxFillBG.TabIndex = 0;
            this.boxFillBG.UseVisualStyleBackColor = false;
            this.boxFillBG.Click += new System.EventHandler(this.boxFillBG_Click);
            // 
            // boxFrontColor
            // 
            this.boxFrontColor.BackColor = System.Drawing.Color.White;
            this.boxFrontColor.FlatAppearance.BorderSize = 0;
            this.boxFrontColor.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White;
            this.boxFrontColor.FlatAppearance.MouseOverBackColor = System.Drawing.Color.White;
            this.boxFrontColor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxFrontColor.Location = new System.Drawing.Point(5, 3);
            this.boxFrontColor.Name = "boxFrontColor";
            this.boxFrontColor.Size = new System.Drawing.Size(16, 16);
            this.boxFrontColor.TabIndex = 25;
            this.boxFrontColor.UseVisualStyleBackColor = false;
            this.boxFrontColor.Click += new System.EventHandler(this.boxFrontColor_Click);
            // 
            // boxDummyButton
            // 
            this.boxDummyButton.BackColor = System.Drawing.Color.Black;
            this.boxDummyButton.Enabled = false;
            this.boxDummyButton.FlatAppearance.BorderSize = 0;
            this.boxDummyButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Black;
            this.boxDummyButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Black;
            this.boxDummyButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxDummyButton.Location = new System.Drawing.Point(4, 2);
            this.boxDummyButton.Name = "boxDummyButton";
            this.boxDummyButton.Size = new System.Drawing.Size(18, 17);
            this.boxDummyButton.TabIndex = 31;
            this.boxDummyButton.UseVisualStyleBackColor = false;
            // 
            // boxBgColor
            // 
            this.boxBgColor.BackColor = System.Drawing.Color.Black;
            this.boxBgColor.FlatAppearance.BorderSize = 0;
            this.boxBgColor.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Black;
            this.boxBgColor.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Black;
            this.boxBgColor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boxBgColor.Location = new System.Drawing.Point(13, 11);
            this.boxBgColor.Name = "boxBgColor";
            this.boxBgColor.Size = new System.Drawing.Size(17, 17);
            this.boxBgColor.TabIndex = 26;
            this.boxBgColor.UseVisualStyleBackColor = false;
            this.boxBgColor.Click += new System.EventHandler(this.boxBgColor_Click);
            // 
            // boxTableLayout
            // 
            this.boxTableLayout.ColumnCount = 8;
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 98F));
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 31F));
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 173F));
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 108F));
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 58F));
            this.boxTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 186F));
            this.boxTableLayout.Controls.Add(this.boxCanvasHolder, 0, 0);
            this.boxTableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boxTableLayout.Location = new System.Drawing.Point(0, 0);
            this.boxTableLayout.Margin = new System.Windows.Forms.Padding(0);
            this.boxTableLayout.Name = "boxTableLayout";
            this.boxTableLayout.RowCount = 1;
            this.boxTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.boxTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 206F));
            this.boxTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 206F));
            this.boxTableLayout.Size = new System.Drawing.Size(872, 206);
            this.boxTableLayout.TabIndex = 0;
            // 
            // boxCanvasHolder
            // 
            this.boxTableLayout.SetColumnSpan(this.boxCanvasHolder, 8);
            this.boxCanvasHolder.Controls.Add(this.boxSplit);
            this.boxCanvasHolder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boxCanvasHolder.Location = new System.Drawing.Point(0, 0);
            this.boxCanvasHolder.Margin = new System.Windows.Forms.Padding(0);
            this.boxCanvasHolder.Name = "boxCanvasHolder";
            this.boxCanvasHolder.Size = new System.Drawing.Size(872, 206);
            this.boxCanvasHolder.TabIndex = 1;
            // 
            // boxSplit
            // 
            this.boxSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boxSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.boxSplit.Location = new System.Drawing.Point(0, 0);
            this.boxSplit.Margin = new System.Windows.Forms.Padding(0);
            this.boxSplit.Name = "boxSplit";
            // 
            // boxSplit.Panel1
            // 
            this.boxSplit.Panel1.Controls.Add(this.boxCanvas);
            this.boxSplit.Panel2MinSize = 0;
            this.boxSplit.Size = new System.Drawing.Size(872, 206);
            this.boxSplit.SplitterDistance = 820;
            this.boxSplit.TabIndex = 17;
            // 
            // boxCanvas
            // 
            this.boxCanvas.BackColor = System.Drawing.Color.Transparent;
            this.boxCanvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boxCanvas.Location = new System.Drawing.Point(0, 0);
            this.boxCanvas.Margin = new System.Windows.Forms.Padding(0);
            this.boxCanvas.Name = "boxCanvas";
            this.boxCanvas.Size = new System.Drawing.Size(820, 206);
            this.boxCanvas.TabIndex = 16;
            this.boxCanvas.MouseDown += new System.Windows.Forms.MouseEventHandler(this.boxCanvas_MouseDown);
            this.boxCanvas.MouseMove += new System.Windows.Forms.MouseEventHandler(this.boxCanvas_MouseMove);
            this.boxCanvas.MouseUp += new System.Windows.Forms.MouseEventHandler(this.boxCanvas_MouseUp);
            // 
            // DrawControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MinimumSize = new System.Drawing.Size(500, 200);
            this.Name = "DrawControl";
            this.Size = new System.Drawing.Size(872, 241);
            this.Load += new System.EventHandler(this.DrawControl_Load);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.boxWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxPointSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boxHeight)).EndInit();
            this.boxTableLayout.ResumeLayout(false);
            this.boxCanvasHolder.ResumeLayout(false);
            this.boxSplit.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.boxSplit)).EndInit();
            this.boxSplit.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Button boxFrontColor;
        private System.Windows.Forms.Button boxBgColor;
        private System.Windows.Forms.Button boxFillBG;
        private System.Windows.Forms.Button boxTextButton;
        private System.Windows.Forms.TableLayoutPanel boxTableLayout;
        private System.Windows.Forms.Panel boxCanvasHolder;
        private System.Windows.Forms.SplitContainer boxSplit;
        private PixelCanvas boxCanvas;
        private System.Windows.Forms.Button boxSelect;
        private System.Windows.Forms.Button boxBmpImport;
        private System.Windows.Forms.NumericUpDown boxHeight;
        private System.Windows.Forms.Button boxDummyButton;
        private System.Windows.Forms.Label boxLImageLinesCountLabel;
        private System.Windows.Forms.Label boxScaleLabel;
        private System.Windows.Forms.NumericUpDown boxPointSize;
        private System.Windows.Forms.Label boxLabelWidth;
        private System.Windows.Forms.NumericUpDown boxWidth;
        private System.Windows.Forms.Button boxPipette;
        private System.Windows.Forms.ToolTip boxPipetteTT;
    }
}
