using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using static Communication.Service.Win32SerialNativeMethods;

namespace Communication.Service
{
    /// <summary>
    /// Serial port transport built directly on the Win32 API, used instead of
    /// System.IO.Ports.SerialPort.
    /// </summary>
    /// <remarks>
    /// The port is configured exactly like SerialPort configures it (same DCB, DTR/RTS,
    /// timeouts and buffer sizes), so devices see no difference on the wire.
    ///
    /// Unlike SerialPort there is no background event thread. SerialPort's event thread
    /// (SerialStream.EventLoopRunner) crashes the whole process with ObjectDisposedException
    /// when the port handle is closed while it is running (USB unplug during Close, or the
    /// GC finalizing an abandoned port). Here every read and write waits for its own I/O to
    /// complete before returning, and nothing touches the handle in the background, so
    /// closing the port or unplugging the device can only fail the current call.
    ///
    /// Like SerialPort, <see cref="IsOpen"/> becomes false by itself when the device is
    /// unplugged: SerialPort learns it from its event thread, this class queries the driver
    /// (ClearCommError) and treats the same error codes as "device removed".
    /// </remarks>
    public sealed class Win32SerialPort : ITransport, IDisposable
    {
        public const int InfiniteTimeout = -1;

        // Same values as SerialPort's defaults.
        private const int ReadBufferSize = 4096;
        private const int WriteBufferSize = 2048;

        // SerialStream uses -2 as "infinite" read constant: SetCommTimeouts rejects -1.
        private const uint InfiniteReadTimeoutConstant = 0xFFFFFFFE;

        private static readonly int OverlappedSize = Marshal.SizeOf(typeof(NativeOverlapped));

        private readonly object _sync = new object();
        private readonly string _portName;
        private readonly int _baudRate;

        private SafeFileHandle _handle;
        private int _readTimeout = InfiniteTimeout;
        private int _writeTimeout = InfiniteTimeout;
        private Stream _stream;

        public Win32SerialPort(string portName, int baudRate)
        {
            if (portName == null)
                throw new ArgumentNullException("portName");
            if (baudRate <= 0)
                throw new ArgumentOutOfRangeException("baudRate");

            _portName = portName;
            _baudRate = baudRate;
        }

        public string PortName
        {
            get { return _portName; }
        }

        public string ChannelID
        {
            get { return _portName; }
        }

