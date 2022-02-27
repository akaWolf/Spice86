namespace Spice86.Ui.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using Spice86.Ui.ViewModels;

using System;
using System.ComponentModel;

public partial class MainWindow : Window {

    public MainWindow() {
        InitializeComponent();
        this.Closing += MainWindow_Closing;
        this.DataContextChanged += MainWindow_DataContextChanged;
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private async void MainWindow_DataContextChanged(object? sender, EventArgs e) {
        if(sender is MainWindowViewModel vm) {
            await Dispatcher.UIThread.InvokeAsync(() => vm.SetResolution(320, 200, 1), DispatcherPriority.MaxValue);
        }
    }

    public static event EventHandler<CancelEventArgs>? AppClosing;

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) {
        AppClosing?.Invoke(sender, e);
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}