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
using LibHID;
using System.Threading;
using System.Text.RegularExpressions;

namespace ConnectionTester
{
    public partial class MainFrm : Form
    {
        PixelDeviceTest _device = null;

        USBInterface _usbDevice = null;

        Common.Log.TargetBase _logTarget = null;
        Common.Log.TargetBase _logTargetHID = null;
        Common.Log.Logger _logger = null;

        List<string> requestsSource = new List<string>();

        public MainFrm()
        {
            Settings.Default.Reload();
            foreach (var s in Settings.Default.requests) requestsSource.Add(s);

            InitializeComponent();
            boxLogToFile.Checked = Settings.Default.logToFile;
            boxRequest.DataSource = requestsSource;
            boxActualColor.BackColor = boxSampleColor.BackColor = Color.Black;

            boxVendorID.Text = Settings.Default.vendorID;
            boxProductID.Text = Settings.Default.productID;
            boxChooseFileToUploadHID.Text = string.IsNullOrEmpty(Settings.Default.fileToUploadHID) ? boxChooseFileToUploadHID.Text : Settings.Default.fileToUploadHID;
            boxLogToFileHID.Checked = Settings.Default.logToFileHID;
            boxFilterNull.Checked = Settings.Default.filterNull;

            if (boxLogToFile.Checked)
            {
                _logTarget = new Common.Log.FileTarget("connection.log");
                _logTargetHID = new Common.Log.FileTarget("connectionHID.log");
            }
            else
            {
                _logTarget = new LoggerRichTextBox(boxLog);
                _logTargetHID = new LoggerRichTextBox(boxLogHID);
            }
            boxRequest.SelectedIndexChanged += boxRequest_SelectedIndexChanged;
            boxPortNum.Value = Settings.Default.port;
            boxPathFW.Text = Settings.Default.pathFW;
            boxPathProject.Text = Settings.Default.pathProj;
        }
        
        private void boxConnect_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _logger = new Common.Log.Logger(_logTarget);
            SetStatus("Открытие порта", Common.Log.MessageType.Info, 0);
            SerialPort port = new SerialPort("COM" + boxPortNum.Value.ToString(), 19200, Parity.None, 8, StopBits.One);
            port.ReadTimeout = 4000;
            port.WriteTimeout = 1000;
            try
            {
                port.Open();
                _device = new PixelDeviceTest(port);
                _device.Logger = new Common.Log.Logger(_logTarget);
                _device.SessionFinished += new SerialDeviceBase.SessionDelegate(_device_SessionFinished);
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

        List<byte> receivedData = new List<byte>();

        void _device_SessionFinished(DeviceSessionEventArgs e)
        {
            receivedData.AddRange(e.Data);
        }

        private void SetStatus(string p, Common.Log.MessageType messageType, int depth)
        {
            if (_logger == null) _logger = new Common.Log.Logger(_logTargetHID);
            _logger.NewMessage(p, messageType, null, 0);
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


            Settings.Default.logToFileHID = boxLogToFileHID.Checked;
            Settings.Default.fileToUploadHID = boxChooseFileToUploadHID.Text;
            Settings.Default.productID = boxProductID.Text;
            Settings.Default.vendorID = boxVendorID.Text;
            Settings.Default.filterNull = boxFilterNull.Checked;
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

            _device.GetInfo();
        }

        private void boxClear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            boxLog.Clear();
            receivedData.Clear();
        }

        private void boxLoadProj_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device == null)
            {
                SetStatus("Не подключено.", Common.Log.MessageType.Alert, 0);
                return;
            }