        /// <summary>
        /// True while the port is open and the device is present. Becomes false after
        /// <see cref="Close"/> or once the device has been unplugged.
        /// </summary>
        public bool IsOpen
        {
            get
            {
                SafeFileHandle handle;
                lock (_sync)
                {
                    handle = _handle;
                }
                if (handle == null) return false;

                try
                {
                    // ClearCommError is what SerialPort's event thread calls on every
                    // received byte, so drivers already expect it to be called often.
                    int errors;
                    if (!ClearCommError(handle, out errors, IntPtr.Zero))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (IsDeviceRemovedError(error))
                        {
                            MarkRemoved(handle);
                            return false;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }

                lock (_sync)
                {
                    return _handle != null;
                }
            }
        }

        public int ReadTimeout
        {
            get { return _readTimeout; }
            set
            {
                if (value < 0 && value != InfiniteTimeout)
                    throw new ArgumentOutOfRangeException("ReadTimeout", "Timeout must be greater than or equal to zero, or -1 (infinite).");

                SafeFileHandle handle = CurrentHandle();
                if (handle != null) ApplyTimeouts(handle, value, _writeTimeout);
                _readTimeout = value;
            }
        }

        public int WriteTimeout
        {
            get { return _writeTimeout; }
            set
            {
                if (value <= 0 && value != InfiniteTimeout)
                    throw new ArgumentOutOfRangeException("WriteTimeout", "Timeout must be greater than zero, or -1 (infinite).");

                SafeFileHandle handle = CurrentHandle();
                if (handle != null) ApplyTimeouts(handle, _readTimeout, value);
                _writeTimeout = value;
            }
        }

        public Stream BaseStream
        {
            get
            {
                lock (_sync)
                {
                    if (_stream == null) _stream = new PortStream(this);
                    return _stream;
                }
            }
        }

        #region Open / Close

        /// <summary>
        /// Opens the port with the same settings SerialPort.Open() applies by default:
        /// 8 data bits, no parity, one stop bit, no handshake, DTR and RTS off.
        /// </summary>
        public void Open()
        {
            lock (_sync)
            {
                if (_handle != null)
                    throw new InvalidOperationException("The port is already open.");
            }

            if (!_portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The given port name does not start with COM/com or does not resolve to a valid serial port.", "portName");

            SafeFileHandle handle = CreateFile("\\\\.\\" + _portName, GENERIC_READ | GENERIC_WRITE, 0,
                IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw CreateException(error);
            }

            try
            {
                int fileType = GetFileType(handle);
                if (fileType != FILE_TYPE_CHAR && fileType != FILE_TYPE_UNKNOWN)
                    throw new ArgumentException("The given port name does not resolve to a valid serial port.", "portName");

                var commProp = new COMMPROP();
                int modemStatus;
                if (!GetCommProperties(handle, ref commProp) || !GetCommModemStatus(handle, out modemStatus))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == ERROR_INVALID_PARAMETER || error == ERROR_INVALID_HANDLE)
                        throw new ArgumentException("The given port name is invalid. It may be a valid port, but not a serial port.", "portName");
                    throw CreateException(error);
                }
                if (commProp.dwMaxBaud != 0 && (uint)_baudRate > commProp.dwMaxBaud)
                    throw new ArgumentOutOfRangeException("baudRate", "The maximum baud rate for the device is " + commProp.dwMaxBaud + ".");

                InitializeDcb(handle, commProp);
                ApplyTimeouts(handle, _readTimeout, _writeTimeout);

                if (!SetupComm(handle, ReadBufferSize, WriteBufferSize))
                    throw CreateException(Marshal.GetLastWin32Error());
            }
            catch
            {
                handle.Dispose();
                throw;
            }

            lock (_sync)
            {
                _handle = handle;
            }
        }

        /// <summary>
        /// Same sequence as SerialPort.Close(): drop DTR, flush, purge, close the handle.
        /// Never throws: if the device is already gone only the handle is released.
        /// </summary>
        public void Close()
        {
            SafeFileHandle handle;
            lock (_sync)
            {
                handle = _handle;
                _handle = null;
            }
            if (handle == null) return;

            try
            {
                if (EscapeCommFunction(handle, CLRDTR))
                {
                    FlushFileBuffers(handle);
                    PurgeComm(handle, PURGE_RXCLEAR | PURGE_RXABORT);
                    PurgeComm(handle, PURGE_TXCLEAR | PURGE_TXABORT);
                }
            }
            catch (ObjectDisposedException)
            {
            }

            ReleaseHandle(handle);
        }

        public void Dispose()
        {
            Close();
        }

