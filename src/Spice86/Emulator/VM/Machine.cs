﻿namespace Spice86.Emulator.VM;

using Serilog;

using Spice86.Emulator.Callback;
using Spice86.Emulator.CPU;
using Spice86.Emulator.Devices.ExternalInput;
using Spice86.Emulator.Devices.Input.Joystick;
using Spice86.Emulator.Devices.Input.Keyboard;
using Spice86.Emulator.Devices.Sound;
using Spice86.Emulator.Devices.Timer;
using Spice86.Emulator.Devices.Video;
using Spice86.Emulator.Errors;
using Spice86.Emulator.Function;
using Spice86.Emulator.InterruptHandlers.Bios;
using Spice86.Emulator.InterruptHandlers.Dos;
using Spice86.Emulator.InterruptHandlers.Input.Keyboard;
using Spice86.Emulator.InterruptHandlers.Input.Mouse;
using Spice86.Emulator.InterruptHandlers.SystemClock;
using Spice86.Emulator.InterruptHandlers.Timer;
using Spice86.Emulator.InterruptHandlers.Vga;
using Spice86.Emulator.IOPorts;
using Spice86.Emulator.Memory;
using Spice86.Ui;

using System;

/// <summary>
/// Emulates an IBM PC
/// </summary>
public class Machine {
    private const int InterruptHandlersSegment = 0xF000;
    

    public Machine(IVideoKeyboardMouseIO? gui, CounterConfigurator counterConfigurator, JumpHandler jumpHandler, bool failOnUnhandledPort, bool debugMode) {
        Gui = gui;
        DebugMode = debugMode;

        // A full 1MB of addressable memory :)
        Memory = new Memory(0x100000);
        Cpu = new Cpu(this, jumpHandler, debugMode);

        // Breakpoints
        MachineBreakpoints = new MachineBreakpoints(this);

        // IO devices
        IoPortDispatcher = new IOPortDispatcher(this, failOnUnhandledPort);
        Cpu.IoPortDispatcher = IoPortDispatcher;
        Pic = new Pic(this, true, failOnUnhandledPort);
        Register(Pic);
        VgaCard = new VgaCard(this, gui, failOnUnhandledPort);
        Register(VgaCard);
        Timer = new Timer(this, Pic, VgaCard, counterConfigurator, failOnUnhandledPort);
        Register(Timer);
        Keyboard = new Keyboard(this, gui, failOnUnhandledPort);
        Register(Keyboard);
        Joystick = new Joystick(this, failOnUnhandledPort);
        Register(Joystick);
        PcSpeaker = new PcSpeaker(this, failOnUnhandledPort);
        Register(PcSpeaker);
        SoundBlaster = new SoundBlaster(this, failOnUnhandledPort);
        Register(SoundBlaster);
        GravisUltraSound = new GravisUltraSound(this, failOnUnhandledPort);
        Register(GravisUltraSound);
        Midi = new Midi(this, failOnUnhandledPort);
        Register(Midi);

        // Services
        CallbackHandler = new CallbackHandler(this, (ushort)InterruptHandlersSegment);
        Cpu.CallbackHandler = CallbackHandler;
        TimerInt8Handler = new TimerInt8Handler(this);
        Register(TimerInt8Handler);
        BiosKeyboardInt9Handler = new BiosKeyboardInt9Handler(this);
        Register(BiosKeyboardInt9Handler);
        VideoBiosInt10Handler = new VideoBiosInt10Handler(this, VgaCard);
        VideoBiosInt10Handler.InitRam();
        Register(VideoBiosInt10Handler);
        BiosEquipmentDeterminationInt11Handler = new BiosEquipmentDeterminationInt11Handler(this);
        Register(BiosEquipmentDeterminationInt11Handler);
        SystemBiosInt15Handler = new SystemBiosInt15Handler(this);
        Register(SystemBiosInt15Handler);
        KeyboardInt16Handler = new KeyboardInt16Handler(this, BiosKeyboardInt9Handler.GetBiosKeyboardBuffer());
        Register(KeyboardInt16Handler);
        SystemClockInt1AHandler = new SystemClockInt1AHandler(this, TimerInt8Handler);
        Register(SystemClockInt1AHandler);
        DosInt20Handler = new DosInt20Handler(this);
        Register(DosInt20Handler);
        DosInt21Handler = new DosInt21Handler(this);
        Register(DosInt21Handler);
        MouseInt33Handler = new MouseInt33Handler(this, gui);
        Register(MouseInt33Handler);
    }

