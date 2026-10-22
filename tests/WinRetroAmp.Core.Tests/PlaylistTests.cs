using WinRetroAmp.Core.Playlist;
using PlaylistModel = WinRetroAmp.Core.Playlist.Playlist;

namespace WinRetroAmp.Core.Tests;

public class PlaylistTests
{
    private static PlaylistModel Make(int n, int seed = 1)
    {
        var pl = new PlaylistModel(new Random(seed));
        pl.Add(Enumerable.Range(1, n).Select(i => new PlaylistItem($@"C:\music\{i:00}.mp3", $"Song {i:00}", TimeSpan.FromSeconds(60 + i))));
        return pl;
    }

    [Fact]
    public void Sequential_next_stops_at_end_without_repeat_and_wraps_with_repeat()
    {
        var pl = Make(3);
        pl.SetCurrent(2);
        Assert.Equal(-1, pl.PeekNext(userInitiated: false));
        Assert.Equal(0, pl.PeekNext(userInitiated: true));
        pl.Repeat = true;
        Assert.Equal(0, pl.PeekNext(userInitiated: false));
    }

    [Fact]
    public void Shuffle_plays_every_track_once_before_repeating()
    {
        var pl = Make(20, seed: 7);
        pl.SetCurrent(5);
        pl.Shuffle = true;
        var played = new List<int> { pl.CurrentIndex };
        for (int i = 0; i < 19; i++)
        {
            int next = pl.PeekNext(userInitiated: false);
            Assert.NotEqual(-1, next);
            pl.SetCurrent(next);
            played.Add(next);
        }
        Assert.Equal(20, played.Distinct().Count());
        Assert.Equal(-1, pl.PeekNext(userInitiated: false));
    }

    [Fact]
    public void Remove_keeps_current_track_pointer()
    {
        var pl = Make(5);
        pl.SetCurrent(3);
        pl[0].Selected = true;
        pl[1].Selected = true;
        pl.RemoveSelected();
        Assert.Equal(1, pl.CurrentIndex);
        Assert.Equal("Song 04", pl.Current!.Title);
    }

    [Fact]
    public void Crop_and_duplicates()
    {
        var pl = Make(4);
        pl.Add([new PlaylistItem(@"C:\MUSIC\01.mp3")]);
        pl.RemoveDuplicates();
        Assert.Equal(4, pl.Count);
        pl[2].Selected = true;
        pl.Crop();
        Assert.Single(pl.Items);
        Assert.Equal("Song 03", pl[0].Title);
    }

    [Fact]
    public void Move_selected_down_and_up()
    {
        var pl = Make(5);
        pl[1].Selected = true;
        pl[2].Selected = true;
        pl.MoveSelected(2);
        Assert.Equal(["Song 01", "Song 04", "Song 05", "Song 02", "Song 03"], pl.Items.Select(i => i.Title!));
        pl.MoveSelected(-10);
        Assert.Equal(["Song 02", "Song 03", "Song 01", "Song 04", "Song 05"], pl.Items.Select(i => i.Title!));
    }

    [Fact]
    public void Sort_reverse_and_durations()
    {
        var pl = Make(3);
        pl.Reverse();
        Assert.Equal("Song 03", pl[0].Title);
        pl.Sort(SortKey.Title);
        Assert.Equal("Song 01", pl[0].Title);
        Assert.Equal(TimeSpan.FromSeconds(61 + 62 + 63), pl.TotalDuration);
        pl[1].Selected = true;
        Assert.Equal(TimeSpan.FromSeconds(62), pl.SelectedDuration);
    }

    [Fact]
    public void M3u_round_trip_with_relative_and_absolute_paths()
    {
        var items = new[]
        {
            new PlaylistItem(@"C:\music\Café del Mar.mp3", "Artista - Café", TimeSpan.FromSeconds(245)),
            new PlaylistItem("http://radio.example.com:8000/stream", "Radio", null),
        };
        string text = PlaylistFormats.ToM3u(items);
        var back = PlaylistFormats.ParseM3u(text, @"C:\other");
        Assert.Equal(2, back.Count);
        Assert.Equal(items[0].Path, back[0].Path);
        Assert.Equal("Artista - Café", back[0].Title);
        Assert.Equal(TimeSpan.FromSeconds(245), back[0].Duration);
        Assert.Equal("http://radio.example.com:8000/stream", back[1].Path);
        Assert.Null(back[1].Duration);

        var rel = PlaylistFormats.ParseM3u("sub\\a.mp3\r\n# comment\r\n..\\b.ogg\r\n", @"C:\music\lists");
        Assert.Equal([@"C:\music\lists\sub\a.mp3", @"C:\music\b.ogg"], rel.Select(r => r.Path));
    }

    [Fact]
    public void Pls_round_trip()
    {
        var items = new[] { new PlaylistItem(@"C:\a.mp3", "A", TimeSpan.FromSeconds(10)), new PlaylistItem(@"C:\b.flac", "B", null) };
        var back = PlaylistFormats.ParsePls(PlaylistFormats.ToPls(items), @"C:\");
        Assert.Equal(["A", "B"], back.Select(b => b.Title!));
        Assert.Equal(TimeSpan.FromSeconds(10), back[0].Duration);
        Assert.Null(back[1].Duration);
    }

    [Fact]
    public void Save_and_load_m3u8_file()
    {
        string dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(dir, "list.m3u8");
            PlaylistFormats.Save(path, [new PlaylistItem(Path.Combine(dir, "ñandú.mp3"), "Ñandú", TimeSpan.FromSeconds(3))]);
            var back = PlaylistFormats.Load(path);
            Assert.Equal("Ñandú", back[0].Title);
            Assert.Equal(Path.Combine(dir, "ñandú.mp3"), back[0].Path);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