        // DCB setup copied from SerialStream (constructor + InitializeDCB + DtrEnable/RtsEnable
        // setters) for the settings SerialPort uses by default.
        private void InitializeDcb(SafeFileHandle handle, COMMPROP commProp)
        {
            var dcb = new DCB();
            if (!GetCommState(handle, ref dcb))
                throw CreateException(Marshal.GetLastWin32Error());

            dcb.DCBlength = (uint)Marshal.SizeOf(typeof(DCB));
            dcb.BaudRate = (uint)_baudRate;
            dcb.ByteSize = 8;
            dcb.StopBits = ONESTOPBIT;
            dcb.Parity = NOPARITY;

            SetDcbFlag(ref dcb, FPARITY, 0);
            SetDcbFlag(ref dcb, FBINARY, 1);
            SetDcbFlag(ref dcb, FOUTXCTSFLOW, 0);
            SetDcbFlag(ref dcb, FOUTXDSRFLOW, 0);
            SetDcbFlag(ref dcb, FDTRCONTROL, DTR_CONTROL_DISABLE);
            SetDcbFlag(ref dcb, FDSRSENSITIVITY, 0);
            SetDcbFlag(ref dcb, FINX, 0);
            SetDcbFlag(ref dcb, FOUTX, 0);
            SetDcbFlag(ref dcb, FERRORCHAR, 0);
            dcb.ErrorChar = 0;
            SetDcbFlag(ref dcb, FNULL, 0);
            if (GetDcbFlag(dcb, FRTSCONTROL) == RTS_CONTROL_HANDSHAKE)
                SetDcbFlag(ref dcb, FRTSCONTROL, RTS_CONTROL_DISABLE);

            dcb.XonChar = DEFAULTXONCHAR;
            dcb.XoffChar = DEFAULTXOFFCHAR;
            dcb.XonLim = dcb.XoffLim = (ushort)(commProp.dwCurrentRxQueue / 4);
            dcb.EofChar = EOFCHAR;
            dcb.EvtChar = EOFCHAR;

            if (!SetCommState(handle, ref dcb))
                throw CreateException(Marshal.GetLastWin32Error());

            // DtrEnable = false
            SetDcbFlag(ref dcb, FDTRCONTROL, DTR_CONTROL_DISABLE);
            if (!SetCommState(handle, ref dcb))
                throw CreateException(Marshal.GetLastWin32Error());
            if (!EscapeCommFunction(handle, CLRDTR))
                throw CreateException(Marshal.GetLastWin32Error());

            // RtsEnable = false. SerialPort only touches RTS when the driver had it enabled.
            if (GetDcbFlag(dcb, FRTSCONTROL) == RTS_CONTROL_ENABLE)
            {
                SetDcbFlag(ref dcb, FRTSCONTROL, RTS_CONTROL_DISABLE);
                if (!SetCommState(handle, ref dcb))
                    throw CreateException(Marshal.GetLastWin32Error());
                if (!EscapeCommFunction(handle, CLRRTS))
                    throw CreateException(Marshal.GetLastWin32Error());
            }
        }

        private static int GetDcbFlag(DCB dcb, int whichFlag)
        {
            uint mask = (whichFlag == FDTRCONTROL || whichFlag == FRTSCONTROL) ? 0x3u : 0x1u;
            return (int)((dcb.Flags >> whichFlag) & mask);
        }

        private static void SetDcbFlag(ref DCB dcb, int whichFlag, int setting)
        {
            uint mask = (whichFlag == FDTRCONTROL || whichFlag == FRTSCONTROL) ? 0x3u : 0x1u;
            dcb.Flags &= ~(mask << whichFlag);
            dcb.Flags |= ((uint)setting & mask) << whichFlag;
        }

        // Same COMMTIMEOUTS as SerialStream: a read returns as soon as at least one byte
        // is available, or with 0 bytes after ReadTimeout.
        private void ApplyTimeouts(SafeFileHandle handle, int readTimeout, int writeTimeout)
        {
            var timeouts = new COMMTIMEOUTS();
            if (readTimeout == 0)
            {
                timeouts.ReadIntervalTimeout = MAXDWORD;
                timeouts.ReadTotalTimeoutMultiplier = 0;
                timeouts.ReadTotalTimeoutConstant = 0;
            }
            else if (readTimeout == InfiniteTimeout)
            {
                timeouts.ReadIntervalTimeout = MAXDWORD;
                timeouts.ReadTotalTimeoutMultiplier = MAXDWORD;
                timeouts.ReadTotalTimeoutConstant = InfiniteReadTimeoutConstant;
            }
            else
            {
                timeouts.ReadIntervalTimeout = MAXDWORD;
                timeouts.ReadTotalTimeoutMultiplier = MAXDWORD;
                timeouts.ReadTotalTimeoutConstant = (uint)readTimeout;
            }
            timeouts.WriteTotalTimeoutMultiplier = 0;
            timeouts.WriteTotalTimeoutConstant = (writeTimeout == InfiniteTimeout) ? 0 : (uint)writeTimeout;

            bool ok;
            try
            {
                ok = SetCommTimeouts(handle, ref timeouts);
            }
            catch (ObjectDisposedException)
            {
                throw PortClosedException();
            }
            if (!ok) throw IoFailed(handle, Marshal.GetLastWin32Error());
        }

