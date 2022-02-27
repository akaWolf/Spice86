﻿namespace Spice86.Emulator.Gdb;

using Serilog;

using Spice86.Emulator.CPU;
using Spice86.Emulator.Devices.Video;
using Spice86.Emulator.Function;
using Spice86.Emulator.Function.Dump;
using Spice86.Emulator.VM;
using Spice86.Emulator.VM.Breakpoint;
using Spice86.Emulator.Memory;
using Spice86.Ui;
using Spice86.Utils;

using System;
using System.IO;
using System.Linq;
using System.Text;

using Spice86.Ui.ViewModels;

/// <summary>
/// Handles custom GDB commands triggered in command line via the monitor prefix.<br/>
/// Custom commands list can be seen with the monitor help command.
/// </summary>
public class GdbCustomCommandsHandler {
    private static readonly ILogger _logger = Program.Logger.ForContext<GdbCustomCommandsHandler>();
    private readonly string? _defaultDumpDirectory;
    private readonly string? _jumpFile;
    private readonly GdbIo _gdbIo;
    private readonly Machine _machine;
    private readonly Action<BreakPoint> _onBreakpointReached;

    public GdbCustomCommandsHandler(GdbIo gdbIo, Machine machine, Action<BreakPoint> onBreakpointReached, string? defaultDumpDirectory, string? jumpFile) {
        _gdbIo = gdbIo;
        _machine = machine;
        _onBreakpointReached = onBreakpointReached;
        _defaultDumpDirectory = defaultDumpDirectory;
        _jumpFile = jumpFile;
    }

    public virtual string HandleCustomCommands(string command) {
        String[] commandSplit = command.Split(",");
        if (commandSplit.Length != 2) {
            return _gdbIo.GenerateResponse("");
        }

        byte[] customHex = ConvertUtils.HexToByteArray(commandSplit[1]);
        string custom = Encoding.UTF8.GetString(customHex);
        String[] customSplit = custom.Split(" ");
        return ExecuteCustomCommand(customSplit);
    }

    private static double ExtractScale(String[] args) {
        if (args.Length != 5) {
            // Not specified in input
            return 1;
        }

        string scaleString = args[4];
        if (!int.TryParse(scaleString, out var scale)) {
            throw new ArgumentException($"Could not parse scale {scaleString}");
        }

        if (scale < 0.1) {
            throw new ArgumentException("Scale cannot be less than 0.1");
        }

        return scale;
    }

