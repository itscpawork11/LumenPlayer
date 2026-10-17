using LibVLCSharp.Shared;

namespace VlcRebuild.Media;

public sealed class MediaEngine : IDisposable
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private bool _disposed;

    public MediaPlayer? Player => _mediaPlayer;
    public bool IsInitialized => _libVlc is not null;

    public event EventHandler? Playing;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler? EndReached;
    public event EventHandler<string>? ErrorOccurred;

    public void Initialize()
    {
        if (_libVlc is not null) return;
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.Playing += (_, _) => Playing?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.Paused += (_, _) => Paused?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.Stopped += (_, _) => Stopped?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.EndReached += (_, _) => EndReached?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.EncounteredError += (_, _) => ErrorOccurred?.Invoke(this, "Playback error");
    }

    public bool Open(string path)
    {
        EnsureInitialized();
        if (_mediaPlayer is null || _libVlc is null) return false;
        _currentMedia?.Dispose();
        _currentMedia = new Media(_libVlc, path, FromType.FromPath);
        _mediaPlayer.Media = _currentMedia;
        return true;
    }

    public void Play() => _mediaPlayer?.Play();
    public void Pause() => _mediaPlayer?.Pause();
    public void Stop() => _mediaPlayer?.Stop();

    public void Seek(long milliseconds)
    {
        if (_mediaPlayer is null) return;
        _mediaPlayer.Time = milliseconds;
    }

    public void SetVolume(int volume)
    {
        if (_mediaPlayer is null) return;
        _mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
    }

    public void ToggleMute()
    {
        if (_mediaPlayer is null) return;
        _mediaPlayer.Mute = !_mediaPlayer.Mute;
    }

    public long Time => _mediaPlayer?.Time ?? 0;
    public long Length => _mediaPlayer?.Length ?? 0;
    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;

    private void EnsureInitialized()
    {
        if (_libVlc is null) Initialize();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _currentMedia?.Dispose();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
    }
}
