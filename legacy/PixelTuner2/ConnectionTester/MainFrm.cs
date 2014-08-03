using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using ConnectionTester.Properties;
using System.IO;
using Communication.Service;

namespace ConnectionTester
{
    public partial class MainFrm : Form
    {
        PixelDeviceTest _device = null;

        Common.Log.TargetBase _logTarget = null;

        List<string> requestsSource = new List<string>();

        public MainFrm()
        {
            Settings.Default.Reload();
            foreach (var s in Settings.Default.requests) requestsSource.Add(s);

            InitializeComponent();
            boxLogToFile.Checked = Settings.Default.logToFile;
            boxRequest.DataSource = requestsSource;
            boxActualColor.BackColor = boxSampleColor.BackColor = Color.Black;
            if (boxLogToFile.Checked)
            {
                _logTarget = new Common.Log.FileTarget("connection.log");
            }
            else
            {
                _logTarget = new LoggerRichTextBox(boxLog);
            }
            boxRequest.SelectedIndexChanged += boxRequest_SelectedIndexChanged;
            boxPortNum.Value = Settings.Default.port;
            boxPathFW.Text = Settings.Default.pathFW;
            boxPathProject.Text = Settings.Default.pathProj;
        }
        
        private void boxConnect_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            SetStatus("Открытие порта", Common.Log.MessageType.Info, 0);
            SerialPort port = new SerialPort("COM" + boxPortNum.Value.ToString(), 19200, Parity.None, 8, StopBits.One);
            port.ReadTimeout = 4000;
            port.WriteTimeout = 1000;
            try
            {
                port.Open();
                _device = new PixelDeviceTest(port);
                _device.Logger = new Common.Log.Logger(_logTarget);
                SetStatus("Порт открыт", Common.Log.MessageType.Info, 0);
            }
            catch
            {
                SetStatus("Ошибка открытия порта " + port.PortName, Common.Log.MessageType.Error, 0);
                return;
            }

            try
            {
                bool bootModeFlag;
                string id = _device.GetName(out bootModeFlag);
                SetStatus("Ответ устройства: " + id, Common.Log.MessageType.Info, 1);
                boxStatus.Text = "Готов";
            }
            catch
            {
                SetStatus("Ошибка чтения" + port.PortName, Common.Log.MessageType.Error, 0);
            }
        }

