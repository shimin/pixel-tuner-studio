using System.Reflection;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace PixelTuner.Audio.Vlc;

/// <summary>
/// The loaded LibVLC: one per process, shared by the player and the decoder. Windows and macOS ship their
/// own copy next to the app (the VideoLAN NuGet packages put it under <c>libvlc/&lt;rid&gt;</c>); Linux uses
/// the system's.
/// </summary>
public sealed class VlcLibrary : IDisposable
{
    private static readonly Lock Gate = new();
    private static bool _coreInitialised;
    private static VlcLibrary? _shared;

    private VlcLibrary(LibVLC engine) => Engine = engine;

    public LibVLC Engine { get; }

    /// <summary>
    /// The shared instance, or null when LibVLC cannot be loaded (not installed, wrong architecture).
    /// Sound is then unavailable but the rest of the app works.
    /// </summary>
    public static VlcLibrary? Shared
    {
        get
        {
            lock (Gate) return _shared ??= TryCreate();
        }
    }

    /// <summary>A separate instance with its own options, for tests ("--aout=dummy" plays without a sound card).</summary>
    public static VlcLibrary? TryCreate(params string[] options)
    {
        try
        {
            lock (Gate)
            {
                if (!_coreInitialised)
                {
                    if (OperatingSystem.IsLinux()) NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, ResolveLinux);
                    string? directory = BundledDirectory();
                    if (directory is not null) Core.Initialize(directory);
                    else Core.Initialize();
                    _coreInitialised = true;
                }
            }
            string[] all = ["--no-video", "--no-xlib", "--quiet", .. options];
            return new VlcLibrary(new LibVLC(all));
        }
        catch (Exception ex) when (ex is VLCException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// LibVLCSharp asks for "libvlc", which on Linux is the development package's symlink. The packages
    /// people really have installed (libvlc5) carry only the versioned names, so those are tried as well.
    /// </summary>
    private static IntPtr ResolveLinux(string name, Assembly assembly, DllImportSearchPath? path)
    {
        string[] candidates = name switch
        {
            "libvlc" => ["libvlc.so.5", "libvlc.so"],
            "libvlccore" => ["libvlccore.so.9", "libvlccore.so"],
            _ => [],
        };
        foreach (string candidate in candidates)
            if (NativeLibrary.TryLoad(candidate, assembly, path, out var handle)) return handle;
        return IntPtr.Zero;   // anything else: the default lookup
    }

    /// <summary>Where the app's own LibVLC is, or null to let the system look (Linux).</summary>
    public static string? BundledDirectory()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "libvlc");
        var architecture = RuntimeInformation.ProcessArchitecture;
        string? directory = null;
        if (OperatingSystem.IsWindows())
            directory = Path.Combine(root, architecture switch { Architecture.Arm64 => "win-arm64", Architecture.X86 => "win-x86", _ => "win-x64" });
        else if (OperatingSystem.IsMacOS())
            directory = Path.Combine(root, architecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64", "lib");
        return directory is not null && Directory.Exists(directory) ? directory : null;
    }

    public void Dispose() => Engine.Dispose();
}
