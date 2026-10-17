using System.Windows;
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
        Closed += (_, _) => _mediaEngine.Dispose();
    }
}
