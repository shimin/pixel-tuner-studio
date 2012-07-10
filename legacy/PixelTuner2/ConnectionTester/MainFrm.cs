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

namespace ConnectionTester
{
    public partial class MainFrm : Form
    {
        PixelDevice _device = null;

        public MainFrm()
        {
            Settings.Default.Reload();
            InitializeComponent();
            boxPortNum.Value = Settings.Default.port;
            boxPathFW.Text = Settings.Default.pathFW;
            boxPathProject.Text = Settings.Default.pathProj;
        }

        private void boxConnect_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            SetStatus("Открытие порта");
            SerialPort port = new SerialPort("COM" + boxPortNum.Value.ToString(), 19200, Parity.None, 8, StopBits.One);
            port.ReadTimeout = 500;
            port.WriteTimeout = 250;
            try
            {
                port.Open();
                _device = new PixelDevice(port, SetStatus);
                SetStatus("Порт открыт");
            }
            catch
            {
                SetStatus("Ошибка открытия порта " + port.PortName);
                return;
            }

            try
            {
                string id = _device.GetName();
                SetStatus("Ответ устройства: " + id);
                boxStatus.Text = "Готов";
            }
            catch
            {
                SetStatus("Ошибка чтения" + port.PortName);
            }
        }

        private void SetStatus(string p)
        {
            boxStatus.Text = p;
            if (!boxLog.InvokeRequired)
            {
                boxLog.Text += DateTime.Now.ToString("HH:mm:ss.ffff") + " " + p + "\n";
            }
            else
            {
                this.Invoke((MethodInvoker)delegate
                {
                    boxLog.Text += DateTime.Now.ToString("HH:mm:ss.ffff") + " " + p + "\n";
                });
            }
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
                    SetStatus("Порт закрыт.");
                }
                catch { }
            }
        }

        private void boxStateQuery_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device == null)
            {
                SetStatus("Не подключено.");
                return;
            }

            object pixelState = _device.GetState();
        }

        private void boxClear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            boxLog.Clear();
        }

        private void boxLoadProj_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device == null)
            {
                SetStatus("Не подключено.");
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
                SetStatus(ex.Message);
            }
        }

        private void boxLoadFW_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_device == null)
            {
                SetStatus("Не подключено.");
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
                SetStatus(ex.Message);
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
