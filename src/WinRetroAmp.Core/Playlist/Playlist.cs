namespace WinRetroAmp.Core.Playlist;

public sealed class PlaylistItem
{
    public PlaylistItem(string path, string? title = null, TimeSpan? duration = null)
    {
        Path = path;
        Title = title;
        Duration = duration;
    }

    public string Path { get; }

    /// <summary>Formatted display title (null until tags were read).</summary>
    public string? Title { get; set; }

    public TimeSpan? Duration { get; set; }
    public bool Selected { get; set; }

    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title! : FallbackTitle(Path);

    public static string FallbackTitle(string path)
    {
        if (Audio.PlaybackEngine.IsUrl(path)) return path;
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }
}

public enum SortKey { Title, FileName, Path }

/// <summary>The play queue: order, current entry, selection, shuffle and repeat.</summary>
public sealed class Playlist
{
    private readonly List<PlaylistItem> _items = [];
    private readonly Random _random;
    private List<PlaylistItem> _shuffleOrder = [];

    public Playlist(Random? random = null) => _random = random ?? Random.Shared;

    public event EventHandler? Changed;

    public IReadOnlyList<PlaylistItem> Items => _items;
    public int Count => _items.Count;
    public PlaylistItem this[int index] => _items[index];

    public int CurrentIndex { get; private set; } = -1;
    public PlaylistItem? Current => CurrentIndex >= 0 && CurrentIndex < _items.Count ? _items[CurrentIndex] : null;

    public bool Shuffle
    {
        get;
        set
        {
            field = value;
            RebuildShuffle();
        }
    }

    public bool Repeat { get; set; }

    public TimeSpan TotalDuration => Sum(_items);
    public TimeSpan SelectedDuration => Sum(_items.Where(i => i.Selected));
    public bool HasUnknownDurations => _items.Any(i => i.Duration is null);
    public IEnumerable<PlaylistItem> SelectedItems => _items.Where(i => i.Selected);

    private static TimeSpan Sum(IEnumerable<PlaylistItem> items) =>
        items.Aggregate(TimeSpan.Zero, (t, i) => t + (i.Duration ?? TimeSpan.Zero));

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Add(IEnumerable<PlaylistItem> items) => Insert(_items.Count, items);

    public void Insert(int index, IEnumerable<PlaylistItem> items)
    {
        index = Math.Clamp(index, 0, _items.Count);
        var list = items.ToList();
        if (list.Count == 0) return;
        _items.InsertRange(index, list);
        if (CurrentIndex >= index) CurrentIndex += list.Count;
        foreach (var i in list) _shuffleOrder.Insert(_random.Next(_shuffleOrder.Count + 1), i);
        RaiseChanged();
    }

    public void Clear()
    {
        _items.Clear();
        _shuffleOrder.Clear();
        CurrentIndex = -1;
        RaiseChanged();
    }

    public void SetCurrent(int index)
    {
        CurrentIndex = index >= 0 && index < _items.Count ? index : -1;
        RaiseChanged();
    }

    // ---------------------------------------------------------------- navigation

    /// <summary>Index that follows the current one, honouring shuffle and repeat (−1 = end of list).</summary>
    public int PeekNext(bool userInitiated)
    {
        if (_items.Count == 0) return -1;
        if (Shuffle)
        {
            if (_shuffleOrder.Count != _items.Count) RebuildShuffle();
            int pos = Current is null ? -1 : _shuffleOrder.IndexOf(Current);
            if (pos + 1 < _shuffleOrder.Count) return _items.IndexOf(_shuffleOrder[pos + 1]);
            if (!Repeat && !userInitiated) return -1;
            RebuildShuffle(avoidFirst: Current);
            return _items.IndexOf(_shuffleOrder[0]);
        }
        int next = CurrentIndex + 1;
        if (next < _items.Count) return next;
        return Repeat || userInitiated ? 0 : -1;
    }

    public int PeekPrevious()
    {
        if (_items.Count == 0) return -1;
        if (Shuffle)
        {
            int pos = Current is null ? 0 : _shuffleOrder.IndexOf(Current);
            return _items.IndexOf(_shuffleOrder[pos > 0 ? pos - 1 : _shuffleOrder.Count - 1]);
        }
        return CurrentIndex > 0 ? CurrentIndex - 1 : _items.Count - 1;
    }

    /// <summary>The full play order shuffle mode will use (for tests and the "next" preview).</summary>
    public IReadOnlyList<PlaylistItem> ShuffleOrder => _shuffleOrder;

