﻿namespace Spice86.Emulator.ReverseEngineer;

using Spice86.Emulator.CPU;
using Spice86.Emulator.VM;

public class MemoryBasedDataStructureWithSegmentRegisterBaseAddress : MemoryBasedDataStructureWithBaseAddressProvider {
    private readonly int _segmentRegisterIndex;

    private readonly SegmentRegisters _segmentRegisters;

    public MemoryBasedDataStructureWithSegmentRegisterBaseAddress(Machine machine, int segmentRegisterIndex) : base(machine.Memory) {
        _segmentRegisterIndex = segmentRegisterIndex;
        _segmentRegisters = machine.Cpu.State.SegmentRegisters;
    }

    public override uint BaseAddress => (uint)(_segmentRegisters.GetRegister(_segmentRegisterIndex) * 0x10);
}