namespace Spice86.Emulator.Sound.Midi.MT32;

using Mt32emu;

using System;
using System.Threading;
using System.IO;
using System.IO.Compression;

using TinyAudio;

using OpenTK.Audio.OpenAL;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

internal sealed class Mt32Player : IDisposable {
    private readonly Mt32Context _mt32context = new();
    private readonly AudioPlayer? _windowsAudioPlayer;
    private bool _disposed;
    private readonly ALContext? _alContext;
    private readonly ALDevice? _alDevice;
    private readonly int _alBufferId;
    private readonly int _alSourceId;
    private readonly Thread? _alThread;
    private readonly short[] _alBuffer = new short[100];

    public Mt32Player(string romsPath, Configuration configuration) {
        if (string.IsNullOrWhiteSpace(romsPath)) {
            throw new ArgumentNullException(nameof(romsPath));
        }
        if (!configuration.CreateAudioBackend) {
            return;
        }

        LoadRoms(romsPath);
        AnalogOutputMode analogMode = Mt32GlobalState.GetBestAnalogOutputMode(48000);
        _mt32context.AnalogOutputMode = analogMode;
        _mt32context.SetSampleRate(48000);
        _mt32context.OpenSynth();

        if (OperatingSystem.IsWindows()) {
            _windowsAudioPlayer = Audio.CreatePlayer();
            _windowsAudioPlayer?.BeginPlayback(this.FillBuffer);
        }
        else {
            _alDevice = new ALDevice();
            _alContext = new ALContext(_alDevice.Value);
            _alSourceId = AL.GenSource();
            _alBufferId = AL.GenBuffer();
            AL.SourceQueueBuffer(_alSourceId, _alBufferId);
            AL.SourcePlay(_alSourceId);
            _alThread = new Thread(ALThread);
            _alThread.Start();
        }
    }

    private unsafe void ALThread() {
        //OpenAL, how does it work ?
        //FIXME: CPU usage (AutoManualResetEvent)
        while(true) {
            Span<byte> buffer = MemoryMarshal.AsBytes<short>(_alBuffer);
            int samplesWritten = 0;
            try {
                _mt32context.Render(MemoryMarshal.Cast<byte, short>(buffer));
                samplesWritten = buffer.Length;
                fixed(byte* buf = buffer) {
                    AL.BufferData(_alBufferId, ALFormat.Stereo16, buf, samplesWritten, 48000);
                }
                AL.GetSource(_alSourceId, ALGetSourcei.BuffersQueued, out int queued_count);
                if (queued_count > 0)
                    {
                        AL.GetSource(_alSourceId, ALGetSourcei.SourceState, out int state);
                        if ((ALSourceState)state != ALSourceState.Playing)
                        {
                            AL.SourcePlay(_alSourceId);
                        }
                    }
            } catch (ObjectDisposedException) {
                buffer.Clear();
                samplesWritten = 0;
            }
        }
    }

    public void PlayShortMessage(uint message) => _mt32context.PlayMessage(message);
    public void PlaySysex(ReadOnlySpan<byte> data) => _mt32context.PlaySysex(data);

    public void Pause() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        //... Do not pause ...
        //audioPlayer?.StopPlayback();
    }

    public void Resume() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        // ... and restart, this produces an InvalidOperationException
        //audioPlayer?.BeginPlayback(this.FillBuffer);
    }

    public void Dispose() {
        if (!_disposed) {
            _mt32context.Dispose();
            if (OperatingSystem.IsWindows()) {
                _windowsAudioPlayer?.Dispose();
            }
            else {
                AL.SourceStop(_alSourceId);
                AL.DeleteSource(_alSourceId);
                AL.DeleteBuffer(_alBufferId);
            }
            _disposed = true;
        }
    }

    private void FillBuffer(Span<float> buffer, out int samplesWritten) {
        try {
            _mt32context.Render(buffer);
            samplesWritten = buffer.Length;
        } catch (ObjectDisposedException) {
            buffer.Clear();
            samplesWritten = buffer.Length;
        }
    }
    private void LoadRoms(string path) {
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
            using var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
            foreach (ZipArchiveEntry? entry in zip.Entries) {
                if (entry.FullName.EndsWith(".ROM", StringComparison.OrdinalIgnoreCase)) {
                    using Stream? stream = entry.Open();
                    _mt32context.AddRom(stream);
                }
            }
        } else if (Directory.Exists(path)) {
            foreach (string? fileName in Directory.EnumerateFiles(path, "*.ROM")) {
                _mt32context.AddRom(fileName);
            }
        }
    }
}
