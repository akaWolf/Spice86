namespace Spice86.Ui.ViewModels;

using Avalonia.Collections;

using ReactiveUI;

using Spice86.Emulator;

using System.Collections.Generic;

/// <summary>
/// VM to see and edit a collection of bytes from memory
/// </summary>
public class MemoryViewModel : ViewModelBase {
    private AvaloniaList<byte> _bytes = new();
    private ProgramExecutor _programExecutor;

    public byte[] Memory => _programExecutor.Machine.Memory.Ram;
    public MemoryViewModel(ProgramExecutor programExecutor) {
        _programExecutor = programExecutor;
    }

}
