# Before release

Things that work today but must be looked at again, and ideally done better, before a release.
Each entry says what it is, what was found, what stands in for a real fix now, and where to look.

## LibVLC: a media that failed to parse is released late (a stopgap)

**Where:** `src/PixelTuner.Audio.Vlc/VlcAudioPlayer.cs`, `OpenAsync` (the `catch` block) and `DisposeLater`.
Marked `TODO(release)` in the code.

**The crash.** Opening something that is not a sound (a text file named `notes.mp3`) made the whole process
die with a native SIGSEGV, but only some of the time: about two runs in three of the player tests, and always
in a different test, because the crash comes a moment after the cause. A full core dump of the test host
(`dotnet test --blame-crash --blame-crash-dump-type full`, then `lldb -c <dump>`) showed:

- the faulting thread is not a .NET thread but a thread started by `libvlccore.so.9`;
- the top frame is inside `libvlc.so.5`, called from `libvlccore` (`vlc_thread_start` -> a thread function
  -> a `libvlc` function): that is LibVLC's media parser thread delivering the "parsed" event to the
  `libvlc_media_t` through `libvlc_event_send`.

**The cause (our reading, not proven by a symbol-level trace).** `Media.Parse` returns as soon as the "parsed"
event reaches LibVLCSharp. The parser thread is still inside the event dispatch after that. When parsing did
not succeed we disposed the `Media` straight away, which detaches the event callbacks and releases the
object while the parser thread is still walking them: a use-after-free in LibVLC. The success path never
disposes the media there, so it never crashed. Parsing in a loop in a console program (no synchronization
context, so the continuation runs on LibVLC's own thread) did not crash in 40 rounds; the test host did.

**The stopgap.** On every failure after `Media` was created, `DisposeLater` releases it two seconds later on the
thread pool (the code says "2 seconds"; it started as one). The player tests then passed 8 of 8 runs; before
they aborted in most runs.

**Why this is not good enough:**

- It is a timing guess. A slow or loaded machine (CI runners, a laptop under load, Windows with an antivirus)
  can be slower than two seconds, and then the crash is back, now rare and impossible to reproduce.
- Fire-and-forget: if the app exits within two seconds the release never runs (harmless today, but nothing
  checks it), and the media lives longer than needed.
- It hides the real rule instead of stating it.
- Only the player's `OpenAsync` is covered. `VlcAudioDecoder` does not parse, but it stops and disposes players
  after a transcode in the same neighbourhood of LibVLC, and it has not shown the problem. Video, added later,
  will open media too.

**Ideas for a real fix, roughly in order of how much I would try them:**

1. Do not use `Media.Parse` to decide whether a file is a sound. Find another way to learn the length and the
   tracks that does not start LibVLC's parser thread (for example, open the file in a `MediaPlayer` that is
   never played), so there is no late event to race with.
2. Keep failed media until LibVLC is certainly finished with them: a small graveyard owned by `VlcLibrary`,
   disposed when the library is, with no timer. A few leaked objects per bad file for the life of the process is
   a fair price.
3. Wait for the actual end of the parser thread: poll `Media.State`/`IsParsed`, or call `libvlc_media_parse_stop`
   and wait for the parsed event to be delivered to every listener before disposing.
4. Check whether a newer LibVLC fixes it: the Windows package is 3.0.24, the macOS package 3.1.3.2 and Linux
   (Ubuntu 24.04) 3.0.20, so the three systems do not even run the same build. Re-test on each.
5. Report it to LibVLCSharp/VLC with the stack above if it still reproduces on the current builds.

**How to check a fix.** Run `tests/PixelTuner.Audio.Vlc.Tests` with `PIXELTUNER_REQUIRE_VLC=1` many times in a row
(for example 30), once with the stopgap removed to confirm the crash comes back, then with the fix. Also run the
same loop on Windows and macOS, where the natives come from the NuGet packages and have never been run here.
Add a test that opens a few dozen non-sounds in a row.