        private void SetStatus(string p, Common.Log.MessageType messageType, int depth)
        {
            _logTarget.NewMessage(p, messageType, null, 0);
        }

        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_device != null)
            {
                _device.Disconnect();
                _device = null;
            }

            Settings.Default.port = (int)boxPortNum.Value;
            Settings.Default.pathFW = boxPathFW.Text;
            Settings.Default.pathProj = boxPathProject.Text;
            Settings.Default.requests.Clear();
            Settings.Default.logToFile = boxLogToFile.Checked;
            foreach (string s in requestsSource)
            {
                Settings.Default.requests.Add(s);
            }
            Settings.Default.Save();
        }

        private void boxDisconnect_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device != null)
            {
                try
                {
                    _device.Disconnect();
                    _device = null;
                    SetStatus("Порт закрыт.", Common.Log.MessageType.Info, 0);
                }
                catch { }
            }
        }

        private void boxStateQuery_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device == null)
            {
                SetStatus("Не подключено.", Common.Log.MessageType.Alert, 0);
                return;
            }

            object pixelState = _device.GetInfo();
        }

        private void boxClear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            boxLog.Clear();
        }

        private void boxLoadProj_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device == null)
            {
                SetStatus("Не подключено.", Common.Log.MessageType.Alert, 0);
                return;
            }

            if (!File.Exists(boxPathProject.Text))
                MessageBox.Show("Файл не существует");

            List<byte> binaryData = new List<byte>(File.ReadAllBytes(boxPathProject.Text));

            try
            {
                if (boxRepeatCount.Value > 1)
                {
                    if (MessageBox.Show("Upload project " + boxRepeatCount.Value.ToString() + " times?", "PixelTuner", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                    {
                        for (int i = 0; i < boxRepeatCount.Value; i++)
                        {
                            if (_device.Port != null)
                                _device.WriteFile(binaryData);
                            else
                            {
                                _logTarget.NewMessage("Port is null, trying to make new.", Common.Log.MessageType.Alert, "ConnectionTester", 0);
                                try { boxConnect_LinkClicked(null, null); }
                                catch { }
                            }
                            System.Threading.Thread.Sleep((int)boxDelay.Value);
                        }
                    }
                }
                else
                {
                    _device.WriteFile(binaryData);
                }
            }
            catch(Exception ex)
            {
                SetStatus(ex.Message, Common.Log.MessageType.Error, 0);
            }
        }

        private void boxLoadFW_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device == null)
            {
                SetStatus("Не подключено.", Common.Log.MessageType.Alert, 0);
                return;
            }

            if (!File.Exists(boxPathFW.Text))
                MessageBox.Show("Файл не существует");

            List<byte> binaryData = new List<byte>(File.ReadAllBytes(boxPathFW.Text));

            try
            {
                _device.WriteFirmware(binaryData);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Common.Log.MessageType.Error, 0);
            }
        }

        private void boxChooseProj_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = Application.StartupPath;
            ofd.Filter = "binary files|*.bin";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                boxPathProject.Text = ofd.FileName;
            }
        }

        private void boxChooseFW_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = Application.StartupPath;
            ofd.Filter = "binary files|*.bin";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                boxPathFW.Text = ofd.FileName;
            }
        }

        private void boxSampleR_Scroll(object sender, EventArgs e)
        {
            int r = boxSampleR.Value;
            int g = boxSampleG.Value;
            int b = boxSampleB.Value;
            boxSampleRnum.ValueChanged -= this.boxSampleRnum_ValueChanged;
            boxSampleRnum.Value = boxSampleR.Value;
            boxSampleRnum.ValueChanged += new System.EventHandler(this.boxSampleRnum_ValueChanged);
            boxSampleColor.BackColor = Color.FromArgb(r, g, b);
        }

        private void boxSampleG_Scroll(object sender, EventArgs e)
        {
            int r = boxSampleR.Value;
            int g = boxSampleG.Value;
            int b = boxSampleB.Value;
            boxSampleGnum.ValueChanged -= this.boxSampleGnum_ValueChanged;
            boxSampleGnum.Value = boxSampleG.Value;
            boxSampleGnum.ValueChanged += new System.EventHandler(this.boxSampleGnum_ValueChanged);
            boxSampleColor.BackColor = Color.FromArgb(r, g, b);
        }

        private void boxSampleB_Scroll(object sender, EventArgs e)
        {
            int r = boxSampleR.Value;
            int g = boxSampleG.Value;
            int b = boxSampleB.Value;
            boxSampleBnum.ValueChanged -= this.boxSampleBnum_ValueChanged;
            boxSampleBnum.Value = boxSampleB.Value;
            boxSampleBnum.ValueChanged += new System.EventHandler(this.boxSampleBnum_ValueChanged);
            boxSampleColor.BackColor = Color.FromArgb(r, g, b);
        }

        private void boxActualR_Scroll(object sender, EventArgs e)
        {
            int r = boxActualR.Value;
            int g = boxActualG.Value;
            int b = boxActualB.Value;
            boxActualRnum.ValueChanged -= this.boxActualRnum_ValueChanged;
            boxActualRnum.Value = boxActualR.Value;
            boxActualRnum.ValueChanged += new System.EventHandler(this.boxActualRnum_ValueChanged);
            boxActualColor.BackColor = Color.FromArgb(r, g, b);
            if (_device != null)
            {
                _device.SetColor(boxActualColor.BackColor, (byte)boxPixelNumber.Value);
            }
        }

        private void boxActualG_Scroll(object sender, EventArgs e)
        {
            int r = boxActualR.Value;
            int g = boxActualG.Value;
            int b = boxActualB.Value;
            boxActualGnum.ValueChanged -= this.boxActualGnum_ValueChanged;
            boxActualGnum.Value = boxActualG.Value;
            boxActualGnum.ValueChanged += new System.EventHandler(this.boxActualGnum_ValueChanged);
            boxActualColor.BackColor = Color.FromArgb(r, g, b);
            if (_device != null)
            {
                _device.SetColor(boxActualColor.BackColor, (byte)boxPixelNumber.Value);
            }
        }

        private void boxActualB_Scroll(object sender, EventArgs e)
        {
            int r = boxActualR.Value;
            int g = boxActualG.Value;
            int b = boxActualB.Value;
            boxActualBnum.ValueChanged -= this.boxActualBnum_ValueChanged;
            boxActualBnum.Value = boxActualB.Value;
            boxActualBnum.ValueChanged += new System.EventHandler(this.boxActualBnum_ValueChanged);
            boxActualColor.BackColor = Color.FromArgb(r, g, b);
            if (_device != null)
            {
                _device.SetColor(boxActualColor.BackColor, (byte)boxPixelNumber.Value);
            }
        }

        private void boxSampleRnum_ValueChanged(object sender, EventArgs e)
        {
            boxSampleR.Value = (int)boxSampleRnum.Value;
        }

        private void boxSampleGnum_ValueChanged(object sender, EventArgs e)
        {
            boxSampleG.Value = (int)boxSampleGnum.Value;
        }

        private void boxSampleBnum_ValueChanged(object sender, EventArgs e)
        {
            boxSampleB.Value = (int)boxSampleBnum.Value;
        }

        private void boxActualRnum_ValueChanged(object sender, EventArgs e)
        {
            boxActualR.Value = (int)boxActualRnum.Value;
        }

        private void boxActualGnum_ValueChanged(object sender, EventArgs e)
        {
            boxActualG.Value = (int)boxActualGnum.Value;
        }

        private void boxActualBnum_ValueChanged(object sender, EventArgs e)
        {
            boxActualB.Value = (int)boxActualBnum.Value;
        }

        private void boxSendRequest_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device != null && boxRequest.SelectedIndex != 0)
            {
                for (int i = 0; i < boxRepeatCount.Value; i++)
                {
                    List<byte> request = Common.Log.Logger.BytesFromString(boxRequest.Text);
                    try
                    {
                        _device.SendRequest(request);
                    }
                    catch (Exception ex)
                    {
                        _device.Logger.WriteException(ex);
                    }
                    System.Threading.Thread.Sleep((int)boxDelay.Value);
                }
            }
        }

        void boxRequest_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (boxRequest.SelectedIndex == 0)
            {
                var ofd = new OpenFileDialog();
                ofd.Filter = "binary files|*.bin";
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    boxRequest.SelectedIndexChanged -= boxRequest_SelectedIndexChanged;
                    var bs = File.ReadAllBytes(ofd.FileName);
                    boxRequest.DataSource = null;
                    requestsSource.Add(Common.Log.Logger.BytesToString(bs));
                    boxRequest.DataSource = requestsSource;
                    boxRequest.SelectedIndex = requestsSource.Count == 0 ? 0 : requestsSource.Count - 1;
                    boxRequest.SelectedIndexChanged += boxRequest_SelectedIndexChanged;
                }
            }
        }

        private void boxDeleteRequest_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (boxRequest.SelectedIndex != 0)
            {
                boxRequest.SelectedIndexChanged -= boxRequest_SelectedIndexChanged;
                int selected = boxRequest.SelectedIndex;
                boxRequest.DataSource = null;
                requestsSource.RemoveAt(selected);
                boxRequest.DataSource = requestsSource;
                boxRequest.SelectedIndexChanged += boxRequest_SelectedIndexChanged;
            }
        }

        private void boxLogToFile_CheckedChanged(object sender, EventArgs e)
        {
            if (boxLogToFile.Checked)
            {
                _logTarget = new Common.Log.FileTarget("connection.log");
            }
            else
            {
                _logTarget = new LoggerRichTextBox(boxLog);
            }
            if (_device != null)
                _device.Logger = new Common.Log.Logger(_logTarget);
        }

        private void boxEnterTest_Click(object sender, EventArgs e)
        {
            if (_device != null && _device.Port.IsOpen)
            {
                _device.EnterTestMode();
            }
            else
            {
                MessageBox.Show("Нет связи");
            }
        }

        private void boxExitTest_Click(object sender, EventArgs e)
        {
            if (_device != null && _device.Port.IsOpen)
            {
                _device.ExitTestMode();
            }
            else
            {
                MessageBox.Show("Нет связи");
            }
        }

        private void boxSetPixel_Click(object sender, EventArgs e)
        {
            if (_device != null && _device.Port.IsOpen)
            {
                _device.SetPixelColor(boxPixelNumber.Value, boxSampleColor.BackColor);
            }
            else
            {
                MessageBox.Show("Нет связи");
            }
        }
    }
}
