using System.Collections.ObjectModel;

namespace VlcRebuild.Playlist;
{
    public string FilePath { get; }
    public string DisplayName { get; }

    public PlaylistItem(string filePath)
    {
        FilePath = filePath;
        DisplayName = Path.GetFileName(filePath);
    }
}

public sealed class PlaylistService
{
    private readonly ObservableCollection<PlaylistItem> _items = new();

    public ObservableCollection<PlaylistItem> Items => _items;

    public int CurrentIndex { get; private set; } = -1;

    public PlaylistItem? Current => CurrentIndex >= 0 && CurrentIndex < _items.Count ? _items[CurrentIndex] : null;

    public int Count => _items.Count;

    public void Add(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        _items.Add(new PlaylistItem(filePath));
    }

    public void AddRange(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths) Add(path);
    }

    public bool Remove(PlaylistItem item)
    {
        var index = _items.IndexOf(item);
        if (index < 0) return false;

        _items.RemoveAt(index);
        if (_items.Count == 0)
        {
            CurrentIndex = -1;
        }
        else if (index < CurrentIndex)
        {
            CurrentIndex--;
        }
        else if (index == CurrentIndex)
        {
            CurrentIndex = Math.Clamp(CurrentIndex, 0, _items.Count - 1);
        }
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        CurrentIndex = -1;
    }

    public bool MoveUp(PlaylistItem item)
    {
        var index = _items.IndexOf(item);
        if (index <= 0) return false;
        (_items[index - 1], _items[index]) = (_items[index], _items[index - 1]);
        if (CurrentIndex == index) CurrentIndex--;
        else if (CurrentIndex == index - 1) CurrentIndex++;
        return true;
    }

    public bool MoveDown(PlaylistItem item)
    {
        var index = _items.IndexOf(item);
        if (index < 0 || index >= _items.Count - 1) return false;
        (_items[index], _items[index + 1]) = (_items[index + 1], _items[index]);
        if (CurrentIndex == index) CurrentIndex++;
        else if (CurrentIndex == index + 1) CurrentIndex--;
        return true;
    }

    public bool Select(int index)
    {
        if (index < 0 || index >= _items.Count) return false;
        CurrentIndex = index;
        return true;
    }

    public bool Select(PlaylistItem item)
    {
        var index = _items.IndexOf(item);
        return index >= 0 && Select(index);
    }

    public bool Next()
    {
        if (_items.Count == 0) return false;
        CurrentIndex = (CurrentIndex + 1 + _items.Count) % _items.Count;
        return true;
    }

    public bool Previous()
    {
        if (_items.Count == 0) return false;
        CurrentIndex = (CurrentIndex - 1 + _items.Count) % _items.Count;
        return true;
    }
}