            if (!File.Exists(boxPathProject.Text))
            {
                MessageBox.Show("Файл не существует");
                return;
            }

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
                _logger = new Common.Log.Logger(_logTarget);
            }
            else
            {
                _logTarget = new LoggerRichTextBox(boxLog);
                _logger = new Common.Log.Logger(_logTarget);
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

        #region HID

        private void boxConnectHID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (string.IsNullOrEmpty(boxVendorID.Text))
            {
                MessageBox.Show("VendorID должен быть не пустой.");
                return;
            }

            _logger = new Common.Log.Logger(_logTargetHID);
            try
            {
                if (string.IsNullOrEmpty(boxProductID.Text))
                {
                    _usbDevice = new USBInterface(boxVendorID.Text);
                }
                else
                {
                    _usbDevice = new USBInterface(boxVendorID.Text, boxProductID.Text);
                }
                _usbDevice.enableUsbBufferEvent(new EventHandler(BufferChangedHandler));
                _usbDevice.startRead();
                SetStatus("Подключено.", Common.Log.MessageType.Info, 0);
            }
            catch (Exception ex)
            {
                _logger.WriteException(ex);
            }
        }

        private void boxDisconnectHID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_usbDevice != null)
            {
                _usbDevice.stopRead();
                _usbDevice.Disconnect();
                _usbDevice = null;
                SetStatus("Отключено.", Common.Log.MessageType.Info, 0);
            }
        }

        private void BufferChangedHandler(object sender, EventArgs e)
        {
            byte[][] buf = (byte[][])USBInterface.usbBuffer.ToArray(typeof(byte[]));
            var lastBytes = buf.Last<byte[]>();

            if (boxFilterNull.Checked)
            {
                int checkSum = 0;
                for (int i = 0; i < lastBytes.Count<byte>(); i++)
                    checkSum += lastBytes[i];

                if (checkSum > 0)
                {
                    string bufStr = Common.Log.Logger.BytesToString(lastBytes);
                    SetStatus("Buffer: " + bufStr, Common.Log.MessageType.Debug, 0);
                }
            }
            else
            {
                string bufStr = Common.Log.Logger.BytesToString(lastBytes);
                SetStatus("Buffer: " + bufStr, Common.Log.MessageType.Debug, 0);
            }
        }

        private void boxGetDeviceListHID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_usbDevice != null)
            {
                List<string> answerList = new List<string>(_usbDevice.getDeviceList());
                answerList.ForEach((s) =>
                {
                    SetStatus(s, Common.Log.MessageType.Info, 0);
                });
            }
            else
            {
                SetStatus("Нет подключенных устройств.", Common.Log.MessageType.Alert, 0);
            }
        }

        #region LOG

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            boxLogHID.Clear();
            receivedData.Clear();
        }

        private void boxLogToFileHID_CheckedChanged(object sender, EventArgs e)
        {
            if (boxLogToFileHID.Checked)
            {
                _logTargetHID = new Common.Log.FileTarget("connectionHID.log");
                _logger = new Common.Log.Logger(_logTargetHID);
            }
            else
            {
                _logTarget = new LoggerRichTextBox(boxLog);
                _logger = new Common.Log.Logger(_logTargetHID);
            }
        }

        #endregion

        private void boxChooseFileToUploadHID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = Application.StartupPath;
            ofd.Filter = "binary files|*.bin";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                boxChooseFileToUploadHID.Text = ofd.FileName;
            }
        }

        BackgroundWorker bw = null;

        private void boxUploadFileHID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (File.Exists(boxChooseFileToUploadHID.Text))
            {
                if (_usbDevice == null)
                {
                    SetStatus("Не подключено устройство HID.", Common.Log.MessageType.Alert, 0);
                    return;
                }

                List<byte> binaryData = new List<byte>(File.ReadAllBytes(boxChooseFileToUploadHID.Text));

                try
                {
                    if (bw != null && bw.IsBusy)
                    {
                        SetStatus("Запись уже выполняется.", Common.Log.MessageType.Alert, 0);
                        return;
                    }

                    bw = new BackgroundWorker();
                    bw.DoWork += (obj, ea) =>
                    {
                        USBInterface.usbBuffer.Clear();
                        _usbDevice.write(binaryData.ToArray());
                    };
                    bw.RunWorkerAsync();
                }
                catch (Exception ex)
                {
                    _logger.WriteException(ex);
                }
            }
            else
            {
                SetStatus("Файл не существует.", Common.Log.MessageType.Alert, 0);
            }
        }

        #endregion

        private void boxSaveBIN_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //Regex hexRx = new Regex(@"[0-9a-fA-F]{2}\s");
            //StringBuilder sb = new StringBuilder();
            //var ms = hexRx.Matches(boxLog.Text);
            //List<byte> bin = new List<byte>();
            //foreach (var m in ms) {
            //    var s = m.ToString().Trim();
            //    sb.Append(s + " ");
            //    bin.Add(byte.Parse(s, System.Globalization.NumberStyles.HexNumber));
            //}
            var sfd = new SaveFileDialog();
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //File.WriteAllBytes(sfd.FileName, bin.ToArray());
                File.WriteAllBytes(sfd.FileName, receivedData.ToArray());
            }
        }

    }
}
