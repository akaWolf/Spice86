﻿namespace Spice86.Emulator.InterruptHandlers;

using Spice86.Emulator.Callback;
using Spice86.Emulator.CPU;
using Spice86.Emulator.Errors;
using Spice86.Emulator.VM;
using Spice86.Emulator.Memory;

public abstract class InterruptHandler : IndexBasedDispatcher, ICallback {
    protected State _state;

    protected Cpu _cpu;

    // Protected visibility because they are used by almost all implementations
    protected Machine _machine;

    protected Memory _memory;

    private bool _interruptStackPresent = true;

    protected InterruptHandler(Machine machine) {
        this._machine = machine;
        _memory = machine.Memory;
        _cpu = machine.Cpu;
        _state = _cpu.State;
    }

    public abstract byte Index { get; }

    public abstract void Run();

    protected override UnhandledOperationException GenerateUnhandledOperationException(int index) {
        return new UnhandledInterruptException(_machine, Index, index);
    }

    protected void SetCarryFlag(bool value, bool setOnStack) {
        _state.CarryFlag = value;
        if (_interruptStackPresent && setOnStack) {
            _cpu.SetFlagOnInterruptStack(Flags.Carry, value);
        }
    }

    protected void SetZeroFlag(bool value, bool setOnStack) {
        _state.ZeroFlag = value;
        if (_interruptStackPresent && setOnStack) {
            _cpu.SetFlagOnInterruptStack(Flags.Zero, value);
        }
    }

    public void RunFromOverriden() {
        // When running from overriden code, this is a direct C# code so there is no stack to edit.
        _interruptStackPresent = false;
        Run();
        _interruptStackPresent = true;
    }
}