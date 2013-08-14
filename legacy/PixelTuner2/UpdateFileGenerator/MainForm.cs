using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UpdateFileGenerator.Properties;
using System.IO;
using System.Diagnostics;

namespace UpdateFileGenerator
{
    public partial class MainForm : Form
    {
        string baseUrl = "";
        string baseDir = "";

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            boxBaseUrl.Text = baseUrl = Settings.Default.baseUrl;
            boxBaseDir.Text = baseDir = Settings.Default.baseDir;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Default.baseDir = baseDir;
            Settings.Default.baseUrl = baseUrl;
            Settings.Default.Save();
        }

        private void bpxChooseDir_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ofd = new FolderBrowserDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                boxBaseDir.Text = baseDir = ofd.SelectedPath;
            }
        }

        private void boxBaseUrl_TextChanged(object sender, EventArgs e)
        {
            baseUrl = boxBaseUrl.Text;
        }

        private void boxGenerate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string version = "";
            string result = Resources.Template;
            StringBuilder lines = new StringBuilder();
            if (!string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(baseDir))
            {
                List<string> fileNames = new List<string>();
                fileNames.AddRange(Directory.GetFiles(baseDir, "*.exe"));

                if (fileNames.Count > 0)
                {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(fileNames[0]);
                    version = versionInfo.ProductVersion;
                    fileNames.AddRange(Directory.GetFiles(baseDir, "*.dll", SearchOption.AllDirectories));
                    fileNames.AddRange(Directory.GetFiles(baseDir, "*.png", SearchOption.AllDirectories));
                    fileNames.AddRange(Directory.GetFiles(baseDir, "*.bmp", SearchOption.AllDirectories));

                    foreach (var n in fileNames)
                    {
                        FileInfo fi = new FileInfo(n);
                        string url = baseUrl.TrimEnd('\\', '/');
                        url += fi.FullName.Remove(0, baseDir.Length).Replace("\\", "/");

                        lines.AppendLine(string.Format(Resources.templateString,
                            Path.GetFileName(n),
                            url,
                            version,
                            fi.Length));
                    }

                    result = string.Format(Resources.Template, lines.ToString());
                    var sfd = new SaveFileDialog();
                    sfd.FileName = "autoupdateservice";
                    sfd.Filter = "(*.xml)|*.xml";
                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, result);
                    }
                }
            }
        }
    }
}