## Stretching units: check them on real devices

**Where:** `src/PixelTuner.Core/Imaging/ItemPreview.cs` (`PassDuration`, `EpicPassDuration`, `VideoPassDuration`), shown in the
editor's inspector (`EditorViewModel.PlaybackText`). Marked `TODO(release)` in the code. Only the preview text depends on
these units; the .bin export writes the number as PixelTuner 2 did and is checked byte for byte.

**What we assume.**

- Pixel devices and hoops (the same .bin): each column is held for k x 50 us (Ivan; 100 columns, k=100, 1000 ms is two passes).
- Video module devices: k is milliseconds per frame (40 is 25 frames a second). PixelTuner 2's own sample shows use 40 (one 50).
- E-pic poi: copied from PixelTuner 2's "Repeat" window (`RepeatItemWindow`, `K = milliseconds per column * 5`): a column is
  held for k / 5 ms. The E-pic .bin stores k * 5.

**The open discrepancy.** The same "Repeat" window is shared by pixel items, and by that formula a pixel column would be held
for k / 5 ms = 200 us per step, four times what we assume (50 us). A commented-out older version in the same file talks of
"50-ок" and a minimum column time of 0.1 ms, which fits 50 us. Nothing in PixelTuner 2's source states the device's real unit.

**To do before release.** Measure on a Pixel Poi, a hoop, a video module and an E-pic poi: upload a known picture, time one
pass against the inspector's number, and fix the unit (and this note) where it is off. Hoops have no semantics of their own
in PixelTuner 2's code (only the preview of its player maps stretching to a tiling by LED count), so confirm they match the
pixel poi.


## Preview: the swing model is a guess

**Where:** `src/PixelTuner.UI/PreviewPanel` (`PreviewViewModel.SwingSpeed`, `LedPitch`, `Tail`, `OrbitSpeed`, drawn by
`TimelinePreviewControl`). Marked `TODO(release)` in the code. What is shown is exact (`Core/Preview/TimelinePreview` plays the .bin the
build makes, pixel for byte, at the speed of each kind of device, and `tests/PixelTuner.Core.Tests/Preview` checks it); how a swing
looks is not: a poi is drawn as if swung at 6 m/s with its LEDs 16 mm apart, a hoop as a ring on an orbit of 1.5 turns a second with a
120 ms afterglow. PixelTuner 2 had a GL scene with its own sliders (scan speed 6, rotation 2, tail 0.8) and tiling tables tuned by eye.

**To do before release.** Compare with real devices (a poi and a hoop with a known picture, filmed with a long exposure) and set the
defaults so that the picture looks as wide as it does in the air. The units of the stretching under it are the open question above.

What a screen shows is not a guess: the export corrects every colour for the LEDs, so `LedCorrection` runs that correction (`PixelColor`,
`Ws2812Profile`) forwards on every level of grey and reads it backwards (`PreviewFrame.Seen`), which gives the picture back as it was
drawn, with the item's brightness. Only how a swing looks, and how bright a real LED looks next to the picture on a screen, are to be
compared with devices.


## Devices: memory units, refusals and a cancelled upload

**Where:** `src/PixelTuner.UI/DevicesPanel` (`DeviceMapping.MemoryBytes`, `DevicesPanelViewModel.Upload`). Marked `TODO(release)` in the code.