    private string BreakCycles(String[] args) {
        if (args.Length < 2) {
            return InvalidCommand("breakCycles can only work with one argument.");
        }

        string cyclesToWaitString = args[1];
        if (!int.TryParse(cyclesToWaitString, out _)) {
            return InvalidCommand($"breakCycles argument needs to be a number. You gave {cyclesToWaitString}");
        }

        long cyclesToWait = long.Parse(cyclesToWaitString);
        long currentCycles = _machine.Cpu.State.Cycles;
        long cyclesBreak = currentCycles + cyclesToWait;
        var breakPoint = new BreakPoint(BreakPointType.CYCLES, cyclesBreak, _onBreakpointReached, true);
        _machine.MachineBreakpoints.ToggleBreakPoint(breakPoint, true);
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Debug)) {
            _logger.Debug("Breakpoint added for cycles!\n{@BreakPoint}", breakPoint);
        }
        return _gdbIo.GenerateMessageToDisplayResponse($"Breakpoint added for cycles. Current cycles is {currentCycles}. Will wait for {cyclesToWait}. Will stop at {cyclesBreak}");
    }

    private string BreakStop() {
        BreakPoint breakPoint = new UnconditionalBreakPoint(BreakPointType.MACHINE_STOP, _onBreakpointReached, false);
        _machine.MachineBreakpoints.ToggleBreakPoint(breakPoint, true);
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Debug)) {
            _logger.Debug("Breakpoint added for end of execution!\n{@BreakPoint}", breakPoint);
        }
        return _gdbIo.GenerateMessageToDisplayResponse("Breakpoint added for end of execution.");
    }

    private string CallStack() {
        return _gdbIo.GenerateMessageToDisplayResponse(_machine.DumpCallStack());
    }

    private string DoFileAction(string fileName, Action<string> fileNameConsumer, string errorMessageInCaseIOException) {
        try {
            fileNameConsumer.Invoke(fileName);
        } catch (IOException e) {
            if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Error)) {
                _logger.Error(e, "{@ErrorMessageInCaseIOException}", errorMessageInCaseIOException);
            }
            string errorWithException = $"{errorMessageInCaseIOException}: {e.Message}";
            return _gdbIo.GenerateMessageToDisplayResponse(errorWithException);
        }

        return ResultIsInFile(fileName);
    }

    private string DumpAll() {
        string[] args = Array.Empty<string>();
        DumpMemory(args);
        DumpGhidraSymbols(args);
        DumpJumps(args);
        DumpFunctionsCsv(args);
        DumpFunctions(args);
        DumpCSharpStubs(args);
        return _gdbIo.GenerateMessageToDisplayResponse($"Dumped everything in {_defaultDumpDirectory}");
    }

    private string DumpFunctions(String[] args) {
        return DumpFunctionWithFormat(args, "FunctionsDetails.txt", new DetailedFunctionInformationToStringConverter());
    }

    private string DumpFunctionsCsv(String[] args) {
        return DumpFunctionWithFormat(args, "Functions.csv", new CsvFunctionInformationToStringConverter());
    }
    private string DumpGhidraSymbols(String[] args) {
        return DumpFunctionWithFormat(args, "GhidraSymbols.txt", new GhidraSymbolsToStringConverter());
    }

    private string DumpFunctionWithFormat(String[] args, string defaultSuffix, FunctionInformationToStringConverter converter) {
        string fileName = GetFirstArgumentOrDefaultFileSuffix(args, defaultSuffix);
        return DoFileAction(fileName, (f) => {
            Cpu cpu = _machine.Cpu;
            new FunctionInformationDumper().DumpFunctionHandlers(f, converter, cpu.StaticAddressesRecorder, cpu.FunctionHandler, cpu.FunctionHandlerInExternalInterrupt);
        }, "Error while dumping functions");
    }

    private string DumpCSharpStubs(String[] args) {
        return DumpFunctionWithFormat(args, "CSharpStub.cs", new CSharpStubToStringConverter());
    }

    private string DumpMemory(String[] args) {
        string fileName = GetFirstArgumentOrDefaultFileSuffix(args, "MemoryDump.bin");
        return DoFileAction(fileName, (f) => _machine.Memory.DumpToFile(f), "Error while dumping memory");
    }

    private string DumpJumps(string[] args) {
        string fileName = GetFirstArgumentOrDefaultFile(args, _jumpFile ?? GenerateDumpFileSuffix("jumps.json"));
        return DoFileAction(fileName, (f) => {
            new JumpDumper().Dump(_machine.Cpu.JumpHandler, fileName);
        }, "Error while dumping jumps");
    }
    private string ExecuteCustomCommand(params string[] args) {
        string originalCommand = args[0];
        string command = originalCommand.ToLowerInvariant();
        return command switch {
            "help" => Help(""),
            "state" => State(),
            "breakstop" => BreakStop(),
            "callstack" => CallStack(),
            "peekret" => PeekRet(args),
            "dumpmemory" => DumpMemory(args),
            "dumpghidrasymbols" => DumpGhidraSymbols(args),
            "dumpfunctionscsv" => DumpFunctionsCsv(args),
            "dumpfunctions" => DumpFunctions(args),
            "dumpcsharpstubs" => DumpCSharpStubs(args),
            "dumpjumps" => DumpJumps(args),
            "dumpall" => DumpAll(),
            "breakcycles" => BreakCycles(args),
            "vbuffer" => Vbuffer(args),
            _ => InvalidCommand(originalCommand),
        };
    }

    private string ExtractAction(String[] args) {
        if (args.Length >= 2) {
            return args[1];
        }

        throw new ArgumentException("You need to specify an action. Valid actions are [refresh, add, remove]");
    }

    private uint ExtractAddress(String[] args, string action) {
        if (args.Length < 3) {
            throw new ArgumentException($"You need to specify an address for action {action}. Format is 0x12AB (hex) or 1234 (decimal)");
        }

        string addressString = args[2];
        try {
            return ParseAddress(addressString);
        } catch (FormatException nfe) {
            throw new ArgumentException($"Could not parse address {addressString}", nfe);
        }
    }

    private int[] ExtractResolution(String[] args, string action) {
        if (args.Length < 4) {
            throw new ArgumentException($"You need to specify a resolution for action {action}. Format is 320x200 for resolution");
        }

        string resolutionString = args[3];
        return ParseResolution(resolutionString);
    }

    private string GetFirstArgumentOrDefaultFileSuffix(String[] args, string defaultSuffix) {
        return GetFirstArgumentOrDefaultFile(args, GenerateDumpFileSuffix(defaultSuffix));
    }

    private string GetFirstArgumentOrDefaultFile(String[] args, string defaultFile) {
        if (args.Length >= 2) {
            return args[1];
        }
        return defaultFile;
    }

    private string GenerateDumpFileSuffix(String suffix) {
        return $"{_defaultDumpDirectory}/spice86dump{suffix}";
    }

    private string GetValidRetValues() {
        return string.Join(", ", Enum.GetNames(typeof(CallType)));
    }

    private string Help(string additionnalMessage) {
        return _gdbIo.GenerateMessageToDisplayResponse($@"{additionnalMessage}
            Supported custom commands:
             -help: display this
             - dumpall: dumps everything possible in the default directory which is {_defaultDumpDirectory}
             - dumpMemory < file path to dump >: dump the memory as a binary file
             - dumpGhidraSymbols < file path to dump >: dump the functions in a format that can be imported directly into ghidra with ImportSymbolsScript.py script
             - dumpJumps < file path to dump >: dump the calls, jumps and returns destinations per instruction. To import in the spice86 code generator.
             - dumpFunctionsCsv < file path to dump >: dump information about the function calls executed in csv format
             - dumpFunctions < file path to dump >: dump information about the function calls executed with details in human readable format
             - dumpCSharpStubs < file path to dump >: dump C# stubs for functions and globals to be used as override
             - breakCycles <number of cycles to wait before break>: breaks after the given number of cycles is reached
             - breakStop: setups a breakpoint when machine shuts down
             - callStack: dumps the callstack to see in which function you are in the VM.
             - peekRet<optional type>: displays the return address of the current function as stored in the stack in RAM.If a parameter is provided, dump the return on the stack as if the return was one of the provided type. Valid values are: {GetValidRetValues()}
             - state: displays the state of the machine
             - vbuffer: family of commands to control video bufers:
               - vbuffer refresh: refreshes the screen
               - vbuffer add<address> <resolution> <scale?>: Example vbuffer add 0x1234 320x200 1.5 -> Add an additional buffer displaying what is at address 0x1234, with resolution 320x200 and scale 1.5
               - vbuffer remove <address>: Deletes the buffer at address
               - vbuffer list: Lists the buffers currently displayed
            ");
    }

    private string InvalidCommand(string command) {
        return Help($"Invalid command {command}\n");
    }

    private uint ParseAddress(string address) {
        if (address.Contains("0x")) {
            return ConvertUtils.ParseHex32(address);
        }

        return uint.Parse(address);
    }

    private int[] ParseResolution(string resolution) {
        string[] split = resolution.Split("x");
        if (split.Length != 2) {
            throw new ArgumentException($"Could not parse resolution {resolution}. Format is like 320x200");
        }

        try {
            return new[] { int.Parse(split[0]), int.Parse(split[1]) };
        } catch (FormatException nfe) {
            throw new ArgumentException($"Could not parse numbers in resolution {resolution}", nfe);
        }
    }

    private string PeekRet(String[] args) {
        if (args.Length == 1) {
            return _gdbIo.GenerateMessageToDisplayResponse(_machine.PeekReturn());
        } else {
            string returnType = args[1];
            bool parsed = Enum.TryParse(typeof(CallType), returnType, out var callType);
            if (parsed == false) {
                return _gdbIo.GenerateMessageToDisplayResponse($"Could not understand {returnType} as a return type. Valid values are: {GetValidRetValues()}");
            }
            if (callType is CallType type) {
                return _gdbIo.GenerateMessageToDisplayResponse(_machine.PeekReturn(type));
            }
        }
        return "";
    }

    private string ResultIsInFile(string fileName) {
        return _gdbIo.GenerateMessageToDisplayResponse($"Result is in file {fileName}");
    }

    private string State() {
        string state = _machine.Cpu.State.ToString();
        return _gdbIo.GenerateMessageToDisplayResponse(state);
    }

    private string Vbuffer(string[] args) {
        try {
            string action = ExtractAction(args);
            IVideoKeyboardMouseIO? gui = _machine.Gui;
            VgaCard vgaCard = _machine.VgaCard;

            // Actions for 1 parameter
            if ("refresh".Equals(action)) {
                Memory memory = _machine.Memory;
                gui?.Draw(memory.Ram, vgaCard.VgaDac.Rgbs);
                return _gdbIo.GenerateResponse("");
            } else if ("list".Equals(action)) {
                var listBuilder = new StringBuilder();
                gui?.VideoBuffersAsDictionary.ToDictionary(x => x.ToString()).Select(x => $"{x.Value}\n").ToList().ForEach(x => listBuilder.AppendLine(x));
                string list = listBuilder.ToString();
                return _gdbIo.GenerateMessageToDisplayResponse(list);
            }

            uint address = ExtractAddress(args, action);
            if ("remove".Equals(action)) {
                gui?.RemoveBuffer(address);
                return _gdbIo.GenerateMessageToDisplayResponse($"Removed buffer at address {address}");
            }

            int[] resolution = ExtractResolution(args, action);
            double scale = ExtractScale(args);
            if ("add".Equals(action)) {
                if (gui?.VideoBuffersAsDictionary.TryGetValue(address, out VideoBufferViewModel? existing) == true) {
                    return _gdbIo.GenerateMessageToDisplayResponse($"Buffer already exists: {existing}");
                }

                gui?.AddBuffer(address, scale, resolution[0], resolution[1]);
                return _gdbIo.GenerateMessageToDisplayResponse($"Added buffer to view address {address}");
            } else {
                return _gdbIo.GenerateMessageToDisplayResponse($"Could not understand action {action}");
            }
        } catch (ArgumentException e) {
            return _gdbIo.GenerateMessageToDisplayResponse(e.Message);
        }
    }
}