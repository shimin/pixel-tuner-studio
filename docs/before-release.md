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
- Video devices: k is milliseconds per frame (40 is 25 frames a second). PixelTuner 2's own sample shows use 40 (one 50).
- E-Pic poi: copied from PixelTuner 2's "Repeat" window (`RepeatItemWindow`, `K = milliseconds per column * 5`): a column is
  held for k / 5 ms. The E-Pic .bin stores k * 5.

**The open discrepancy.** The same "Repeat" window is shared by pixel items, and by that formula a pixel column would be held
for k / 5 ms = 200 us per step, four times what we assume (50 us). A commented-out older version in the same file talks of
"50-ок" and a minimum column time of 0.1 ms, which fits 50 us. Nothing in PixelTuner 2's source states the device's real unit.

**To do before release.** Measure on a Pixel Poi, a hoop, a video module and an E-Pic poi: upload a known picture, time one
pass against the inspector's number, and fix the unit (and this note) where it is off. Hoops have no semantics of their own
in PixelTuner 2's code (only the preview of its player maps stretching to a tiling by LED count), so confirm they match the
pixel poi.

