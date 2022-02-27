namespace Spice86.Ui.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public partial class MemoryView : UserControl {
    public MemoryView() {
        InitializeComponent();
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}
