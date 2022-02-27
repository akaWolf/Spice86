﻿namespace Spice86;

using Avalonia;
using Avalonia.ReactiveUI;

using Serilog;
using Serilog.Events;

using Spice86.Emulator;
using Spice86.Ui;

using System;
using System.Linq;

/// <summary>
/// Spice86 Entry Point
/// </summary>
public class Program {
    private const string LogFormat = "[{Timestamp:HH:mm:ss} {Level:u3} {Properties}] {Message:lj}{NewLine}{Exception}";
    private static readonly ILogger _logger = new LoggerConfiguration()
        .WriteTo.Console(outputTemplate: LogFormat)
        .WriteTo.Debug(outputTemplate: LogFormat)
        .MinimumLevel.Warning()
        /*//PIC can be very verbose when programs mistreat it ...
        .MinimumLevel.Override("Spice86.Emulator.Devices.ExternalInput.Pic", LogEventLevel.Warning)
        //Timer can be very verbose
        .MinimumLevel.Override("Spice86.Emulator.Devices.Timer.Timer", LogEventLevel.Warning)
        //PC speaker is usually not interesting
        .MinimumLevel.Override("Spice86.Emulator.Devices.Sound.PcSpeaker", LogEventLevel.Warning)
        //Display file IO and other DOS interactions
        .MinimumLevel.Override("Spice86.Emulator.InterruptHandlers.Dos", LogEventLevel.Information)
        //Display Video bios interactions
        .MinimumLevel.Override("Spice86.Emulator.InterruptHandlers.Vga", LogEventLevel.Warning)
        //A few logs at load time
        .MinimumLevel.Override("Spice86.Emulator.LoadableFile", LogEventLevel.Information)
        //Display program load informations
        .MinimumLevel.Override("Spice86.Emulator.ProgramExecutor", LogEventLevel.Information)*/
        .CreateLogger();

    public static ILogger Logger => _logger;

    /// <summary>
    /// Alternate Entry Point
    /// </summary>
    [STAThread]
    public static void RunWithOverrides<T>(string[] args, string expectedChecksum) where T : class, new() {
        var argsList = args.ToList();

        // Inject override
        argsList.Add($"--{nameof(Configuration.OverrideSupplierClassName)}={typeof(T).AssemblyQualifiedName}");
        argsList.Add($"--{nameof(Configuration.ExpectedChecksum)}={expectedChecksum}");
        Program.Main(argsList.ToArray());
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
        ((IDisposable)Logger).Dispose();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}