using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinRetroAmp.Controller;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Playlist;
using WinRetroAmp.Core.Skins;
using WinRetroAmp.Core.Tags;
using WinRetroAmp.Services;

namespace WinRetroAmp.Ui.Dialogs;

/// <summary>"Jump to file": type words to filter the playlist, Enter plays the highlighted entry.</summary>
public sealed class JumpToFileWindow : DialogBase
{
    private readonly PlayerController _c;
    private readonly TextBox _search = new() { Padding = new Thickness(3) };
    private readonly ListBox _list = new() { Height = 300, Margin = new Thickness(0, 8, 0, 0) };

    public JumpToFileWindow(PlayerController c) : base("Jump to file", 480)
    {
        _c = c;
        Body.Children.Add(Label("Search for (words in any order):"));
        Body.Children.Add(_search);
        Body.Children.Add(_list);
        Body.Children.Add(Buttons("Play", Accept, "Close"));
        _search.TextChanged += (_, _) => Filter();
        _search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Down or Key.Up && _list.Items.Count > 0)
            {
                _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, _list.Items.Count - 1);
                _list.ScrollIntoView(_list.SelectedItem);
                e.Handled = true;
            }
        };
        _list.MouseDoubleClick += (_, _) => Accept();
        Loaded += (_, _) =>
        {
            Filter();
            _search.Focus();
        };
    }

    public int SelectedIndex { get; private set; } = -1;

    private sealed record Entry(int Index, string Text)
    {
        public override string ToString() => Text;
    }

    private void Filter()
    {
        string[] words = _search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var items = _c.Playlist.Items
            .Select((item, i) => new Entry(i, $"{i + 1}. {item.DisplayTitle}"))
            .Where(e => words.All(w => e.Text.Contains(w, StringComparison.CurrentCultureIgnoreCase)
                                       || _c.Playlist[e.Index].Path.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
        _list.ItemsSource = items;
        int current = items.FindIndex(e => e.Index == _c.Playlist.CurrentIndex);
        _list.SelectedIndex = words.Length == 0 && current >= 0 ? current : items.Count > 0 ? 0 : -1;
        if (_list.SelectedItem is not null) _list.ScrollIntoView(_list.SelectedItem);
    }

    private void Accept()
    {
        if (_list.SelectedItem is Entry e)
        {
            SelectedIndex = e.Index;
            DialogResult = true;
        }
    }
}

/// <summary>File info: technical details plus an editor for the tags.</summary>
public sealed class FileInfoWindow : DialogBase
{
    private readonly TrackInfo? _info;
    private readonly PlaylistItem _item;
    private readonly PlayerController _c;
    private readonly Dictionary<string, TextBox> _fields = [];

    public FileInfoWindow(PlaylistItem item, PlayerController c) : base("File info", 520)
    {
        _item = item;
        _c = c;
        _info = TrackInfo.TryRead(item.Path);
        Body.Children.Add(new TextBox { Text = item.Path, IsReadOnly = true, BorderThickness = new Thickness(0), Background = null, TextWrapping = TextWrapping.Wrap });

        if (_info is null)
        {
            string what = PlaybackEngine.IsUrl(item.Path)
                ? $"Internet stream.\nNow playing: {(c.Playlist.Current == item ? c.StreamTitle ?? "-" : "-")}\nBitrate: {(c.Playlist.Current == item ? c.Engine.Bitrate : 0)} kbps"
                : "The file could not be read (missing, or its tags are not supported).";
            Body.Children.Add(Label(what, 10));
            Body.Children.Add(Buttons("Close", cancel: ""));
            return;
        }

        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var tags = new StackPanel();
        Grid.SetColumn(tags, 0);
        grid.Children.Add(tags);

        void Field(string name, string value, bool multiline = false)
        {
            tags.Children.Add(Label(name, 4));
            var box = new TextBox { Text = value, Padding = new Thickness(2), AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, Height = multiline ? 54 : double.NaN };
            _fields[name] = box;
            tags.Children.Add(box);
        }

        Field("Title", _info.Title);
        Field("Artist", _info.Artist);
        Field("Album", _info.Album);
        Field("Year", _info.Year);
        Field("Track", _info.Track);
        Field("Genre", _info.Genre);
        Field("Comment", _info.Comment, multiline: true);

        var details = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 24, 0, 0), LineHeight = 20 };
        details.Text =
            $"Format: {_info.Codec}\n" +
            $"Length: {SkinDraw.FormatTime(_info.Duration, hours: true)}\n" +
            $"Bitrate: {_info.Bitrate} kbps\n" +
            $"Sample rate: {_info.SampleRate} Hz\n" +
            $"Channels: {(_info.Channels == 1 ? "Mono" : _info.Channels == 2 ? "Stereo" : _info.Channels.ToString())}\n" +
            $"Size: {_info.FileSize / 1024.0 / 1024.0:0.00} MB ({_info.FileSize:N0} bytes)";
        Grid.SetColumn(details, 2);
        grid.Children.Add(details);
        Body.Children.Add(grid);
        Body.Children.Add(Buttons("Save", Save));
    }

    private void Save()
    {
        if (_info is null) return;
        _info.Title = _fields["Title"].Text;
        _info.Artist = _fields["Artist"].Text;
        _info.Album = _fields["Album"].Text;
        _info.Year = _fields["Year"].Text;
        _info.Track = _fields["Track"].Text;
        _info.Genre = _fields["Genre"].Text;
        _info.Comment = _fields["Comment"].Text;
        try
        {
            _info.Save();
            _c.RefreshTitles([_item]);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TagLib.CorruptFileException or NotSupportedException)
        {
            MessageBox.Show(this, "Could not save the tags: " + ex.Message + "\n\nIf the file is playing, stop it and try again.",
                "File info", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

/// <summary>Preferences: title format, scrolling, output device, tray and file associations.</summary>
public sealed class PreferencesWindow : DialogBase
{
    public PreferencesWindow(PlayerController c, AppShell shell) : base("Preferences", 460)
    {
        var s = c.Settings;

        Body.Children.Add(new TextBlock { Text = "Titles", FontWeight = FontWeights.SemiBold });
        Body.Children.Add(Label("Playlist title format (%1 artist, %2 title, %3 album, %4 year, %5 track, %6 genre, %7 file name):", 4));
        var format = new TextBox { Text = s.TitleFormat, Padding = new Thickness(3) };
        Body.Children.Add(format);
        var scroll = new CheckBox { Content = "Scroll song title in the main window", IsChecked = s.ScrollTitle, Margin = new Thickness(0, 8, 0, 0) };
        Body.Children.Add(scroll);
        Body.Children.Add(Label("Scroll speed:", 6));
        var speed = new Slider { Minimum = 1, Maximum = 4, Value = s.ScrollSpeed, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight };
        Body.Children.Add(speed);

        Body.Children.Add(new TextBlock { Text = "Output", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 0) });
        var devices = new ComboBox { Margin = new Thickness(0, 4, 0, 0), DisplayMemberPath = "Name", SelectedValuePath = "Index" };
        devices.ItemsSource = PlaybackEngine.GetDevices().Select(d => new { d.Index, d.Name }).ToList();
        devices.SelectedValue = s.DeviceNumber;
        if (devices.SelectedIndex < 0) devices.SelectedIndex = 0;
        Body.Children.Add(devices);
        Body.Children.Add(Label("A new output device is used from the next track.", 4));

        Body.Children.Add(new TextBlock { Text = "System", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 0) });
        var tray = new CheckBox { Content = "Show icon in the notification area", IsChecked = s.ShowTrayIcon, Margin = new Thickness(0, 4, 0, 0) };
        var toTray = new CheckBox { Content = "Minimize to the notification area", IsChecked = s.MinimizeToTray, Margin = new Thickness(0, 4, 0, 0) };
        var resume = new CheckBox { Content = "Resume playback where I left off when starting", IsChecked = s.ResumeOnStart, Margin = new Thickness(0, 4, 0, 0) };
        Body.Children.Add(tray);
        Body.Children.Add(toTray);
        Body.Children.Add(resume);

        var assoc = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var register = new Button { Content = "Associate audio files", Padding = new Thickness(8, 2, 8, 2) };
        var unregister = new Button { Content = "Remove associations", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        register.Click += (_, _) =>
        {
            FileAssociations.Register();
            MessageBox.Show(this, "WinRetro Amp now appears in \"Open with\" for audio files and playlists. " +
                                  "Choose it as the default app in Windows Settings > Apps > Default apps.", "Preferences");
        };
        unregister.Click += (_, _) => FileAssociations.Unregister();
        assoc.Children.Add(register);
        assoc.Children.Add(unregister);
        Body.Children.Add(assoc);

        Body.Children.Add(Buttons("OK", () =>
        {
            bool formatChanged = s.TitleFormat != format.Text;
            s.TitleFormat = string.IsNullOrWhiteSpace(format.Text) ? "%1 - %2" : format.Text;
            s.ScrollTitle = scroll.IsChecked == true;
            s.ScrollSpeed = (int)speed.Value;
            s.DeviceNumber = devices.SelectedValue is int d ? d : -1;
            c.Engine.DeviceNumber = s.DeviceNumber;
            s.MinimizeToTray = toTray.IsChecked == true;
            s.ResumeOnStart = resume.IsChecked == true;
            shell.SetTrayIcon(tray.IsChecked == true);
            if (formatChanged) c.RefreshTitles();
            DialogResult = true;
        }));
    }
}
