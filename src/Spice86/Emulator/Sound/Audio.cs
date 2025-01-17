﻿namespace Spice86.Emulator.Sound;

using System;
using System.Runtime.Versioning;
using System.Threading;

using TinyAudio;

[SupportedOSPlatform("windows")]
internal static class Audio {
    public static AudioPlayer? CreatePlayer(bool useCallback = false) {
        if (OperatingSystem.IsWindows()) {
            return WasapiAudioPlayer.Create(TimeSpan.FromSeconds(0.25), useCallback);
        }
        return null;
    }

    public static void WriteFullBuffer(AudioPlayer player, ReadOnlySpan<float> buffer) {
        ReadOnlySpan<float> writeBuffer = buffer;

        while (true) {
            if (OperatingSystem.IsWindows()) {
                int count = (int)player.WriteData(writeBuffer);
                writeBuffer = writeBuffer[count..];
                if (writeBuffer.IsEmpty) {
                    return;
                }
            }
            Thread.Sleep(1);
        }
    }
    public static void WriteFullBuffer(AudioPlayer player, ReadOnlySpan<short> buffer) {
        ReadOnlySpan<short> writeBuffer = buffer;

        while (true) {
            if (!OperatingSystem.IsWindows()) {
                return;
            }
            int count = (int)player.WriteData(writeBuffer);
            writeBuffer = writeBuffer[count..];
            if (writeBuffer.IsEmpty) {
                return;
            }

            Thread.Sleep(1);
        }
    }
    public static void WriteFullBuffer(AudioPlayer player, ReadOnlySpan<byte> buffer) {
        ReadOnlySpan<byte> writeBuffer = buffer;

        float[]? floatArray = new float[writeBuffer.Length];

        for (int i = 0; i < writeBuffer.Length; i++) {
            floatArray[i] = writeBuffer[i];
        }

        var span = new ReadOnlySpan<float>(floatArray);

        while (true) {
            if (!OperatingSystem.IsWindows()) {
                return;
            }
            int count = (int)player.WriteData(span);
            writeBuffer = writeBuffer[count..];
            if (writeBuffer.IsEmpty) {
                return;
            }

            Thread.Sleep(1);
        }
    }
}
