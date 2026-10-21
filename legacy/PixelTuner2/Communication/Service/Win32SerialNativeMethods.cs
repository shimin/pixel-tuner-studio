using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Communication.Service
{
    /// <summary>
    /// Win32 serial port API used by <see cref="Win32SerialPort"/>.
    /// Constants and structures follow winbase.h.
    /// </summary>
    internal static class Win32SerialNativeMethods
    {
        internal const uint GENERIC_READ = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint OPEN_EXISTING = 3;
        internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        internal const int FILE_TYPE_UNKNOWN = 0x0000;
        internal const int FILE_TYPE_CHAR = 0x0002;

        internal const int ERROR_FILE_NOT_FOUND = 2;
        internal const int ERROR_PATH_NOT_FOUND = 3;
        internal const int ERROR_ACCESS_DENIED = 5;
        internal const int ERROR_INVALID_HANDLE = 6;
        internal const int ERROR_BAD_COMMAND = 22;
        internal const int ERROR_SHARING_VIOLATION = 32;
        internal const int ERROR_INVALID_PARAMETER = 87;
        internal const int ERROR_FILENAME_EXCED_RANGE = 206;
        internal const int ERROR_OPERATION_ABORTED = 995;
        internal const int ERROR_IO_PENDING = 997;
        internal const int ERROR_DEVICE_REMOVED = 1617;

        // EscapeCommFunction
        internal const int CLRRTS = 4;
        internal const int CLRDTR = 6;

        // PurgeComm
        internal const uint PURGE_TXABORT = 0x0001;
        internal const uint PURGE_RXABORT = 0x0002;
        internal const uint PURGE_TXCLEAR = 0x0004;
        internal const uint PURGE_RXCLEAR = 0x0008;

        // DCB.Flags bit positions
        internal const int FBINARY = 0;
        internal const int FPARITY = 1;
        internal const int FOUTXCTSFLOW = 2;
        internal const int FOUTXDSRFLOW = 3;
        internal const int FDTRCONTROL = 4;
        internal const int FDSRSENSITIVITY = 6;
        internal const int FOUTX = 8;
        internal const int FINX = 9;
        internal const int FERRORCHAR = 10;
        internal const int FNULL = 11;
        internal const int FRTSCONTROL = 12;

        internal const int DTR_CONTROL_DISABLE = 0x00;
        internal const int RTS_CONTROL_DISABLE = 0x00;
        internal const int RTS_CONTROL_ENABLE = 0x01;
        internal const int RTS_CONTROL_HANDSHAKE = 0x02;

        internal const byte NOPARITY = 0;
        internal const byte ONESTOPBIT = 0;

        internal const byte DEFAULTXONCHAR = 17;
        internal const byte DEFAULTXOFFCHAR = 19;
        internal const byte EOFCHAR = 26;

        internal const uint MAXDWORD = 0xFFFFFFFF;

        internal const uint INFINITE = 0xFFFFFFFF;
        internal const uint WAIT_OBJECT_0 = 0;

        [StructLayout(LayoutKind.Sequential)]
        internal struct DCB
        {
            public uint DCBlength;
            public uint BaudRate;
            public uint Flags;
            public ushort wReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public byte XonChar;
            public byte XoffChar;
            public byte ErrorChar;
            public byte EofChar;
            public byte EvtChar;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct COMMTIMEOUTS
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct COMMPROP
        {
            public ushort wPacketLength;
            public ushort wPacketVersion;
            public uint dwServiceMask;
            public uint dwReserved1;
            public uint dwMaxTxQueue;
            public uint dwMaxRxQueue;
            public uint dwMaxBaud;
            public uint dwProvSubType;
            public uint dwProvCapabilities;
            public uint dwSettableParams;
            public uint dwSettableBaud;
            public ushort wSettableData;
            public ushort wSettableStopParity;
            public uint dwCurrentTxQueue;
            public uint dwCurrentRxQueue;
            public uint dwProvSpec1;
            public uint dwProvSpec2;
            public ushort wcProvChar;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int GetFileType(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCommProperties(SafeFileHandle hFile, ref COMMPROP lpCommProp);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCommModemStatus(SafeFileHandle hFile, out int lpModemStat);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EscapeCommFunction(SafeFileHandle hFile, int dwFunc);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCommTimeouts(SafeFileHandle hFile, ref COMMTIMEOUTS lpCommTimeouts);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupComm(SafeFileHandle hFile, int dwInQueue, int dwOutQueue);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PurgeComm(SafeFileHandle hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClearCommError(SafeFileHandle hFile, out int lpErrors, IntPtr lpStat);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReadFile(SafeFileHandle hFile, IntPtr lpBuffer, int nNumberOfBytesToRead,
            IntPtr lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WriteFile(SafeFileHandle hFile, IntPtr lpBuffer, int nNumberOfBytesToWrite,
            IntPtr lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CancelIoEx(SafeFileHandle hFile, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateEvent(IntPtr lpEventAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
            [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
            string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("ntdll.dll")]
        internal static extern int RtlNtStatusToDosError(int status);
    }
}
