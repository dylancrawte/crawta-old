using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;

namespace CrawtaDesktop;

public partial class MainWindow : Window
{
    private readonly AudioPlayerService _audio = new();
    private bool _isSliderSyncing;
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainWindow()
    {
        InitializeComponent();
        FooterCopyText.Text = $"© {DateTime.Now.Year} Crawta. All rights reserved.";
        _progressTimer.Tick += (_, _) => UpdatePlayerProgress();
        _progressTimer.Start();
        _audio.PositionChanged += UpdatePlayerProgress;
        _audio.PlaybackStopped += OnPlaybackStopped;
        SizeChanged += (_, _) => UpdateResponsiveSizing();
        Closed += (_, _) =>
        {
            _progressTimer.Stop();
            _audio.Dispose();
        };
        UpdateResponsiveSizing();
    }

    private string AssetPath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", relativePath);

    private async void PlayAudio(string fileName, string trackName)
    {
        try
        {
            var path = AssetPath(Path.Combine("audio", fileName));
            if (!File.Exists(path))
            {
                await ShowMessage($"Audio file not found:\n{fileName}");
                return;
            }

            await _audio.PlayAsync(path, trackName);
            NowPlayingText.Text = trackName;
            PlayerToggleButton.Content = _audio.IsPlaying ? "Pause" : "Play";
            UpdatePlayerProgress();
        }
        catch (Exception ex)
        {
            await ShowMessage($"Could not play track:\n{ex.Message}\n\nInstall ffmpeg with: brew install ffmpeg");
        }
    }

    private void OnPlaybackStopped()
    {
        Dispatcher.UIThread.Post(() =>
        {
            PlayerToggleButton.Content = "Play";
            UpdatePlayerProgress();
        });
    }

    private async void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await ShowMessage($"Could not open link:\n{ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task ShowMessage(string message)
    {
        var dialog = new Window
        {
            Width = 480,
            Height = 180,
            Title = "Crawta",
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Width = 90
                    }
                }
            }
        };

        if (dialog.Content is StackPanel panel && panel.Children[1] is Button okButton)
        {
            okButton.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(this);
    }

    private void ScrollToTopClick(object? sender, RoutedEventArgs e) =>
        MainScrollViewer.Offset = default;

    private void ScrollToMusicClick(object? sender, RoutedEventArgs e) =>
        MainScrollViewer.Offset = new Avalonia.Vector(0, MusicSection.Bounds.Top);

    private void ScrollToNewsletterClick(object? sender, RoutedEventArgs e) =>
        MainScrollViewer.Offset = new Avalonia.Vector(0, NewsletterSection.Bounds.Top);

    private void PlayerToggleClick(object? sender, RoutedEventArgs e)
    {
        _audio.TogglePlayPause();
        PlayerToggleButton.Content = _audio.IsPlaying ? "Pause" : "Play";
        UpdatePlayerProgress();
    }

    private void PlayerProgressChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isSliderSyncing)
        {
            return;
        }

        _audio.SeekPercent(e.NewValue);
        UpdatePlayerProgress();
    }

    private void UpdatePlayerProgress()
    {
        var total = _audio.TotalTimeSeconds;
        var current = _audio.CurrentTimeSeconds;

        _isSliderSyncing = true;
        PlayerProgressSlider.Value = total > 0 ? Math.Clamp(current / total, 0, 1) : 0;
        _isSliderSyncing = false;
        PlayerTimeText.Text = $"{FormatTime(current)} / {FormatTime(total)}";

        if (_audio.CurrentTrackName != null)
        {
            NowPlayingText.Text = _audio.CurrentTrackName;
            PlayerToggleButton.Content = _audio.IsPlaying ? "Pause" : "Play";
        }
    }

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds))
        {
            return "0:00";
        }

        var total = (int)Math.Floor(seconds);
        return $"{total / 60}:{total % 60:D2}";
    }

    private void SubscribeClick(object? sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text?.Trim() ?? string.Empty;
        if (!email.Contains("@", StringComparison.Ordinal))
        {
            EmailBox.BorderBrush = Avalonia.Media.Brushes.IndianRed;
            return;
        }

        EmailBox.IsVisible = false;
        SubscribeButton.IsVisible = false;
        SignupNoteText.IsVisible = false;
        SuccessText.IsVisible = true;
    }

    private void UpdateResponsiveSizing()
    {
        var compact = Bounds.Width < 1040;
        var narrow = Bounds.Width < 760;
        var side = compact ? 20.0 : 48.0;
        var top = compact ? 40.0 : 56.0;

        MusicSection.Margin = new Avalonia.Thickness(side, top, side, 0);
        NewsletterSection.Margin = new Avalonia.Thickness(side, top, side, 0);
        HeroTitleText.FontSize = narrow ? 52 : compact ? 64 : 84;
        SectionTitleText.FontSize = narrow ? 32 : compact ? 38 : 44;
        NewsletterTitleText.FontSize = narrow ? 30 : compact ? 36 : 42;
    }

    private void PlayMotherEarthClick(object? sender, RoutedEventArgs e) => PlayAudio("mother-earth.mp3", "Mother Earth");
    private void PlayComplacentClick(object? sender, RoutedEventArgs e) => PlayAudio("complacent-demo.mp3", "Complacent");
    private void PlayWhiteNoiseClick(object? sender, RoutedEventArgs e) => PlayAudio("white-noise-demo.mp3", "White Noise");
    private void PlayIrisClick(object? sender, RoutedEventArgs e) => PlayAudio("iris-demo.mp3", "Iris");
    private void PlayProspectsClick(object? sender, RoutedEventArgs e) => PlayAudio("prospects-master.wav", "Prospects");
    private void PlayHyphMngoClick(object? sender, RoutedEventArgs e) => PlayAudio("hyph-mngo-edit.wav", "Hyph Mngo edit");
    private void PlayRequiemClick(object? sender, RoutedEventArgs e) => PlayAudio("requiem-master.wav", "Requiem");

    private void OpenProspectsSpotifyClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://open.spotify.com/track/2enVbhmGydv09lge4me8kp");

    private void OpenHyphMngoSoundCloudClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://soundcloud.com/nomoreparties/crawta-hype-mango?in=crawta/sets/crawta-releases");

    private void OpenRequiemSoundCloudClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://soundcloud.com/ducklandbristol/crawta-requiem-free-download?in=crawta/sets/crawta-releases");

    private void OpenMainSoundCloudClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://soundcloud.com/crawta");

    private void OpenMainSpotifyClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://open.spotify.com/artist/0uetZYfTX6zFzEVNT06pNZ?si=P_97fDHKT5aypSSkdAzx8Q");

    private void OpenInstagramClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://instagram.com/dyl.crawta");
}
