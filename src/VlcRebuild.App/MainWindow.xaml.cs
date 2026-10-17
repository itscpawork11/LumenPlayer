using System.IO;
using System.Windows;
using Microsoft.Win32;
using VlcRebuild.Media;

namespace LumenPlayer;

public partial class MainWindow : Window
{
    private readonly MediaEngine _mediaEngine = new();

    public MainWindow()
    {
        InitializeComponent();

        _mediaEngine.Initialize();
        VideoSurface.MediaPlayer = _mediaEngine.Player;

        _mediaEngine.Playing += (_, _) => Dispatcher.Invoke(() => StatusText.Text = "Playing");
        _mediaEngine.Paused += (_, _) => Dispatcher.Invoke(() => StatusText.Text = "Paused");
        _mediaEngine.Stopped += (_, _) => Dispatcher.Invoke(() => StatusText.Text = "Stopped");
        _mediaEngine.EndReached += (_, _) => Dispatcher.Invoke(() => StatusText.Text = "Playback ended");
        _mediaEngine.ErrorOccurred += (_, _) => Dispatcher.Invoke(() => StatusText.Text = "Playback error");

        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        VideoSurface.MediaPlayer = null;
        _mediaEngine.Dispose();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open media file",
            Filter = "Media files|*.mp4;*.mkv;*.avi;*.mov;*.mp3;*.wav;*.m4a;*.flac|All files|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            StatusText.Text = $"Opening {Path.GetFileName(dialog.FileName)}...";

            if (!_mediaEngine.Open(dialog.FileName))
            {
                StatusText.Text = "Failed to open media file.";
                return;
            }

            _mediaEngine.Play();
            StatusText.Text = $"Playing {Path.GetFileName(dialog.FileName)}";
        }
        catch
        {
            StatusText.Text = "Failed to open media file.";
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mediaEngine.Play();
            StatusText.Text = "Playing";
        }
        catch
        {
            StatusText.Text = "Unable to play media.";
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mediaEngine.Pause();
            StatusText.Text = "Paused";
        }
        catch
        {
            StatusText.Text = "Unable to pause media.";
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mediaEngine.Stop();
            StatusText.Text = "Stopped";
        }
        catch
        {
            StatusText.Text = "Unable to stop media.";
        }
    }
}
