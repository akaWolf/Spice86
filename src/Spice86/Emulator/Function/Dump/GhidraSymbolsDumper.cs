namespace Spice86.Emulator.Function.Dump;

using Memory;

using Serilog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Utils;

using VM;

public class GhidraSymbolsDumper {
    private static readonly ILogger _logger = Log.Logger.ForContext<GhidraSymbolsDumper>();

    public void Dump(Machine machine, string destinationFilePath) {
        ICollection<FunctionInformation> functionInformations = machine.Cpu.FunctionHandler.FunctionInformations.Values;
        List<string> lines = new();
        // keep addresses in a set in order not to write a label where a function was, ghidra will otherwise overwrite functions with labels and this is not cool.
        ISet<SegmentedAddress> dumpedAddresses = new HashSet<SegmentedAddress>();
        dumpFunctionInformations(lines, dumpedAddresses, functionInformations);
        dumpLabels(lines, dumpedAddresses, machine.Cpu.JumpHandler);
        using var printWriter = new StreamWriter(destinationFilePath);
        lines.ForEach(line => printWriter.WriteLine(line));
    }

    private void dumpLabels(List<string> lines, ISet<SegmentedAddress> dumpedAddresses, JumpHandler jumpHandler) {
        jumpHandler.JumpsFromTo
            .SelectMany(x => x.Value)
            .OrderBy(x => x)
            .Where(address => !dumpedAddresses.Contains(address))
            .Distinct()
            .OrderBy(x => x)
            .ToList()
            .ForEach(address => lines.Add(dumpLabel(address)));
    }

    private string dumpLabel(SegmentedAddress address) {
        return ToGhidraSymbol($"spice86_label", address, "l");
    }

    private void dumpFunctionInformations(ICollection<string> lines, ISet<SegmentedAddress> dumpedAddresses, ICollection<FunctionInformation> functionInformations) {
        functionInformations
            .OrderBy(functionInformation => functionInformation.Address)
            .Select(functionInformation => dumpFunctionInformation(dumpedAddresses, functionInformation))
            .ToList()
            .ForEach(line => lines.Add(line));
    }

    private string dumpFunctionInformation(ISet<SegmentedAddress> dumpedAddresses, FunctionInformation functionInformation) {
        dumpedAddresses.Add(functionInformation.Address);
        return ToGhidraSymbol(functionInformation.Name, functionInformation.Address, "f");
    }

    private string ToGhidraSymbol(string name, SegmentedAddress address, string type) {
        return $"{name}_{ToString(address)} {ConvertUtils.ToHex(address.ToPhysical())} {type}";
    }

    private string ToString(SegmentedAddress address) {
        return $"{ConvertUtils.ToHex16WithoutX(address.Segment)}_{ConvertUtils.ToHex16WithoutX(address.Offset)}_{ConvertUtils.ToHex32WithoutX(address.ToPhysical())}";
    }

    public IDictionary<SegmentedAddress, FunctionInformation> ReadFromFileOrCreate(string? filePath) {
        if (String.IsNullOrEmpty(filePath)) {
            _logger.Information("No file specified");
            return new Dictionary<SegmentedAddress, FunctionInformation>();
        }
        if (!File.Exists(filePath)) {
            _logger.Information("File doesn't exists");
            return new Dictionary<SegmentedAddress, FunctionInformation>();
        }
        return System.IO.File.ReadLines(filePath)
            .Select(line => toFunctionInformation(line))
            .OfType<FunctionInformation>()
            .Distinct()
            .ToDictionary(functionInformation => functionInformation.Address, functionInformation => functionInformation);

    }

    private FunctionInformation? toFunctionInformation(string line) {
        string[] split = line.Split(" ");
        if (split.Length != 3) {
            _logger.Debug("Cannot parse line {Line} into a function, only lines with 3 arguments can represent functions", line);
            // Not a function line
            return null;
        }
        string type = split[2];
        if (type != "f") {
            _logger.Information("Cannot parse line {Line} into a function, type is not f", line);
            // Not a function line
            return null;
        }
        string nameWithAddress = split[0];
        string[] nameSplit = nameWithAddress.Split("_");
        if (nameSplit.Length < 4) {
            // Format is not correct, we can't use this line
            _logger.Information("Cannot parse line {Line} into a function, segmented address missing", line);
            return null;
        }
        SegmentedAddress address;
        try {
            ushort segment = ConvertUtils.ParseHex16(nameSplit[nameSplit.Length - 3]);
            ushort offset = ConvertUtils.ParseHex16(nameSplit[nameSplit.Length - 2]);
            address = new SegmentedAddress(segment, offset);
        } catch (FormatException exception) {
            _logger.Information("Cannot parse line {Line} into a function, the last 3 underscore segments of the name are not hexadecimal values", line);
            return null;
        }
        string nameWithoutAddress = string.Join("_", nameSplit.Take(nameSplit.Length - 3));
        return new FunctionInformation(address, nameWithoutAddress);
    }
}