    private void RebuildShuffle(PlaylistItem? avoidFirst = null)
    {
        _shuffleOrder = [.. _items];
        for (int i = _shuffleOrder.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_shuffleOrder[i], _shuffleOrder[j]) = (_shuffleOrder[j], _shuffleOrder[i]);
        }
        if (avoidFirst is not null && _shuffleOrder.Count > 1 && _shuffleOrder[0] == avoidFirst)
            (_shuffleOrder[0], _shuffleOrder[^1]) = (_shuffleOrder[^1], _shuffleOrder[0]);
        // Start the shuffle at the current track so every other track plays once.
        if (avoidFirst is null && Current is { } cur)
        {
            _shuffleOrder.Remove(cur);
            _shuffleOrder.Insert(0, cur);
        }
    }

    // ---------------------------------------------------------------- selection

    public void SelectAll(bool selected = true)
    {
        foreach (var i in _items) i.Selected = selected;
        RaiseChanged();
    }

    public void InvertSelection()
    {
        foreach (var i in _items) i.Selected = !i.Selected;
        RaiseChanged();
    }

    public void SelectOnly(int index)
    {
        for (int i = 0; i < _items.Count; i++) _items[i].Selected = i == index;
        RaiseChanged();
    }

    public void SelectRange(int from, int to, bool additive)
    {
        if (!additive) foreach (var i in _items) i.Selected = false;
        for (int i = Math.Min(from, to); i <= Math.Max(from, to); i++)
            if (i >= 0 && i < _items.Count) _items[i].Selected = true;
        RaiseChanged();
    }

    public void Toggle(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        _items[index].Selected = !_items[index].Selected;
        RaiseChanged();
    }

    // ---------------------------------------------------------------- removal

    public void RemoveWhere(Func<PlaylistItem, bool> predicate)
    {
        var current = Current;
        int removed = _items.RemoveAll(i => predicate(i));
        if (removed == 0) return;
        _shuffleOrder.RemoveAll(i => !_items.Contains(i));
        CurrentIndex = current is null ? -1 : _items.IndexOf(current);
        RaiseChanged();
    }

    public void RemoveSelected() => RemoveWhere(i => i.Selected);
    public void Crop() => RemoveWhere(i => !i.Selected);

    public void RemoveDuplicates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RemoveWhere(i => !seen.Add(i.Path));
    }

    /// <summary>Removes local files that no longer exist (URLs are kept).</summary>
    public void RemoveDead() => RemoveWhere(i => !Audio.PlaybackEngine.IsUrl(i.Path) && !File.Exists(i.Path));

    // ---------------------------------------------------------------- reordering

    /// <summary>Moves the selected items by <paramref name="delta"/> rows, keeping their spacing.</summary>
    public void MoveSelected(int delta)
    {
        if (delta == 0) return;
        var selected = _items.Where(i => i.Selected).ToList();
        if (selected.Count == 0) return;
        int first = _items.IndexOf(selected[0]), last = _items.IndexOf(selected[^1]);
        delta = Math.Clamp(delta, -first, _items.Count - 1 - last);
        if (delta == 0) return;
        var current = Current;
        var result = new PlaylistItem?[_items.Count];
        foreach (var s in selected) result[_items.IndexOf(s) + delta] = s;
        using var rest = _items.Where(i => !i.Selected).GetEnumerator();
        for (int k = 0; k < result.Length; k++)
            if (result[k] is null && rest.MoveNext()) result[k] = rest.Current;
        _items.Clear();
        _items.AddRange(result!);
        CurrentIndex = current is null ? -1 : _items.IndexOf(current);
        RaiseChanged();
    }

    public void Sort(SortKey key)
    {
        Func<PlaylistItem, string> by = key switch
        {
            SortKey.Title => i => i.DisplayTitle,
            SortKey.FileName => i => System.IO.Path.GetFileName(i.Path),
            _ => i => i.Path,
        };
        Reorder(_items.OrderBy(by, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    public void Reverse() => Reorder(Enumerable.Reverse(_items).ToList());

    public void Randomize() => Reorder(_items.OrderBy(_ => _random.Next()).ToList());

    private void Reorder(List<PlaylistItem> order)
    {
        var current = Current;
        _items.Clear();
        _items.AddRange(order);
        CurrentIndex = current is null ? -1 : _items.IndexOf(current);
        RaiseChanged();
    }
}
