using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;

namespace PixelTuner.Service
{
    enum Command
    {
        WHO_IS_IT = 0x53,
        MK_STAT = 0x69,
        VERSION_BOOT = 0x56,
        VERSION_FW = 0x76,
        WRITE_IFLASH_MODE = 0x46,
        WRITE_IDATA_MODE = 0x66,
        WRITE_BLOCK = 0x42,
        END_WRITE_FLASH = 0x43,
        END_WRITE_DATA = 0x63,
    }

    public delegate void StatusNotifierDelegate (string status);

    public class PixelDevice
    {
        SerialPort _port = null;
        StatusNotifierDelegate statusSetter = null;

        public SerialPort Port
        {
            get { return _port; }
            set { _port = value; }
        }

        int _writeReadDelay = 100;
        /// <summary>
        /// Задержка в мсиллисекундах между операцей записи и чтения.
        /// </summary>
        public int WriteReadDelay
        {
            get { return _writeReadDelay; }
            set { _writeReadDelay = value; }
        }

        string _name = "";

        public string Name
        {
            get { return _name; }
        }

        public PixelDevice(SerialPort port)
        {
            _port = port;
        }

        public PixelDevice(SerialPort port, StatusNotifierDelegate statusSetter) 
            : this(port)
        {
            this.statusSetter = statusSetter;
        }

        #region Commands

        public void Disconnect()
        {
            if (_port != null)
            {
                try
                {
                    _port.Close();
                    _port = null;
                }
                catch { }
            }
        }

        public string GetName()
        {
            byte[] q = GetCommandBytes(Command.WHO_IS_IT).ToArray();
            string result = "";
            result = Encoding.ASCII.GetString(DoSession(q));
            if (statusSetter != null) statusSetter.Invoke("Строка: " + result);
            _name = PixelSpecs.TryGetName(result);
            return result;
        }
        
        public object GetState()
        {
            byte[] q = GetCommandBytes(Command.MK_STAT).ToArray();
            DoSession(q);

            return null;
        }

        public void WriteFile(List<byte> binaryData)
        {
            List<byte> buffer = new List<byte>();

            // переход в режим записи
            DoSession(GetCommandBytes(Command.WRITE_IDATA_MODE, buffer).ToArray());

            foreach (byte b in binaryData)
            {
                buffer.Add(b);
                if (buffer.Count == 61)
                {
                    DoSession(GetCommandBytes(Command.WRITE_BLOCK, buffer).ToArray());
                }
            }
            if (buffer.Count > 0)
            {
                DoSession(GetCommandBytes(Command.WRITE_BLOCK, buffer).ToArray());
            }

            // выход из режима записи
            DoSession(GetCommandBytes(Command.END_WRITE_DATA, buffer).ToArray());
        }

        /// <summary>
        /// Возвращает ответ устройства на пользовательский запрос.
        /// </summary>
        /// <param name="query">Запрос</param>
        /// <exception cref="InvalidOperationException">Бросается если не совпал номер команды, пришедший в ответе.</exception>
        /// <returns>Ответ</returns>
        private byte[] DoSession(byte[] query)
        {
            if (statusSetter != null) statusSetter.Invoke("Запись: " + BytesToString(query));
            _port.Write(query, 0, query.Length);

            Thread.Sleep(WriteReadDelay);

            if (!WaitBytes(1, statusSetter)) return new byte[] { };            
            int ans = _port.ReadByte();

            if (statusSetter != null) statusSetter.Invoke("Чтение: " + ans.ToString("X2"));
            if (ans == (int)query[0])   // если ответ на ту же команду
            {
                if (!WaitBytes(1, statusSetter)) return new byte[] { };
                int len = _port.ReadByte();

                byte[] receiveBuf = new byte[len];
                int bytesGot = _port.Read(receiveBuf, 0, len);

                if (!WaitBytes(1, statusSetter)) return receiveBuf;
                int dSymbol = _port.ReadByte();

                if (statusSetter != null) statusSetter.Invoke("Чтение: " + len.ToString("X2") + BytesToString(receiveBuf));
                return receiveBuf;
            }
            throw new InvalidOperationException(String.Format(
                "Неверный ответ '{0}' на команду '{1}'", ans.ToString(), query[0])
            );
        }

        private List<byte> GetCommandBytes(Command command, List<byte> args = null)
        {
            List<byte> result = new List<byte>() { (byte)command };
            switch (command)
            {
                case Command.WHO_IS_IT: result.Add(0); break;
                case Command.MK_STAT: result.Add(0); break;
                case Command.WRITE_IDATA_MODE: result.Add(0); break;
                case Command.WRITE_IFLASH_MODE: result.Add(0); break;
                case Command.END_WRITE_DATA: result.Add(0); break;
                case Command.END_WRITE_FLASH: result.Add(0); break;

                case Command.WRITE_BLOCK: 
                    if (args == null) 
                        throw new ArgumentNullException("Не задан отправляемый набор байт", "args");
                    if (args.Count > 61)
                        throw new ArgumentOutOfRangeException("Длина пакета превышает допустимую (61)", "args");
                    result.Add((byte)args.Count);
                    result.AddRange(args);
                    break;
                default: break;
            }
            result.Add(0x0D);
            return result;
        }

        #endregion

        #region Misc

        private bool WaitBytes(int p, StatusNotifierDelegate statusSetter = null)
        {
            try
            {
                PortHelper.WaitForBytes(_port, 1, _port.ReadTimeout);
                return true;
            }
            catch
            {
                if (statusSetter != null) statusSetter.Invoke("Таймаут чтения.");
            }
            return false;
        }

        private string BytesToString(byte[] buf)
        {
            string result = "";
            foreach (byte b in buf) result += b.ToString("X2") + " ";
            return result;
        }

        private string BytesToString(List<byte> buf)
        {
            string result = "";
            foreach (byte b in buf) result += b.ToString("X2") + " ";
            return result;
        }

        public override string ToString()
        {
            return this.Name;
            //return _port != null ? this.Port.PortName : "";
        }

        public override bool Equals(object obj)
        {
            if (obj is PixelDevice)
                if ((obj as PixelDevice)._port == this._port) return true;

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }
}