    public bool DebugMode { get; private set; }

    public string DumpCallStack() {
        FunctionHandler inUse = Cpu.FunctionHandlerInUse;
        string callStack = "";
        if (inUse.Equals(Cpu.FunctionHandlerInExternalInterrupt)) {
            callStack += "From external interrupt:\n";
        }

        callStack += inUse.DumpCallStack();
        return callStack;
    }

    public BiosEquipmentDeterminationInt11Handler BiosEquipmentDeterminationInt11Handler { get; private set; }

    public BiosKeyboardInt9Handler BiosKeyboardInt9Handler { get; private set; }

    public CallbackHandler CallbackHandler { get; private set; }

    public Cpu Cpu { get; private set; }

    public DosInt20Handler DosInt20Handler { get; private set; }

    public DosInt21Handler DosInt21Handler { get; private set; }

    public GravisUltraSound GravisUltraSound { get; private set; }

    public IVideoKeyboardMouseIO? Gui { get; private set; }

    public IOPortDispatcher IoPortDispatcher { get; private set; }

    public Joystick Joystick { get; private set; }

    public Keyboard Keyboard { get; private set; }

    public KeyboardInt16Handler KeyboardInt16Handler { get; private set; }

    public MachineBreakpoints MachineBreakpoints { get; private set; }

    public Memory Memory { get; private set; }

    public Midi Midi { get; private set; }

    public MouseInt33Handler MouseInt33Handler { get; private set; }

    public PcSpeaker PcSpeaker { get; private set; }

    public Pic Pic { get; private set; }

    public SoundBlaster SoundBlaster { get; private set; }

    public SystemBiosInt15Handler SystemBiosInt15Handler { get; private set; }

    public SystemClockInt1AHandler SystemClockInt1AHandler { get; private set; }

    public Timer Timer { get; private set; }

    public TimerInt8Handler TimerInt8Handler { get; private set; }

    public VgaCard VgaCard { get; private set; }

    public VideoBiosInt10Handler VideoBiosInt10Handler { get; private set; }

    public void InstallAllCallbacksInInterruptTable() {
        CallbackHandler.InstallAllCallbacksInInterruptTable();
    }

    public string PeekReturn() {
        return ToString(Cpu.FunctionHandlerInUse.PeekReturnAddressOnMachineStackForCurrentFunction());
    }

    public string PeekReturn(CallType returnCallType) {
        return ToString(Cpu.FunctionHandlerInUse.PeekReturnAddressOnMachineStack(returnCallType));
    }

    public void Register(IIOPortHandler ioPortHandler) {
        ioPortHandler.InitPortHandlers(IoPortDispatcher);
    }

    public void Register(ICallback callback) {
        CallbackHandler.AddCallback(callback);
    }

    public void Run() {
        State state = Cpu.State;
        FunctionHandler functionHandler = Cpu.FunctionHandler;
        functionHandler.Call(CallType.MACHINE, state.CS, state.IP, null, null, () => "entry", false);
        try {
            RunLoop();
        } catch (InvalidVMOperationException) {
            throw;
        } catch (Exception e) {
            throw new InvalidVMOperationException(this, e);
        }

        MachineBreakpoints.OnMachineStop();
        functionHandler.Ret(CallType.MACHINE);
    }

    private void RunLoop() {
        while (Cpu.IsRunning) {
            if(Gui?.IsPaused == true) {
                Gui?.WaitOne();
            }
            if (DebugMode) {
                MachineBreakpoints.CheckBreakPoint();
            }

            Cpu.ExecuteNextInstruction();
            Timer.Tick();
        }
    }

    private static string ToString(SegmentedAddress? segmentedAddress) {
        if (segmentedAddress is not null) {
            return segmentedAddress.ToString();
        }

        return "null";
    }
}