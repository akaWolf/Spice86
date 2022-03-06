﻿namespace Spice86.Emulator.Sound.FM;

using Spice86.Emulator.Devices;
using Spice86.Emulator.VM;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using TinyAudio;

using Ymf262Emu;

/// <summary>
/// Virtual device which emulates OPL3 FM sound.
/// </summary>
public sealed class FmSoundCard
{
    private const byte Timer1Mask = 0xC0;
    private const byte Timer2Mask = 0xA0;

    private readonly AudioPlayer audioPlayer = Audio.CreatePlayer();
    private int currentAddress;
    private readonly FmSynthesizer synth;
    private System.Threading.Thread generateThread;
    private volatile bool endThread;
    private byte timer1Data;
    private byte timer2Data;
    private byte timerControlByte;
    private byte statusByte;
    private bool initialized;
    private bool paused;

    public FmSoundCard()
    {
        this.synth = new FmSynthesizer((int)this.audioPlayer.Format.SampleRate);
        this.generateThread = new System.Threading.Thread(this.GenerateWaveforms)
        {
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.AboveNormal
        };
    }

    IEnumerable<int> InputPorts => new int[] { 0x388 };
   public  byte ReadByte(int port)
    {
        if ((this.timerControlByte & 0x01) != 0x00 && (this.statusByte & Timer1Mask) == 0)
        {
            this.timer1Data++;
            if (this.timer1Data == 0)
                this.statusByte |= Timer1Mask;
        }

        if ((this.timerControlByte & 0x02) != 0x00 && (this.statusByte & Timer2Mask) == 0)
        {
            this.timer2Data++;
            if (this.timer2Data == 0)
                this.statusByte |= Timer2Mask;
        }

        return this.statusByte;
    }
    public ushort ReadWord(int port) => this.statusByte;

    public IEnumerable<int> OutputPorts => new int[] { 0x388, 0x389 };
    public void WriteByte(int port, byte value)
    {
        if (port == 0x388)
        {
            currentAddress = value;
        }
        else if (port == 0x389)
        {
            if (currentAddress == 0x02)
            {
                this.timer1Data = value;
            }
            else if (currentAddress == 0x03)
            {
                this.timer2Data = value;
            }
            else if (currentAddress == 0x04)
            {
                this.timerControlByte = value;
                if ((value & 0x80) == 0x80)
                    this.statusByte = 0;
            }
            else
            {
                if (!this.initialized)
                    this.Initialize();

                this.synth.SetRegisterValue(0, currentAddress, value);
            }
        }
    }
    public void WriteWord(int port, ushort value)
    {
        if (port == 0x388)
        {
            WriteByte(0x388, (byte)value);
            this.WriteByte(0x389, (byte)(value >> 8));
        }
    }

    public void Pause()
    {
        if (this.initialized && !this.paused)
        {
            this.endThread = true;
            this.generateThread.Join();
            this.paused = true;
        }
    }
    public void Resume()
    {
        if (paused)
        {
            this.endThread = false;
            this.generateThread = new System.Threading.Thread(this.GenerateWaveforms) { IsBackground = true };
            this.generateThread.Start();
            this.paused = false;
        }
    }

    public void Dispose()
    {
        if (this.initialized)
        {
            if (!paused)
            {
                this.endThread = true;
                this.generateThread.Join();
            }

            this.audioPlayer.Dispose();
            this.initialized = false;
        }
    }

    /// <summary>
    /// Generates and plays back output waveform data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void GenerateWaveforms()
    {
        var buffer = new float[1024];
        float[] playBuffer;

        bool expandToStereo = this.audioPlayer.Format.Channels == 2;
        if (expandToStereo)
            playBuffer = new float[buffer.Length * 2];
        else
            playBuffer = buffer;

        this.audioPlayer.BeginPlayback();
        fillBuffer();
        while (!endThread)
        {
            Audio.WriteFullBuffer(this.audioPlayer, playBuffer);
            fillBuffer();
        }

        this.audioPlayer.StopPlayback();

        void fillBuffer()
        {
            this.synth.GetData(buffer);
            if (expandToStereo)
                ChannelAdapter.MonoToStereo(buffer.AsSpan(), playBuffer.AsSpan());
        }
    }
    /// <summary>
    /// Performs DirectSound initialization.
    /// </summary>
    private void Initialize()
    {
        this.generateThread.Start();
        this.initialized = true;
    }
}