        #endregion

        #region Read / Write

        public void DiscardInBuffer()
        {
            SafeFileHandle handle = OpenHandle();
            // SerialPort's event thread clears latched line errors all the time; without
            // it, do it here so an error flag can't block the next session.
            TryClearCommError(handle);
            Purge(handle, PURGE_RXCLEAR | PURGE_RXABORT);
        }

        public void DiscardOutBuffer()
        {
            SafeFileHandle handle = OpenHandle();
            Purge(handle, PURGE_TXCLEAR | PURGE_TXABORT);
        }

        /// <summary>
        /// Reads up to <paramref name="count"/> bytes. Returns as soon as at least one byte
        /// is available; throws TimeoutException if nothing arrives within ReadTimeout.
        /// </summary>
        public int Read(byte[] buffer, int offset, int count)
        {
            SafeFileHandle handle = OpenHandle();
            ValidateBuffer(buffer, offset, count);
            if (count == 0) return 0;

            int read = DoOverlappedIo(handle, buffer, offset, count, false);
            if (read == 0) throw new TimeoutException("The operation has timed out.");
            return read;
        }

        public int ReadByte()
        {
            var one = new byte[1];
            Read(one, 0, 1);
            return one[0];
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            SafeFileHandle handle = OpenHandle();
            ValidateBuffer(buffer, offset, count);
            if (count == 0) return;

            int written = DoOverlappedIo(handle, buffer, offset, count, true);
            if (written == 0) throw new TimeoutException("The write timed out.");
        }

        private void Purge(SafeFileHandle handle, uint flags)
        {
            bool ok;
            try
            {
                ok = PurgeComm(handle, flags);
            }
            catch (ObjectDisposedException)
            {
                throw PortClosedException();
            }
            if (!ok) throw IoFailed(handle, Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Starts one overlapped ReadFile/WriteFile and always waits for it to complete
        /// before returning, so the OVERLAPPED block and the pinned buffer are never freed
        /// while the driver may still write to them. The wait is on our own event, not on
        /// the port handle, so it is safe even if the handle is closed meanwhile (closing
        /// cancels the I/O, which completes it).
        /// </summary>
        private int DoOverlappedIo(SafeFileHandle handle, byte[] buffer, int offset, int count, bool write)
        {
            IntPtr waitEvent = CreateEvent(IntPtr.Zero, true, false, null);
            if (waitEvent == IntPtr.Zero)
                throw CreateException(Marshal.GetLastWin32Error());

            IntPtr overlapped = IntPtr.Zero;
            GCHandle pinned = default(GCHandle);
            bool pending = false;
            try
            {
                overlapped = Marshal.AllocHGlobal(OverlappedSize);
                var native = new NativeOverlapped();
                native.EventHandle = waitEvent;
                Marshal.StructureToPtr(native, overlapped, false);

                pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                IntPtr data = IntPtr.Add(pinned.AddrOfPinnedObject(), offset);

                bool completed;
                try
                {
                    completed = write
                        ? WriteFile(handle, data, count, IntPtr.Zero, overlapped)
                        : ReadFile(handle, data, count, IntPtr.Zero, overlapped);
                }
                catch (ObjectDisposedException)
                {
                    // Closed before the I/O was started: nothing is pending.
                    throw PortClosedException();
                }

                if (!completed)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != ERROR_IO_PENDING) throw IoFailed(handle, error);

                    pending = true;
                    if (WaitForSingleObject(waitEvent, INFINITE) != WAIT_OBJECT_0)
                        throw CreateException(Marshal.GetLastWin32Error());
                    pending = false;
                }

                // OVERLAPPED.Internal is the NTSTATUS, InternalHigh the byte count.
                int status = unchecked((int)Marshal.ReadIntPtr(overlapped, 0).ToInt64());
                int transferred = unchecked((int)Marshal.ReadIntPtr(overlapped, IntPtr.Size).ToInt64());
                if (status < 0) throw IoFailed(handle, RtlNtStatusToDosError(status));
                return transferred;
            }
            finally
            {
                if (pending)
                {
                    // Left early (e.g. thread abort): cancel and still wait for completion
                    // before freeing memory the driver writes to.
                    try { CancelIoEx(handle, overlapped); }
                    catch { }
                    WaitForSingleObject(waitEvent, INFINITE);
                }
                if (pinned.IsAllocated) pinned.Free();
                if (overlapped != IntPtr.Zero) Marshal.FreeHGlobal(overlapped);
                CloseHandle(waitEvent);
            }
        }

        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (buffer.Length - offset < count)
                throw new ArgumentException("Offset and length were out of bounds for the array.");
        }

