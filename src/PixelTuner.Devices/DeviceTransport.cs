using System.IO.Ports;
using System.Net.Sockets;

namespace PixelTuner.Devices;

/// <summary>
/// A byte channel to one device: a serial port (USB cable) or a TCP connection (Wi-Fi).
/// Calls block; the device layer runs them off the UI thread.
/// </summary>
public interface IDeviceTransport : IDisposable
{
    /// <summary>Where the device is: "COM3", "/dev/cu.usbserial-110", "192.168.1.40:50122".</summary>
    string ChannelId { get; }

    bool IsOpen { get; }

    TimeSpan ReadTimeout { get; set; }
    TimeSpan WriteTimeout { get; set; }

    /// <summary>Bytes that have arrived and can be read without waiting.</summary>
    int BytesAvailable { get; }

    /// <summary>Drops whatever the device sent that nobody read yet.</summary>
    void DiscardInput();

    void Write(ReadOnlySpan<byte> data);

    /// <summary>Reads at least one byte and at most <c>buffer.Length</c>.</summary>
    /// <exception cref="TimeoutException">Nothing arrived within <see cref="ReadTimeout"/>.</exception>
    /// <exception cref="IOException">The connection is gone.</exception>
    int Read(Span<byte> buffer);
}

/// <summary>A device on a serial port. PixelTuner 2 talked to every device at 115200 baud.</summary>
public sealed class SerialPortTransport : IDeviceTransport
{
    public const int BaudRate = 115200;

    private readonly SerialPort _port;

    public SerialPortTransport(string portName)
    {
        _port = new SerialPort(portName, BaudRate);
        _port.Open();
    }

    public string ChannelId => _port.PortName;
    public bool IsOpen => _port.IsOpen;

    public TimeSpan ReadTimeout
    {
        get => TimeSpan.FromMilliseconds(_port.ReadTimeout);
        set => _port.ReadTimeout = (int)value.TotalMilliseconds;
    }

    public TimeSpan WriteTimeout
    {
        get => TimeSpan.FromMilliseconds(_port.WriteTimeout);
        set => _port.WriteTimeout = (int)value.TotalMilliseconds;
    }

    public int BytesAvailable => _port.BytesToRead;

    public void DiscardInput()
    {
        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
    }

    public void Write(ReadOnlySpan<byte> data) => _port.Write(data.ToArray(), 0, data.Length);

    public int Read(Span<byte> buffer)
    {
        // SerialPort.Read honours ReadTimeout on every platform; BaseStream does not on Unix.
        byte[] chunk = new byte[buffer.Length];
        int read = _port.Read(chunk, 0, chunk.Length);
        chunk.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    public void Dispose()
    {
        try { _port.Close(); }
        catch (IOException) { }   // the cable was pulled
        _port.Dispose();
    }
}

/// <summary>
/// A device that connected over Wi-Fi. Devices connect to the app (TCP port 3331) after
/// the app announced itself; see <see cref="NetworkDeviceListener"/>.
/// </summary>
public sealed class TcpDeviceTransport(Socket socket) : IDeviceTransport
{
    public string ChannelId { get; } = socket.RemoteEndPoint?.ToString() ?? "";

    public bool IsOpen => socket.Connected;

    public TimeSpan ReadTimeout
    {
        get => TimeSpan.FromMilliseconds(socket.ReceiveTimeout);
        set => socket.ReceiveTimeout = (int)value.TotalMilliseconds;
    }

    public TimeSpan WriteTimeout
    {
        get => TimeSpan.FromMilliseconds(socket.SendTimeout);
        set => socket.SendTimeout = (int)value.TotalMilliseconds;
    }

    public int BytesAvailable => socket.Available;

    public void DiscardInput()
    {
        Span<byte> buffer = stackalloc byte[1024];
        while (socket.Available > 0) socket.Receive(buffer);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        try
        {
            while (!data.IsEmpty) data = data[socket.Send(data)..];
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
        {
            throw new TimeoutException("The device did not take the data in time.", e);
        }
        catch (SocketException e)
        {
            throw new IOException("The connection to the device is gone.", e);
        }
    }

    public int Read(Span<byte> buffer)
    {
        int read;
        try
        {
            read = socket.Receive(buffer);
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
        {
            throw new TimeoutException("The device did not answer in time.", e);
        }
        catch (SocketException e)
        {
            throw new IOException("The connection to the device is gone.", e);
        }
        // 0 means the device closed the connection.
        if (read == 0 && !buffer.IsEmpty) throw new IOException("The device closed the connection.");
        return read;
    }

    public void Dispose()
    {
        try { socket.Shutdown(SocketShutdown.Both); }
        catch (SocketException) { }
        socket.Dispose();
    }
}
