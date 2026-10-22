using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WinRetroAmp.Ui.Dialogs;

/// <summary>Plain Windows dialogs, built in code (the original used standard dialogs too).</summary>
public abstract class DialogBase : Window
{
    protected DialogBase(string title, double width)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 12;
        Body = new StackPanel { Margin = new Thickness(14) };
        Content = Body;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
        };
    }

    protected StackPanel Body { get; }

    protected static TextBlock Label(string text, double top = 0) => new() { Text = text, Margin = new Thickness(0, top, 0, 4), TextWrapping = TextWrapping.Wrap };

    protected StackPanel Buttons(string ok = "OK", Action? onOk = null, string cancel = "Cancel")
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var okButton = new Button { Content = ok, MinWidth = 80, IsDefault = true, Padding = new Thickness(10, 3, 10, 3) };
        okButton.Click += (_, _) =>
        {
            if (onOk is not null) onOk();
            else DialogResult = true;
        };
        panel.Children.Add(okButton);
        if (cancel.Length > 0)
        {
            var cancelButton = new Button { Content = cancel, MinWidth = 80, IsCancel = true, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 3, 10, 3) };
            panel.Children.Add(cancelButton);
        }
        return panel;
    }
}

public sealed class InputWindow : DialogBase
{
    private readonly TextBox _box;

    public InputWindow(string title, string prompt, string value) : base(title, 360)
    {
        Body.Children.Add(Label(prompt));
        _box = new TextBox { Text = value, Padding = new Thickness(3) };
        Body.Children.Add(_box);
        Body.Children.Add(Buttons());
        Loaded += (_, _) =>
        {
            _box.Focus();
            _box.SelectAll();
        };
    }

    public string Value => _box.Text;
}

public sealed class OpenUrlWindow : DialogBase
{
    private readonly ComboBox _box;

    public OpenUrlWindow(IEnumerable<string> recent) : base("Open URL", 460)
    {
        Body.Children.Add(Label("Enter the address of a file or an Internet radio stream (HTTP/Shoutcast MP3):"));
        _box = new ComboBox { IsEditable = true, ItemsSource = recent.ToList(), Padding = new Thickness(3) };
        Body.Children.Add(_box);
        Body.Children.Add(Buttons("Open"));
        Loaded += (_, _) => _box.Focus();
    }

    public string Url => _box.Text;
}

public sealed class AboutWindow : DialogBase
{
    public AboutWindow() : base("About WinRetro Amp", 420)
    {
        Body.Children.Add(new TextBlock { Text = "WinRetro Amp 1.0", FontSize = 20, FontWeight = FontWeights.SemiBold });
        Body.Children.Add(Label("A tribute to the classic skinnable MP3 players of the late 90s, built with .NET 10, WPF and NAudio.", 8));
        Body.Children.Add(Label("Plays MP3, AAC/M4A, WMA, FLAC, OGG Vorbis, WAV and AIFF files and MP3 Internet radio. " +
                                "Supports classic .wsz skins, M3U/PLS playlists and EQF presets.", 6));
        Body.Children.Add(Label("The default \"Graphite\" skin, icon and artwork are original and drawn in code. " +
                                "This project is not affiliated with or endorsed by any other media player or its owners.", 6));
        Body.Children.Add(Buttons("Close", cancel: ""));
    }
}
