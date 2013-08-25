using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Common.Log
{
    public class FileTarget : TargetBase
    {
        private readonly string LogFileName;
        private long maxLogFileSize;
        private const int MaxLogFileSizeDefault = 10485760;
        private Encoding enc = Encoding.GetEncoding(1251);

        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="fileName">Имя файла, если имя неполное, то к файл будет располагаться в директории запуска</param>
        /// <param name="maxLogFileSize">Максимальный размер файла протокола</param>
        public FileTarget(string fileName, long maxLogFileSize = MaxLogFileSizeDefault)
        {
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".\\Logs");
            Directory.CreateDirectory(logFolder);
            LogFileName = Path.Combine(logFolder, fileName);
            this.maxLogFileSize = maxLogFileSize;
        }

        public override void NewMessage(string message, MessageType messageType, string component, int depth)
        {
            FileInfo info = new FileInfo(LogFileName);
            bool append = !info.Exists || info.Length < maxLogFileSize;

            using (StreamWriter writer = new System.IO.StreamWriter(LogFileName, append, enc))
            {
                writer.WriteLine
                    ("{3}\t[{0}]\t{4}\t{2}{1}"
                    , DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff")
                    , message
                    , new string('\t', depth)
                    , _messageTypes[messageType]
                    , component
                    );
            }
        }
    }
}