**Memory units: measured on one device.** A video module (PLR_v2FW, firmware 2.4.2) with a 4 GB card reports 3,878,489, so the number is kilobytes (3.7 GB), and
PixelTuner 2 cut it at 3,800,000 and, for platform 2, divided it by 1024 (right for the old pixel poi that reported bytes, wrong for this module). Studio keeps
the value in kilobytes (`DeviceInfo.MaxMemory`; no cut) and divides by 1024 only for a pixel or E-pic poi of platform 2, as PixelTuner 2 did; `DeviceMapping.MemoryBytes`
turns it to bytes and the build refuses a file that is bigger (`TooBig`). Not measured: the other families (a pixel poi or hoop of platform 4-6, an E-pic poi, which
PixelTuner 2's progress bar also read as kilobytes) and the poi of platform 2. The LightMaster reports free kilobytes. PixelTuner 2 compared its files with the memory in
a way that mixed units, and had the video check switched off, so its numbers cannot settle this. The panel shows the file against the memory ("12.7 KB of 3.7 GB").

**Stricter than PixelTuner 2.** PixelTuner 2 asked "send anyway?" when a picture was not as high as the device. Studio refuses (`WrongHeight`: a picture that is not as high as the LEDs the device reports, which the plan also shows by not letting such a timeline be ticked),
a video pair that ends at different times (`VideoPairEndsDiffer`), and a family that cannot play the file (a video module plays a fan's file; nothing else crosses). If real devices take such files, loosen the refusal into a warning.

**A cancelled upload leaves the device half written.** Cancelling stops between blocks and does not close the transfer (as PixelTuner 2's cancel did); the
device keeps its old show until a new upload completes, or is left in write mode until it is switched off. Check on real devices and, if needed, send
`EndWriteData` or reconnect on cancel.

**Upload speed.** The protocol is stop-and-wait (a block of 250 bytes, then its answer), so a 16 MB show is some 67,000 round trips. .NET's `SerialPort` costs
about 1.2 ms per call on macOS and Linux, and the write is a call too: measured on a video module with `info` requests, 1.0 ms in the operating system, 5.9 ms with PixelTuner 2's
four reads per answer, 2.3 ms with one read (now). The rest is the cost of `SerialPort.Write` and `Read` themselves; talking to the port through `read`/`write`/`poll` of libc
(a transport of its own on macOS and Linux) would bring it near 1 ms, but needs the port set up without `SerialPort`. Not measured: a real upload, because the flash of the
device may take longer per block than the cable does.

**Firmware update is untested on a real device.** `FirmwareCatalog` ships PixelTuner 2's `Flash_*.bin` (one per family and platform; the version is the byte at offset 52) and the
panel offers "Update firmware" / "Install firmware" (a device in its bootloader) / "Firmware from a file…". The flow is PixelTuner 2's: `SwitchToBoot`, wait up to a minute
(`DevicesPanelViewModel.BootWait`) for the same family to appear in its bootloader (on macOS the port may be renamed), then `UploadFirmware`; it cannot be cancelled, and the
device is left in its bootloader if it fails half way (press "Install firmware" again). Check on each family: that the bootloader answers "who is it" at once, that the port comes
back, and that the end of the write needs no wait. The Illuminator has a file in PixelTuner 2 but never had an update path, so it has none here either.

**Device settings are untested on a real device.** The "Device settings" tab writes what PixelTuner 2's `DefaultsWindow` wrote (`DeviceSettingsCatalog`, `DeviceSetting`), but to the one
device chosen, and each group on its own (PixelTuner 2 wrote the name together with the Wi-Fi network, to every device of a class). Guesses to check on devices: which devices take which
group (`SupportFor`: Wi-Fi and a name from platform 5 on a Pixel Poi, as only those report a name; the leader flag on poi platforms 5-6 and hoop platforms 1-2; the menu on poi and hoop only,
though PixelTuner 2's "all batons" also matched video modules), the limits of 10 characters for the name and the network and 14 for the password (the boxes of PixelTuner 2; the info
answer holds 12 bytes of name), that a Wi-Fi write without the name is accepted, and the port (PixelTuner 2's box began on 333 while it listened on 3331; here 3331, the port of
`DeviceManager`). The idle shutdown (30) is written with the menu without a box, as before. The Wi-Fi password is kept as typed in `device-settings.txt`, as PixelTuner 2 kept it.

**Not ported yet:** Illuminator, the master address of a Pixel Poi 4 (`SetAsMaster`, `WriteMac`: they need an Illuminator), and the "PixelTuner settings" tab of that window (the app's own defaults).
