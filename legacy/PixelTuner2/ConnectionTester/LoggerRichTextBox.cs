using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ConnectionTester
{
    public class LoggerRichTextBox : Common.Log.TargetBase
    {
        RichTextBox boxLog = null;

        public LoggerRichTextBox(RichTextBox control)
        {
            boxLog = control;
        }

        public override void NewMessage(string message, Common.Log.MessageType messageType, string component, int depth)
        {
            var formattedMessage = String.Format("{3}\t[{0}]\t{4}\t{2}{1}"
                , DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff")
                , message
                , new string('\t', depth)
                , _messageTypes[messageType]
                , component);

            if (!boxLog.InvokeRequired)
            {
                boxLog.Text += formattedMessage + "\n";
                boxLog.SelectionStart = boxLog.Text.Length;
                boxLog.ScrollToCaret();
            }
            else
            {
                boxLog.Invoke((MethodInvoker) delegate
                {
                    boxLog.Text += formattedMessage + "\n";
                    boxLog.SelectionStart = boxLog.Text.Length;
                    boxLog.ScrollToCaret();
                });
            }
        }
    }
}