        #endregion

        #region Handle state

        private SafeFileHandle CurrentHandle()
        {
            lock (_sync)
            {
                return _handle;
            }
        }

        private SafeFileHandle OpenHandle()
        {
            SafeFileHandle handle = CurrentHandle();
            if (handle == null) throw PortClosedException();
            return handle;
        }

        // The error codes SerialStream's event thread treats as "device was unplugged".
        private static bool IsDeviceRemovedError(int error)
        {
            return error == ERROR_ACCESS_DENIED || error == ERROR_BAD_COMMAND || error == ERROR_DEVICE_REMOVED;
        }

        private Exception IoFailed(SafeFileHandle handle, int error)
        {
            if (IsDeviceRemovedError(error))
                MarkRemoved(handle);
            else if (error == ERROR_OPERATION_ABORTED)
                TryClearCommError(handle);
            return CreateException(error);
        }

        /// <summary>
        /// The device is gone: the port reports closed from now on and the handle is
        /// released, so the same COM port can be opened again when the device returns.
        /// </summary>
        private void MarkRemoved(SafeFileHandle handle)
        {
            lock (_sync)
            {
                if (_handle != handle) return;
                _handle = null;
            }
            ReleaseHandle(handle);
        }

        private static void ReleaseHandle(SafeFileHandle handle)
        {
            try
            {
                // Completes reads/writes other threads may still be waiting on.
                CancelIoEx(handle, IntPtr.Zero);
            }
            catch
            {
            }
            handle.Dispose();
        }

        private static void TryClearCommError(SafeFileHandle handle)
        {
            try
            {
                int errors;
                ClearCommError(handle, out errors, IntPtr.Zero);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static Exception PortClosedException()
        {
            return new InvalidOperationException("The port is closed.");
        }

        private Exception CreateException(int error)
        {
            int hresult = unchecked((int)0x80070000 | (error & 0xFFFF));
            switch (error)
            {
                case ERROR_FILE_NOT_FOUND:
                case ERROR_PATH_NOT_FOUND:
                    return new IOException("The port '" + _portName + "' does not exist.", hresult);
                case ERROR_ACCESS_DENIED:
                    return new UnauthorizedAccessException("Access to the port '" + _portName + "' is denied.");
                case ERROR_FILENAME_EXCED_RANGE:
                    return new PathTooLongException();
                case ERROR_SHARING_VIOLATION:
                    return new IOException("The process cannot access the port '" + _portName + "' because it is being used by another process.", hresult);
                default:
                    return new IOException(new Win32Exception(error).Message, hresult);
            }
        }

        #endregion

        public override string ToString()
        {
            return _portName;
        }

        /// <summary>
        /// Stream view of the port for ITransport.BaseStream.
        /// </summary>
        private sealed class PortStream : Stream
        {
            private readonly Win32SerialPort _port;

            public PortStream(Win32SerialPort port)
            {
                _port = port;
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override bool CanTimeout { get { return true; } }

            public override int ReadTimeout
            {
                get { return _port.ReadTimeout; }
                set { _port.ReadTimeout = value; }
            }

            public override int WriteTimeout
            {
                get { return _port.WriteTimeout; }
                set { _port.WriteTimeout = value; }
            }

            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _port.Read(buffer, offset, count);
            }

            public override int ReadByte()
            {
                return _port.ReadByte();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _port.Write(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _port.Close();
                base.Dispose(disposing);
            }
        }
    }
}
