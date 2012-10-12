using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.Service
{
    using System;
    using System.Diagnostics;
    using System.IO.Ports;
    using System.Threading;

    public static class PortHelper
    {

        /// <summary>
        /// Wait for a specified number of bytes to arrive on the serial port, or until a timeout occurs.
        /// </summary>
        /// <param name="port">Serial port on which bytes are expected to arrive.</param>
        /// <param name="count">Number of bytes expected.</param>
        /// <param name="millisecondsTimeout">Maximum amount of time to wait.</param>
        /// <exception cref="TimeoutException">Thrown if <paramref name="count"/> bytes are not received
        /// within <paramref name="millisecondsTimeout"/> milliseconds.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="port"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if either <paramref name="count"/> or
        /// <paramref name="millisecondsTimeout"/> is less than zero.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the serial port is closed.</exception>
        /// <remarks>This extension method is intended only as an ad-hoc aid.  If you're using it a lot,
        /// then it's recommended for performance reasons to instead instantiate a
        /// <see cref="SerialPortWatcher"/> instance for the lifetime of your SerialPort.</remarks>
        public static void WaitForBytes(this SerialPort port, int count, int millisecondsTimeout)
        {
            lock(port)
            if (port == null) throw new ArgumentNullException("port");
            if (port.BytesToRead >= count) return;

            using (var watcher = new SerialPortWatcher(port))
            {
                watcher.WaitForBytes(count, millisecondsTimeout);
            }

        }

        /// <summary>
        /// Wait for a specified number of bytes to arrive on the serial port, or until a timeout occurs.
        /// </summary>
        /// <param name="port">Serial port on which bytes are expected to arrive.</param>
        /// <param name="count">Number of bytes expected.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="port"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if either <paramref name="count"/> or
        /// <paramref name="millisecondsTimeout"/> is less than zero.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the serial port is closed.</exception>
        /// <exception cref="TimeoutException">Thrown if <paramref name="count"/> bytes are not received
        /// within the number of milliseconds specified in the <see cref="SerialPort.ReadTimeout"/> property
        /// of <paramref name="port"/>.</exception>
        /// <remarks>This extension method is intended only as an ad-hoc aid.  If you're using it a lot,
        /// then it's recommended for performance reasons to instead instantiate a
        /// <see cref="SerialPortWatcher"/> instance for the lifetime of your SerialPort.</remarks>
        public static void WaitForBytes(this SerialPort port, int count)
        {
            if (port == null) throw new ArgumentNullException("port");
            WaitForBytes(port, count, port.ReadTimeout);
        }

    }

    /// <summary>
    /// Watches for incoming bytes on a serial port and provides a reliable method to wait for a given
    /// number of bytes in a synchronous communications algorithm.
    /// </summary>
    class SerialPortWatcher : IDisposable
    {

        // This class works primarilly by watching for the SerialPort.DataReceived event.  However, since
        // that event is not guaranteed to fire, it is neccessary to also periodically poll for new data.
        // The polling interval can be fine-tuned here.  A higher number means less wasted CPU time, while
        // a lower number decreases the maximum possible latency.
        private const int POLL_MS = 30;

        private AutoResetEvent dataArrived = new AutoResetEvent(false);
        private SerialPort port;

        public SerialPortWatcher(SerialPort port)
        {
            if (port == null) throw new ArgumentNullException("port");
            this.port = port;
            this.port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
        }

        public void Dispose()
        {
            if (port != null)
            {
                port.DataReceived -= port_DataReceived;
                port = null;
            }
            if (dataArrived != null)
            {
                dataArrived.Dispose();
                dataArrived = null;
            }
        }

        void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            // This event will occur on a secondary thread.  Signal the waiting thread (if any).
            // Note: This handler could fire even after we are disposed.

            // MSDN documentation describing DataReceived event:
            // http://msdn.microsoft.com/en-us/library/system.io.ports.serialport.datareceived.aspx

            // Links discussing thread safety and event handlers:
            // http://stackoverflow.com/questions/786383/c-events-and-thread-safety
            // http://www.codeproject.com/Articles/37474/Threadsafe-Events.aspx

            // Note that we do not actually check the SerialPort.BytesToRead property here as it
            // is not documented to be thread-safe.

            if (dataArrived != null) dataArrived.Set();

        }

        /// <summary>
        /// Blocks the current thread until the specified number of bytes have been received from the
        /// serial port, or until a timeout occurs.
        /// </summary>
        /// <param name="count">Number of bytes expected.</param>
        /// <param name="millisecondsTimeout">Maximum amount of time to wait.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if either <paramref name="count"/> or
        /// <paramref name="millisecondsTimeout"/> is less than zero.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the serial port is closed, or if this
        /// <see cref="SerialPortWatcher"/> instance has been disposed.</exception>
        /// <exception cref="TimeoutException">Thrown if <paramref name="count"/> bytes are not received
        /// within the number of milliseconds specified in the <see cref="SerialPort.ReadTimeout"/> property
        /// of <paramref name="port"/>.</exception>
        public void WaitForBytes(int count, int millisecondsTimeout)
        {

            if (count < 0) throw new ArgumentOutOfRangeException("count");
            if (millisecondsTimeout < 0) throw new ArgumentOutOfRangeException("millisecondsTimeout");
            if (port == null) throw new InvalidOperationException("SerialPortWatcher has been disposed.");
            if (!port.IsOpen) throw new InvalidOperationException("Port is closed");

            if (port.BytesToRead >= count) return;

            DateTime expire = DateTime.Now.AddMilliseconds(millisecondsTimeout);

            // Wait for the specified number of bytes to become available.  This is done primarily by
            // waiting for a signal from the thread which handles the DataReceived event.  However, since
            // that event isn't guaranteed to fire, we also poll for new data every POLL_MS milliseconds.
            while (port.BytesToRead < count)
            {
                if (DateTime.Now >= expire)
                {
                    throw new TimeoutException(String.Format(
                      "Timed out waiting for data from port {0}", port.PortName));
                }
                WaitForSignal();
            }

        }

        // Timeout exceptions are expected to be thrown in this block of code, and are perfectly normal.
        // A separate method is used so it can be marked with DebuggerNonUserCode, which will cause the
        // debugger to ignore these exceptions (even if Thrown is checkmarked under Debug | Exceptions).
        [DebuggerNonUserCode]
        private void WaitForSignal()
        {
            try
            {
                dataArrived.WaitOne(POLL_MS);
            }
            catch (TimeoutException) { }
        }

    }
}
