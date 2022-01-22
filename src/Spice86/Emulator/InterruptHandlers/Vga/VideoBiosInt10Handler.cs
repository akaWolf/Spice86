﻿namespace Spice86.Emulator.InterruptHandlers.Vga;

using Serilog;

using Spice86.Emulator.Callback;
using Spice86.Emulator.Devices.Video;
using Spice86.Emulator.Errors;
using Spice86.Emulator.VM;
using Spice86.Emulator.Memory;
using Spice86.Utils;

public class VideoBiosInt10Handler : InterruptHandler {
    public const int BiosVideoMode = 0x49;
    public static readonly uint BIOS_VIDEO_MODE_ADDRESS = MemoryUtils.ToPhysicalAddress(MemoryMap.BiosDataAreaSegment, BiosVideoMode);
    public static readonly uint CRT_IO_PORT_ADDRESS_IN_RAM = MemoryUtils.ToPhysicalAddress(MemoryMap.BiosDataAreaSegment, MemoryMap.BiosDataAreaOffsetCrtIoPort);
    private static readonly ILogger _logger = Log.Logger.ForContext<VideoBiosInt10Handler>();
    private readonly byte _currentDisplayPage = 0;
    private readonly byte _numberOfScreenColumns = 80;
    private readonly VgaCard _vgaCard;

    public VideoBiosInt10Handler(Machine machine, VgaCard vgaCard) : base(machine) {
        this._vgaCard = vgaCard;
        FillDispatchTable();
    }

    public void GetBlockOfDacColorRegisters() {
        ushort firstRegisterToGet = _state.GetBX();
        ushort numberOfColorsToGet = _state.GetCX();
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("GET BLOCKS OF DAC COLOR REGISTERS. First register is {@FirstRegisterToGet}, getting {@NumberOfColorsToGet} colors, values are to be stored at address {@EsDx}", ConvertUtils.ToHex(firstRegisterToGet), numberOfColorsToGet, ConvertUtils.ToSegmentedAddressRepresentation(_state.GetES(), _state.GetDX()));
        }

