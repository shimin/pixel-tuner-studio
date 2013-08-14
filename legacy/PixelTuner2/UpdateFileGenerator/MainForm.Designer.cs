namespace UpdateFileGenerator
{
    partial class MainForm
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
            this.boxBaseDir = new System.Windows.Forms.Label();
            this.bpxChooseDir = new System.Windows.Forms.LinkLabel();
            this.boxBaseUrl = new System.Windows.Forms.TextBox();
            this.boxGenerate = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // boxBaseDir
            // 
            this.boxBaseDir.AutoSize = true;
            this.boxBaseDir.Location = new System.Drawing.Point(63, 9);
            this.boxBaseDir.Name = "boxBaseDir";
            this.boxBaseDir.Size = new System.Drawing.Size(0, 13);
            this.boxBaseDir.TabIndex = 1;
            // 
            // bpxChooseDir
            // 
            this.bpxChooseDir.AutoSize = true;
            this.bpxChooseDir.Location = new System.Drawing.Point(8, 9);
            this.bpxChooseDir.Name = "bpxChooseDir";
            this.bpxChooseDir.Size = new System.Drawing.Size(51, 13);
            this.bpxChooseDir.TabIndex = 2;
            this.bpxChooseDir.TabStop = true;
            this.bpxChooseDir.Text = "Каталог:";
            this.bpxChooseDir.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.bpxChooseDir_LinkClicked);
            // 
            // boxBaseUrl
            // 
            this.boxBaseUrl.Location = new System.Drawing.Point(11, 25);
            this.boxBaseUrl.Name = "boxBaseUrl";
            this.boxBaseUrl.Size = new System.Drawing.Size(553, 20);
            this.boxBaseUrl.TabIndex = 3;
            this.boxBaseUrl.TextChanged += new System.EventHandler(this.boxBaseUrl_TextChanged);
            // 
            // boxGenerate
            // 
            this.boxGenerate.AutoSize = true;
            this.boxGenerate.Location = new System.Drawing.Point(12, 48);
            this.boxGenerate.Name = "boxGenerate";
            this.boxGenerate.Size = new System.Drawing.Size(78, 13);
            this.boxGenerate.TabIndex = 4;
            this.boxGenerate.TabStop = true;
            this.boxGenerate.Text = "Генерировать";
            this.boxGenerate.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.boxGenerate_LinkClicked);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(576, 342);
            this.Controls.Add(this.boxGenerate);
            this.Controls.Add(this.boxBaseUrl);
            this.Controls.Add(this.bpxChooseDir);
            this.Controls.Add(this.boxBaseDir);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MainForm";
            this.Text = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label boxBaseDir;
        private System.Windows.Forms.LinkLabel bpxChooseDir;
        private System.Windows.Forms.TextBox boxBaseUrl;
        private System.Windows.Forms.LinkLabel boxGenerate;
    }
}

