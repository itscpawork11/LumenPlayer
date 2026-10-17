using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using Microsoft.Win32;
using VlcRebuild.Media;
using VlcRebuild.Playlist;

namespace LumenPlayer;

public partial class MainWindow : Window
{
    private MediaEngine? _mediaEngine;
    private readonly PlaylistService _playlist = new();
    private readonly DispatcherTimer _progressTimer;

    private readonly List<string> _recentFiles = new();
    private readonly Dictionary<string, long> _resumePositions = new();
    private bool _isUpdatingRecent;
    private long _pendingResumePosition;
    private long _abA = -1;
    private long _abB = -1;
    private bool _abEnabled;
    private bool _isSeeking;
    private bool _isUpdatingTracks;
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private bool _previousTopmost;

    private readonly string _settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenPlayer");

    public MainWindow()
    {
        InitializeComponent();

        LoadRecentFiles();
        LoadResumePositions();

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += ProgressTimer_Tick;

        PlaylistBox.ItemsSource = _playlist.Items;

        VolumeSlider.ValueChanged += (_, _) => ApplyVolume();
        ApplyVolume();

        Closed += OnClosed;
    }

    private MediaEngine EnsureMediaEngine()
    {
        if (_mediaEngine is not null) return _mediaEngine;

        _mediaEngine = new MediaEngine();
        _mediaEngine.Initialize();
        VideoSurface.MediaPlayer = _mediaEngine.Player;

        _mediaEngine.Playing += (_, _) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = "Playing";
            AnimateEmptyStateOut();
            RefreshTracks();
            _progressTimer.Start();

            if (_pendingResumePosition > 0)
            {
                _mediaEngine.Seek(_pendingResumePosition);
                _pendingResumePosition = 0;
                StatusText.Text = "Resumed";
            }
        });
        _mediaEngine.Paused += (_, _) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = "Paused";
            _progressTimer.Stop();
        });
        _mediaEngine.Stopped += (_, _) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = "Stopped";
            _progressTimer.Stop();
            UpdateProgress();
        });
        _mediaEngine.EndReached += (_, _) => Dispatcher.Invoke(() =>
        {
            _progressTimer.Stop();
            if (LoopCheckBox.IsChecked == true)
            {
                _mediaEngine.Seek(0);
                _mediaEngine.Play();
                StatusText.Text = "Looping";
                return;
            }

            if (AutoNextCheckBox.IsChecked == true && _playlist.Next())
            {
                PlaylistBox.SelectedItem = _playlist.Current;
                OpenCurrent();
            }
            else
            {
                StatusText.Text = "Playback ended";
            }
        });
        _mediaEngine.ErrorOccurred += (_, _) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = "Playback error";
            _progressTimer.Stop();
        });

        return _mediaEngine;
    }

    private void AnimateEmptyStateOut()
    {
        if (EmptyState.Visibility != Visibility.Visible) return;
        var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => EmptyState.Visibility = Visibility.Collapsed;
        EmptyState.BeginAnimation(UIElement.OpacityProperty, fade);
        VideoSurface.Visibility = Visibility.Visible;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_mediaEngine is not null)
        {
            _resumePositions[_playlist.Current?.FilePath ?? string.Empty] = _mediaEngine.Time;
            SaveResumePositions();
        }

        _progressTimer.Stop();
        VideoSurface.MediaPlayer = null;
        _mediaEngine?.Dispose();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open media file",
            Filter = "Media files|*.mp4;*.mkv;*.avi;*.mov;*.mp3;*.wav;*.m4a;*.flac|All files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var hadItems = _playlist.Count > 0;
        _playlist.AddRange(dialog.FileNames);

        if (!hadItems && _playlist.Count > 0)
        {
            _playlist.Select(0);
            PlaylistBox.SelectedItem = _playlist.Current;
            OpenCurrent();
        }
        else
        {
            StatusText.Text = $"Added {dialog.FileNames.Length} file(s).";
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_playlist.Current is null && _playlist.Count > 0)
            {
                _playlist.Select(0);
                PlaylistBox.SelectedItem = _playlist.Current;
                OpenCurrent();
                return;
            }

            _mediaEngine?.Play();
            if (_mediaEngine is not null) StatusText.Text = "Playing";
        }
        catch
        {
            StatusText.Text = "Unable to play media.";
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine is null) return;
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
        if (_mediaEngine is null) return;
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

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playlist.Previous()) return;
        PlaylistBox.SelectedItem = _playlist.Current;
        OpenCurrent();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playlist.Next()) return;
        PlaylistBox.SelectedItem = _playlist.Current;
        OpenCurrent();
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine is null) return;
        _mediaEngine.ToggleMute();
        StatusText.Text = _mediaEngine.Player?.Mute == true ? "Muted" : "Unmuted";
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void LoadSubtitleButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open subtitle file",
            Filter = "Subtitle files|*.srt;*.sub;*.ass;*.ssa;*.txt|All files|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        if (_mediaEngine.AddSubtitleFile(dialog.FileName))
        {
            StatusText.Text = $"Subtitle loaded: {Path.GetFileName(dialog.FileName)}";
            RefreshSubtitleTracks();
        }
        else
        {
            StatusText.Text = "Failed to load subtitle file.";
        }
    }

    private void ClearPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        _mediaEngine.Stop();
        _playlist.Clear();
        PlaylistBox.ItemsSource = _playlist.Items;
        ResetPlaybackUi();
        StatusText.Text = "Playlist cleared.";
    }

    private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not PlaylistItem item) return;
        _playlist.Remove(item);
        PlaylistBox.ItemsSource = _playlist.Items;
        if (_playlist.Current is not null)
        {
            PlaylistBox.SelectedItem = _playlist.Current;
        }
        StatusText.Text = "Removed selected item.";
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not PlaylistItem item) return;
        if (_playlist.MoveUp(item))
        {
            PlaylistBox.ItemsSource = _playlist.Items;
            PlaylistBox.SelectedItem = item;
        }
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not PlaylistItem item) return;
        if (_playlist.MoveDown(item))
        {
            PlaylistBox.ItemsSource = _playlist.Items;
            PlaylistBox.SelectedItem = item;
        }
    }

    private void PlaylistBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not PlaylistItem item) return;
        if (_playlist.Current == item) return;
        _playlist.Select(item);
        OpenCurrent();
    }

    private void PlaylistBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlaylistBox.SelectedItem is PlaylistItem)
        {
            OpenCurrent();
        }
    }

    private void OpenCurrent()
    {
        var item = _playlist.Current;
        if (item is null) return;

        try
        {
            var previous = _playlist.Current;
            if (previous is not null && _mediaEngine is not null)
            {
                _resumePositions[previous.FilePath] = _mediaEngine.Time;
                SaveResumePositions();
            }

            StatusText.Text = $"Opening {item.DisplayName}...";
            ResetTrackCombos();

            var engine = EnsureMediaEngine();
            if (!engine.Open(item.FilePath))
            {
                StatusText.Text = "Failed to open media file.";
                return;
            }

            _pendingResumePosition = _resumePositions.TryGetValue(item.FilePath, out var pos) ? pos : 0;
            engine.Play();
            StatusText.Text = $"Playing {item.DisplayName}";
            AddRecentFile(item.FilePath);
        }
        catch
        {
            StatusText.Text = "Failed to open media file.";
        }
    }

    private void LoadRecentFiles()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            var path = Path.Combine(_settingsDirectory, "recent.txt");
            if (!File.Exists(path)) return;

            foreach (var line in File.ReadAllLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line) && File.Exists(line) && !_recentFiles.Contains(line))
                {
                    _recentFiles.Add(line);
                }
            }

            _isUpdatingRecent = true;
            RecentFilesComboBox.Items.Clear();
            foreach (var file in _recentFiles)
            {
                RecentFilesComboBox.Items.Add(file);
            }
        }
        catch
        {
            // Keep startup fast; recent list is optional.
        }
        finally
        {
            _isUpdatingRecent = false;
        }
    }

    private void SaveRecentFiles()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            File.WriteAllLines(Path.Combine(_settingsDirectory, "recent.txt"), _recentFiles);
        }
        catch
        {
            // Ignore persistence errors.
        }
    }

    private void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _recentFiles.Remove(path);
        _recentFiles.Insert(0, path);
        if (_recentFiles.Count > 12) _recentFiles.RemoveRange(12, _recentFiles.Count - 12);

        _isUpdatingRecent = true;
        RecentFilesComboBox.Items.Clear();
        foreach (var file in _recentFiles)
        {
            RecentFilesComboBox.Items.Add(file);
        }
        _isUpdatingRecent = false;
        SaveRecentFiles();
    }

    private void LoadResumePositions()
    {
        try
        {
            var path = Path.Combine(_settingsDirectory, "positions.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
            if (data is not null)
            {
                foreach (var pair in data) _resumePositions[pair.Key] = pair.Value;
            }
        }
        catch
        {
            // Ignore corrupt state.
        }
    }

    private void SaveResumePositions()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            var json = JsonSerializer.Serialize(_resumePositions);
            File.WriteAllText(Path.Combine(_settingsDirectory, "positions.json"), json);
        }
        catch
        {
            // Ignore persistence errors.
        }
    }

    private void RecentFilesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingRecent) return;
        if (RecentFilesComboBox.SelectedItem is not string path || !File.Exists(path)) return;

        _playlist.Clear();
        _playlist.Add(path);
        _playlist.Select(0);
        PlaylistBox.ItemsSource = _playlist.Items;
        PlaylistBox.SelectedItem = _playlist.Current;
        OpenCurrent();
    }

    private void SetAButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine is null) return;
        _abA = _mediaEngine.Time;
        _abEnabled = false;
        StatusText.Text = $"A set: {FormatTime(_abA)}";
    }

    private void SetBButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine is null || _abA < 0) return;
        var b = _mediaEngine.Time;
        if (b <= _abA)
        {
            StatusText.Text = "B must be after A.";
            return;
        }
        _abB = b;
        _abEnabled = true;
        StatusText.Text = $"A-B repeat: {FormatTime(_abA)} to {FormatTime(_abB)}";
    }

    private void ClearAbButton_Click(object sender, RoutedEventArgs e)
    {
        _abA = -1;
        _abB = -1;
        _abEnabled = false;
        StatusText.Text = "A-B repeat cleared.";
    }

    private void SubtitleDelayMinus_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine?.Player is null) return;
        _mediaEngine.Player.SetSpuDelay(_mediaEngine.Player.SpuDelay - 500000);
        StatusText.Text = "Subtitle delay -0.5s";
    }

    private void SubtitleDelayPlus_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine?.Player is null) return;
        _mediaEngine.Player.SetSpuDelay(_mediaEngine.Player.SpuDelay + 500000);
        StatusText.Text = "Subtitle delay +0.5s";
    }

    private void AudioDelayMinus_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine?.Player is null) return;
        _mediaEngine.Player.SetAudioDelay(_mediaEngine.Player.AudioDelay - 500000);
        StatusText.Text = "Audio delay -0.5s";
    }

    private void AudioDelayPlus_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine?.Player is null) return;
        _mediaEngine.Player.SetAudioDelay(_mediaEngine.Player.AudioDelay + 500000);
        StatusText.Text = "Audio delay +0.5s";
    }

    private void AspectRatioButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine?.Player is null) return;
        var current = _mediaEngine.Player.AspectRatio;
        _mediaEngine.Player.AspectRatio = string.Equals(current, "16:9", StringComparison.Ordinal) ? null : "16:9";
        StatusText.Text = $"Aspect ratio: {_mediaEngine.Player.AspectRatio ?? "default"}";
    }

    private void MediaInfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine is null) return;
        var length = TimeSpan.FromMilliseconds(_mediaEngine.Length);
        var info = $"Duration: {length:hh\\:mm\\:ss}\n" +
                   $"Audio tracks: {_mediaEngine.GetAudioTracks().Length}\n" +
                   $"Subtitle tracks: {_mediaEngine.GetSubtitleTracks().Length}\n" +
                   $"Volume: {VolumeSlider.Value:0}";
        System.Windows.MessageBox.Show(this, info, "Media info", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e) => OpenButton_Click(sender, e);

    private void RecentButton_Click(object sender, RoutedEventArgs e)
    {
        RecentFilesComboBox.IsDropDownOpen = true;
    }
    private void ResetPlaybackUi()
    {
        DurationText.Text = "00:00";
        SeekSlider.IsEnabled = false;
        EmptyState.Visibility = Visibility.Visible;
        VideoSurface.Visibility = Visibility.Collapsed;
        EmptyState.Opacity = 1;
        ResetTrackCombos();
    }

    private void ResetTrackCombos()
    {
        AudioTrackComboBox.ItemsSource = null;
        SubtitleTrackComboBox.ItemsSource = null;
    }

    private void RefreshTracks()
    {
        var engine = _mediaEngine;
        if (engine is null) return;
        try
        {
            _isUpdatingTracks = true;

            var audioTracks = engine.GetAudioTracks();
            AudioTrackComboBox.ItemsSource = audioTracks;
            AudioTrackComboBox.DisplayMemberPath = "Name";
            AudioTrackComboBox.SelectedValuePath = "Id";
            if (audioTracks.Length > 0)
            {
                AudioTrackComboBox.SelectedIndex = 0;
            }

            RefreshSubtitleTracks();
        }
        finally
        {
            _isUpdatingTracks = false;
        }
    }

    private void RefreshSubtitleTracks(MediaEngine engine)
    {
        var subtitleTracks = engine.GetSubtitleTracks();
        SubtitleTrackComboBox.ItemsSource = subtitleTracks;
        SubtitleTrackComboBox.DisplayMemberPath = "Name";
        SubtitleTrackComboBox.SelectedValuePath = "Id";

        var currentSpu = engine.Player?.Spu ?? -1;
        var index = Array.FindIndex(subtitleTracks, t => t.Id == currentSpu);
        SubtitleTrackComboBox.SelectedIndex = index >= 0 ? index : (subtitleTracks.Length > 0 ? 0 : -1);
    }

    private void AudioTrackComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingTracks) return;
        if (AudioTrackComboBox.SelectedItem is TrackDescription track)
        {
            _mediaEngine?.SetAudioTrack(track.Id);
        }
    }

    private void SubtitleTrackComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingTracks) return;
        if (SubtitleTrackComboBox.SelectedItem is TrackDescription track)
        {
            _mediaEngine?.SetSubtitleTrack(track.Id);
        }
    }

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSeeking = true;
    }

    private void SeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_mediaEngine is not null && _mediaEngine.Length > 0)
        {
            var ms = (long)(SeekSlider.Value / SeekSlider.Maximum * _mediaEngine.Length);
            _mediaEngine.Seek(ms);
        }
        _isSeeking = false;
        UpdateProgress();
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isSeeking || _mediaEngine is null || _mediaEngine.Length <= 0) return;
        var ms = (long)(SeekSlider.Value / SeekSlider.Maximum * _mediaEngine.Length);
        CurrentTimeText.Text = FormatTime(ms);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ApplyVolume();
    }

    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_abEnabled && _abB > _abA && _mediaEngine is not null && _mediaEngine.Time >= _abB)
        {
            _mediaEngine.Seek(_abA);
        }

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var engine = _mediaEngine;
        if (engine is null)
        {
            SeekSlider.IsEnabled = false;
            SeekSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
            DurationText.Text = "00:00";
            return;
        }

        var length = engine.Length;
        var time = engine.Time;

        if (length > 0)
        {
            SeekSlider.IsEnabled = true;
            if (!_isSeeking)
            {
                SeekSlider.Value = time * SeekSlider.Maximum / length;
            }
        }
        else
        {
            SeekSlider.IsEnabled = false;
            SeekSlider.Value = 0;
        }

        CurrentTimeText.Text = FormatTime(time);
        DurationText.Text = FormatTime(length);
    }

    private void ApplyVolume()
    {
        _mediaEngine?.SetVolume((int)VolumeSlider.Value);
        VolumeText.Text = ((int)VolumeSlider.Value).ToString();
    }

    private static string FormatTime(long ms)
    {
        if (ms < 0) ms = 0;
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            _previousTopmost = Topmost;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
            _isFullscreen = true;
        }
        else
        {
            Topmost = _previousTopmost;
            WindowStyle = _previousWindowStyle;
            ResizeMode = _previousResizeMode;
            WindowState = _previousWindowState;
            _isFullscreen = false;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            if (_mediaEngine.IsPlaying) _mediaEngine.Pause(); else _mediaEngine.Play();
            e.Handled = true;
        }
        else if (e.Key == Key.M)
        {
            _mediaEngine.ToggleMute();
            StatusText.Text = _mediaEngine.Player?.Mute == true ? "Muted" : "Unmuted";
            e.Handled = true;
        }
        else if (e.Key == Key.F || e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            _mediaEngine.Seek(Math.Max(0, _mediaEngine.Time - 10000));
            UpdateProgress();
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            _mediaEngine.Seek(_mediaEngine.Time + 10000);
            UpdateProgress();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 5);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 5);
            e.Handled = true;
        }
    }

    private void SpeedComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_mediaEngine is null || SpeedComboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        var text = item.Content?.ToString() ?? "1.0x";
        if (float.TryParse(text.Replace("x", "", StringComparison.Ordinal), out var rate))
        {
            _mediaEngine.SetPlaybackRate(rate);
            StatusText.Text = $"Speed: {text}";
        }
    }

    private void SnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaEngine is null)
        {
            StatusText.Text = "No media loaded.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save snapshot",
            Filter = "PNG image|*.png|JPEG image|*.jpg",
            FileName = "snapshot.png"
        };

        if (dialog.ShowDialog() != true) return;

        StatusText.Text = _mediaEngine.TakeSnapshot(dialog.FileName)
            ? $"Snapshot saved: {Path.GetFileName(dialog.FileName)}"
            : "Snapshot failed.";
    }

    private void AlwaysOnTopButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        StatusText.Text = Topmost ? "Always on top: on" : "Always on top: off";
    }

    private void MiniModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;

        if (Width > 520)
        {
            Width = 420;
            Height = 260;
            StatusText.Text = "Mini mode";
        }
        else
        {
            Width = 1280;
            Height = 800;
            StatusText.Text = "Normal mode";
        }
    }
    private void Window_Drop(object? sender, DragEventArgs e)
    {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files is null || files.Length == 0) return;

        var hadItems = _playlist.Count > 0;
        _playlist.AddRange(files);
        PlaylistBox.ItemsSource = _playlist.Items;

        if (!hadItems && _playlist.Count > 0)
        {
            _playlist.Select(0);
            PlaylistBox.SelectedItem = _playlist.Current;
            OpenCurrent();
        }
        else
        {
            StatusText.Text = $"Added {files.Length} file(s).";
        }
    }
}
