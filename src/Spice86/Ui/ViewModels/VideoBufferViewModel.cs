﻿namespace Spice86.UI.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Spice86.Emulator.Devices.Video;
using Spice86.UI.Views;

using System;
using System.Threading.Tasks;

public partial class VideoBufferViewModel : ObservableObject, IComparable<VideoBufferViewModel>, IDisposable {
    private bool _disposedValue;
    private readonly int _initialHeight;
    private readonly int _initialWidth;

    /// <summary>
    /// For AvaloniaUI Designer
    /// </summary>
    public VideoBufferViewModel() {
        if (Design.IsDesignMode == false) {
            throw new InvalidOperationException("This constructor is not for runtime usage");
        }
        Width = 320;
        Height = 200;
        Address = 1;
        _index = 1;
        Scale = 1;
    }

    public VideoBufferViewModel(double scale, int width, int height, uint address, int index, bool isPrimaryDisplay) {
        _isPrimaryDisplay = isPrimaryDisplay;
        Width = _initialWidth = width;
        Height = _initialHeight = height;
        Address = address;
        _index = index;
        Scale = scale;
        MainWindow.AppClosing += MainWindow_AppClosing;
    }

    private Action? UIUpdateMethod { get; set; }

    internal void SetUIUpdateMethod(Action invalidateImagAction) {
        UIUpdateMethod = invalidateImagAction;
    }

    [ICommand]
    public async Task SaveBitmap() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var picker = new SaveFileDialog {
                DefaultExtension = "bmp",
                InitialFileName = "screenshot.bmp",
                Title = "Save Bitmap"
            };
            string? file = await picker.ShowAsync(desktop.MainWindow);
            if (string.IsNullOrWhiteSpace(file) == false) {
                _bitmap.Save(file);
            }
        }
    }

    private void MainWindow_AppClosing(object? sender, System.ComponentModel.CancelEventArgs e) {
        _appClosing = true;
    }

    public uint Address { get; private set; }


    /// <summary>
    /// TODO : Get current DPI from Avalonia or Skia.
    /// It isn't DesktopScaling or RenderScaling as this returns 1 when Windows Desktop Scaling is set at 100%
    /// DPI: AvaloniaUI, like WPF, renders UI Controls in Device Independant Pixels.<br/>
    /// According to searches online, DPI is tied to a TopLevel control (a Window).<br/>
    /// Right now, the DPI is hardcoded for WriteableBitmap : https://github.com/AvaloniaUI/Avalonia/issues/1292 <br/>
    /// See also : https://github.com/AvaloniaUI/Avalonia/pull/1889 <br/>
    /// Also WriteableBitmap is an IImage implementation and not a UI Control,<br/>
    /// that's why it's used to bind the Source property of the Image control in VideoBufferView.xaml<br/>
    /// </summary>
    [ObservableProperty]
    private WriteableBitmap _bitmap = new(new PixelSize(320, 200), new Vector(75, 75), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

    private bool _showCursor = true;

    public bool ShowCursor {
        get => _showCursor;
        set {
            this.SetProperty(ref _showCursor, value);
            if (_showCursor) {
                Cursor?.Dispose();
                Cursor = Cursor.Default;
            } else {
                Cursor?.Dispose();
                Cursor = new Cursor(StandardCursorType.None);
            }
        }
    }

    [ObservableProperty]
    private Cursor? _cursor = Cursor.Default;

    private double _scale = 1;

    public double Scale {
        get => _scale;
        set => this.SetProperty(ref _scale, Math.Max(value, 1));
    }

    [ObservableProperty]
    private int _height = 320;

    [ObservableProperty]
    public bool _isPrimaryDisplay;

    [ObservableProperty]
    private int _width = 200;

    private bool _appClosing;

    private readonly int _index;

    public int CompareTo(VideoBufferViewModel? other) {
        if (_index < other?._index) {
            return -1;
        } else if (_index == other?._index) {
            return 0;
        } else {
            return 1;
        }
    }

    public void Dispose() {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    [ObservableProperty]
    private bool _isDrawing;

    private byte[] _memoryRange = Array.Empty<byte>();
    private Rgb[] _previousPalette = Array.Empty<Rgb>();
    
    public unsafe void Draw(byte[] memory, Rgb[] palette) {
        if (_appClosing || _disposedValue || UIUpdateMethod is null) {
            return;
        }
        
        int size = Width * Height;
        int endAddress = (int)(Address + size);
        if (_memoryRange.Length == 0) {
            _memoryRange = new byte[size];
            _previousPalette = palette;
        }
        
        if (_memoryRange.AsSpan()
                .SequenceEqual(
                    memory
                    .AsSpan(
                    (int)Address,
                    size))
            &&
            _previousPalette.AsSpan().SequenceEqual(palette)) {
            return;
        }

        using ILockedFramebuffer pixels = Bitmap.Lock();
        uint* firstPixelAddress = (uint*)pixels.Address;
        int rowBytes = Width;
        uint memoryAddress = Address;
        uint* currentRow = firstPixelAddress;
        for (int row = 0; row < Height; row++) {
            uint* startOfLine = currentRow;
            uint* endOfLine = currentRow + Width;
            for (uint* column = startOfLine; column < endOfLine; column++) {
                byte colorIndex = memory[memoryAddress];
                Rgb pixel = palette[colorIndex];
                uint argb = pixel.ToArgb();
                if (pixels.Format == PixelFormat.Rgba8888) {
                    argb = pixel.ToRgba();
                }
                *column = argb;
                memoryAddress++;
            }
            currentRow += rowBytes;
        }
        if (!IsDrawing) {
            IsDrawing = true;
        }
        Dispatcher.UIThread.Post(() => UIUpdateMethod?.Invoke(), DispatcherPriority.MaxValue);
 
        Array.ConstrainedCopy(
            memory, 
            (int)Address, 
            _memoryRange, 
            0, 
            size);
    }

    public override bool Equals(object? obj) {
        return this == obj || (obj is VideoBufferViewModel other) && _index == other._index;
    }

    public override int GetHashCode() {
        return _index;
    }

    protected virtual void Dispose(bool disposing) {
        if (!_disposedValue) {
            if (disposing) {
                Dispatcher.UIThread.Post(() => {
                    Bitmap?.Dispose();
                    Cursor?.Dispose();
                }, DispatcherPriority.MaxValue);
            }
            _disposedValue = true;
        }
    }
}