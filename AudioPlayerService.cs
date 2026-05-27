using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CrawtaDesktop;

sealed class AudioPlayerService : IDisposable
{
    private Process? _process;
    private string? _filePath;
    private string? _currentTrackName;
    private double _startOffset;
    private DateTime _playStartUtc;
    private double _duration;
    private bool _isPaused;
    private double _pausedAt;

    public event Action? PositionChanged;
    public event Action? PlaybackStopped;

    public string? CurrentTrackName => _currentTrackName;
    public bool IsPlaying => _process is { HasExited: false } && !_isPaused;

    public double CurrentTimeSeconds
    {
        get
        {
            if (_filePath == null)
            {
                return 0;
            }

            if (_isPaused)
            {
                return _pausedAt;
            }

            if (_process is { HasExited: false })
            {
                return _startOffset + (DateTime.UtcNow - _playStartUtc).TotalSeconds;
            }

            return _startOffset;
        }
    }

    public double TotalTimeSeconds => _duration;

    public async Task PlayAsync(string filePath, string trackName)
    {
        if (_currentTrackName == trackName && _filePath != null)
        {
            if (IsPlaying)
            {
                Pause();
            }
            else if (_isPaused)
            {
                Resume();
            }
            else
            {
                await StartPlaybackAsync(_startOffset);
            }

            PositionChanged?.Invoke();
            return;
        }

        StopInternal(resetTrack: true);
        _filePath = filePath;
        _currentTrackName = trackName;
        _duration = await ProbeDurationAsync(filePath);
        await StartPlaybackAsync(0);
        PositionChanged?.Invoke();
    }

    public void TogglePlayPause()
    {
        if (_filePath == null)
        {
            return;
        }

        if (IsPlaying)
        {
            Pause();
        }
        else if (_isPaused)
        {
            Resume();
        }

        PositionChanged?.Invoke();
    }

    public async void SeekPercent(double percent)
    {
        if (_filePath == null || _duration <= 0)
        {
            return;
        }

        percent = Math.Clamp(percent, 0, 1);
        var target = _duration * percent;
        _startOffset = target;
        _pausedAt = target;
        _isPaused = false;

        if (_process is { HasExited: false })
        {
            await StartPlaybackAsync(target);
        }

        PositionChanged?.Invoke();
    }

    private void Pause()
    {
        if (_process is not { HasExited: false })
        {
            return;
        }

        _pausedAt = CurrentTimeSeconds;
        _isPaused = true;
        KillProcess();
    }

    private async void Resume()
    {
        if (_filePath == null)
        {
            return;
        }

        _isPaused = false;
        await StartPlaybackAsync(_pausedAt);
        PositionChanged?.Invoke();
    }

    private async Task StartPlaybackAsync(double startSeconds)
    {
        if (_filePath == null)
        {
            return;
        }

        KillProcess();
        _startOffset = startSeconds;
        _playStartUtc = DateTime.UtcNow;
        _isPaused = false;

        var ffplay = ResolveTool("ffplay");
        var args = $"-nodisp -autoexit -loglevel quiet -ss {startSeconds.ToString(CultureInfo.InvariantCulture)} \"{_filePath}\"";

        _process = Process.Start(new ProcessStartInfo(ffplay, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (_process == null)
        {
            throw new InvalidOperationException("Could not start ffplay.");
        }

        _ = Task.Run(async () =>
        {
            await _process.WaitForExitAsync();
            if (!_isPaused && _process.ExitCode == 0)
            {
                _startOffset = _duration;
                PlaybackStopped?.Invoke();
            }

            PositionChanged?.Invoke();
        });
    }

    private static async Task<double> ProbeDurationAsync(string filePath)
    {
        var ffprobe = ResolveTool("ffprobe");
        var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"";

        using var process = Process.Start(new ProcessStartInfo(ffprobe, args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null)
        {
            return 0;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 0;
    }

    private static string ResolveTool(string tool)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return tool;
        }

        var homebrew = Path.Combine("/opt/homebrew/bin", tool);
        if (File.Exists(homebrew))
        {
            return homebrew;
        }

        var usrLocal = Path.Combine("/usr/local/bin", tool);
        if (File.Exists(usrLocal))
        {
            return usrLocal;
        }

        return tool;
    }

    private void KillProcess()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.Dispose();
        }

        _process = null;
    }

    private void StopInternal(bool resetTrack)
    {
        KillProcess();
        if (resetTrack)
        {
            _filePath = null;
            _currentTrackName = null;
            _duration = 0;
            _startOffset = 0;
            _pausedAt = 0;
            _isPaused = false;
        }
    }

    public void Dispose()
    {
        StopInternal(resetTrack: true);
    }
}
