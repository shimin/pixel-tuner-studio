using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PixelTuner.Service;
using System.IO.Ports;
using ConnectionTester.Properties;
using System.IO;
using Communication.Service;

namespace ConnectionTester
{
    public partial class MainFrm : Form
    {
        PixelDevice _device = null;

        LoggerRichTextBox _logTarget = null;

        public MainFrm()
        {
            Settings.Default.Reload();
            InitializeComponent();
            _logTarget = new LoggerRichTextBox(boxLog);
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
                _device = new PixelDevice(port);
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
                _device.WriteFile(binaryData);
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

        private void boxChoosePath_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = Application.StartupPath;
            ofd.Filter = "binary files|*.bin";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                boxPathFW.Text = ofd.FileName;
            }
        }
    }
}
