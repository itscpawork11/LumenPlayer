using VlcRebuild.Playlist;
using Xunit;

namespace VlcRebuild.Tests;

public class PlaylistServiceTests
{
    [Fact]
    public void Add_IncreasesCount()
    {
        var playlist = new PlaylistService();
        playlist.Add("a.mp4");
        Assert.Equal(1, playlist.Count);
        Assert.Equal("a.mp4", playlist.Items[0].FilePath);
    }

    [Fact]
    public void Remove_SelectedItem_UpdatesCurrent()
    {
        var playlist = new PlaylistService();
        playlist.AddRange(new[] { "a.mp4", "b.mp4", "c.mp4" });
        playlist.Select(1);

        Assert.True(playlist.Remove(playlist.Items[1]));
        Assert.Equal(1, playlist.CurrentIndex);
        Assert.Equal("c.mp4", playlist.Current?.DisplayName);
    }

    [Fact]
    public void Next_WrapsAround()
    {
        var playlist = new PlaylistService();
        playlist.AddRange(new[] { "a.mp4", "b.mp4" });
        playlist.Select(1);

        Assert.True(playlist.Next());
        Assert.Equal(0, playlist.CurrentIndex);
    }

    [Fact]
    public void Previous_WrapsAround()
    {
        var playlist = new PlaylistService();
        playlist.AddRange(new[] { "a.mp4", "b.mp4" });
        playlist.Select(0);

        Assert.True(playlist.Previous());
        Assert.Equal(1, playlist.CurrentIndex);
    }

    [Fact]
    public void MoveUp_ChangesOrder()
    {
        var playlist = new PlaylistService();
        playlist.AddRange(new[] { "a.mp4", "b.mp4" });
        var item = playlist.Items[1];

        Assert.True(playlist.MoveUp(item));
        Assert.Equal("b.mp4", playlist.Items[0].DisplayName);
    }
}