        uint colorValuesAddress = MemoryUtils.ToPhysicalAddress(_state.GetES(), _state.GetDX());
        _vgaCard.GetBlockOfDacColorRegisters(firstRegisterToGet, numberOfColorsToGet, colorValuesAddress);
    }

    public override byte GetIndex() {
        return 0x10;
    }

    public void GetSetPaletteRegisters() {
        byte op = _state.GetAL();
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("GET/SET PALETTE REGISTERS {@Operation}", ConvertUtils.ToHex8(op));
        }

        if (op == 0x12) {
            SetBlockOfDacColorRegisters();
        } else if (op == 0x17) {
            GetBlockOfDacColorRegisters();
        } else {
            throw new UnhandledOperationException(_machine, $"Unhandled operation for get/set palette registers op={ConvertUtils.ToHex8(op)}");
        }
    }

    public byte GetVideoModeValue() {
        _logger.Information("GET VIDEO MODE");
        return _memory.GetUint8(BIOS_VIDEO_MODE_ADDRESS);
    }

    public void GetVideoStatus() {
        _logger.Debug("GET VIDEO STATUS");
        _state.SetAH(_numberOfScreenColumns);
        _state.SetAL(GetVideoModeValue());
        _state.SetBH(_currentDisplayPage);
    }

    public void InitRam() {
        this.SetVideoModeValue(VgaCard.MODE_320_200_256);
        _memory.SetUint16(CRT_IO_PORT_ADDRESS_IN_RAM, VgaCard.CRT_IO_PORT);
    }

    public override void Run() {
        byte operation = _state.GetAH();
        this.Run(operation);
    }

    public void ScrollPageUp() {
        byte scrollAmount = _state.GetAL();
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("SCROLL PAGE UP BY AMOUNT {@ScrollAmount}", ConvertUtils.ToHex8(scrollAmount));
        }
    }

    public void SetBlockOfDacColorRegisters() {
        ushort firstRegisterToSet = _state.GetBX();
        ushort numberOfColorsToSet = _state.GetCX();
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("SET BLOCKS OF DAC COLOR REGISTERS. First register is {@FirstRegisterToSet}, setting {@NumberOfColorsToSet} colors, values are from address {@EsDx}", ConvertUtils.ToHex(firstRegisterToSet), numberOfColorsToSet, ConvertUtils.ToSegmentedAddressRepresentation(_state.GetES(), _state.GetDX()));
        }

        uint colorValuesAddress = MemoryUtils.ToPhysicalAddress(_state.GetES(), _state.GetDX());
        _vgaCard.SetBlockOfDacColorRegisters(firstRegisterToSet, numberOfColorsToSet, colorValuesAddress);
    }

    public void SetColorPalette() {
        byte colorId = _state.GetBH();
        byte colorValue = _state.GetBL();
        _logger.Information("SET COLOR PALETTE {@ColorId}, {@ColorValue}", colorId, colorValue);
    }

    public void SetCursorPosition() {
        byte cursorPositionRow = _state.GetDH();
        byte cursorPositionColumn = _state.GetDL();
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("SET CURSOR POSITION, {@Row}, {@Column}", ConvertUtils.ToHex8(cursorPositionRow), ConvertUtils.ToHex8(cursorPositionColumn));
        }
    }

    public void SetCursorType() {
        ushort cursorStartEnd = _state.GetCX();
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("SET CURSOR TYPE, SCAN LINE START END IS {@CursorStartEnd}", ConvertUtils.ToHex(cursorStartEnd));
        }
    }

    public void SetVideoMode() {
        byte videoMode = _state.GetAL();
        SetVideoModeValue(videoMode);
    }

    public void SetVideoModeValue(byte mode) {
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("SET VIDEO MODE {@VideoMode}", ConvertUtils.ToHex8(mode));
        }
        _memory.SetUint8(BIOS_VIDEO_MODE_ADDRESS, mode);
        _vgaCard.SetVideoModeValue(mode);
    }

    public void WriteTextInTeletypeMode() {
        byte chr = _state.GetAL();
        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Information)) {
            _logger.Information("Write Text in Teletype Mode ascii code {@AsciiCode}, chr {@Character}", ConvertUtils.ToHex(chr), ConvertUtils.ToChar(chr));
        }
    }

    private void FillDispatchTable() {
        _dispatchTable.Add(0x00, new Callback(0x00, this.SetVideoMode));
        _dispatchTable.Add(0x01, new Callback(0x01, this.SetCursorType));
        _dispatchTable.Add(0x02, new Callback(0x02, this.SetCursorPosition));
        _dispatchTable.Add(0x06, new Callback(0x06, this.ScrollPageUp));
        _dispatchTable.Add(0x0B, new Callback(0x0B, this.SetColorPalette));
        _dispatchTable.Add(0x0E, new Callback(0x0E, this.WriteTextInTeletypeMode));
        _dispatchTable.Add(0x0F, new Callback(0x0F, this.GetVideoStatus));
        _dispatchTable.Add(0x10, new Callback(0x10, this.GetSetPaletteRegisters));
        _dispatchTable.Add(0x12, new Callback(0x12, this.VideoSubsystemConfiguration));
        _dispatchTable.Add(0x1A, new Callback(0x1A, this.VideoDisplayCombination));
    }

    private void VideoDisplayCombination() {
        byte op = _state.GetAL();
        if (op == 0) {
            _logger.Information("GET VIDEO DISPLAY COMBINATION");
            // VGA with analog color display
            _state.SetBX(0x08);
        } else if (op == 1) {
            _logger.Information("SET VIDEO DISPLAY COMBINATION");
            throw new UnhandledOperationException(_machine, "Unimplemented");
        } else {
            throw new UnhandledOperationException(_machine,
                $"Unhandled operation for videoDisplayCombination op={ConvertUtils.ToHex8(op)}");
        }
        _state.SetAL(0x1A);
    }

    private void VideoSubsystemConfiguration() {
        byte op = _state.GetBL();
        if (op == 0x0) {
            _logger.Information("UNKNOWN!");
            return;
        }
        if (op == 0x10) {
            _logger.Information("GET VIDEO CONFIGURATION INFORMATION");
            // color
            _state.SetBH(0);
            // 64k of vram
            _state.SetBL(0);
            // From dosbox source code ...
            _state.SetCH(0);
            _state.SetCL(0x09);
            return;
        }
        throw new UnhandledOperationException(_machine,
        $"Unhandled operation for videoSubsystemConfiguration op={ConvertUtils.ToHex8(op)}");
    }
}