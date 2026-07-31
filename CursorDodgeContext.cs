using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CursorDodge;

internal sealed class CursorDodgeContext : ApplicationContext
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "CursorDodge";
    private const int TrayIconTextMaxLength = 63;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _autoStartItem;

    private readonly LowLevelMouseHook _mouseHook = new();
    private readonly LowLevelKeyboardHook _keyboardHook = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly object _stateLock = new();

    private readonly string _configPath = AppSettings.GetConfigPath();
    private AppSettings _settings;

    private volatile bool _isEnabled;
    private volatile bool _isArmed;
    private DateTime _armedUntilUtc;
    private int _isAnimating;

    public CursorDodgeContext()
    {
        _settings = AppSettings.Load(_configPath);
        _isEnabled = true;

        _toggleItem = new("無効化", null, OnToggleEnabled);
        _autoStartItem = new("自動起動", null, OnToggleAutoStart)
        {
            Checked = IsAutoStartEnabled()
        };
        var settingItem = new ToolStripMenuItem("設定", null, OnOpenSettings);
        var exitItem = new ToolStripMenuItem("終了", null, OnExitClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(settingItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "CursorDodge",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OnOpenSettings();

        _mouseHook.MouseReleased += OnMouseReleased;
        _keyboardHook.KeyDowned += OnKeyDowned;

        StartHooks();
    }

    public AppSettings CurrentSettings
    {
        get
        {
            lock (_stateLock)
            {
                return new AppSettings
                {
                    DistancePx = _settings.DistancePx,
                    AngleDegrees = _settings.AngleDegrees,
                    FrameRate = _settings.FrameRate,
                    MoveDurationMs = _settings.MoveDurationMs,
                    ArmTimeoutMs = _settings.ArmTimeoutMs
                };
            }
        }
    }

    public void ApplySettings(AppSettings next)
    {
        if (next is null)
            return;

        next.Normalize();
        lock (_stateLock)
        {
            _settings = next;
        }
        next.Save(_configPath);
    }

    private void StartHooks()
    {
        if (!_isEnabled) return;

        try
        {
            _mouseHook.Start();
            _keyboardHook.Start();
            SetToggleText();
            SetTrayTooltip("CursorDodge (有効)");
        }
        catch
        {
            _isEnabled = false;
            SetToggleText();
            SetTrayTooltip("CursorDodge (フック失敗)");
        }
    }

    private void StopHooks()
    {
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        SetToggleText();
        SetTrayTooltip("CursorDodge (無効)");
        _isArmed = false;
        Interlocked.Exchange(ref _isAnimating, 0);
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _isEnabled = !_isEnabled;
        if (_isEnabled)
        {
            StartHooks();
        }
        else
        {
            StopHooks();
        }
    }

    private void OnToggleAutoStart(object? sender, EventArgs e)
    {
        var next = !IsAutoStartEnabled();
        SetAutoStartEnabled(next);
        _autoStartItem.Checked = next;
    }

    private void OnOpenSettings(object? sender = null, EventArgs? e = null)
    {
        using var form = new SettingsForm(this);
        form.ShowDialog();
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        ExitThread();
    }

    private void SetToggleText()
    {
        _toggleItem.Text = _isEnabled ? "無効化" : "有効化";
    }

    private void SetTrayTooltip(string text)
    {
        _trayIcon.Text = text.Length <= TrayIconTextMaxLength ? text : text[..TrayIconTextMaxLength];
    }

    private bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
        return key?.GetValue(RunValueName) is string value && value.Length > 0;
    }

    private void SetAutoStartEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunRegistryPath, true);
        if (key is null)
            return;

        if (enabled)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            key.SetValue(RunValueName, $"\"{exePath}\"", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(RunValueName, false);
        }
    }

    private void OnMouseReleased(int messageCode)
    {
        if (!_isEnabled) return;
        lock (_stateLock)
        {
            _isArmed = true;
            _armedUntilUtc = DateTime.UtcNow.AddMilliseconds(_settings.ArmTimeoutMs);
        }
    }

    private async void OnKeyDowned(int vkCode)
    {
        if (!_isEnabled)
            return;

        bool shouldDodge;
        AppSettings snapshot;

        lock (_stateLock)
        {
            shouldDodge = _isArmed && DateTime.UtcNow <= _armedUntilUtc;
            if (shouldDodge)
                _isArmed = false;

            snapshot = new AppSettings
            {
                DistancePx = _settings.DistancePx,
                AngleDegrees = _settings.AngleDegrees,
                FrameRate = _settings.FrameRate,
                MoveDurationMs = _settings.MoveDurationMs,
                ArmTimeoutMs = _settings.ArmTimeoutMs
            };
        }

        if (!shouldDodge)
            return;

        if (Interlocked.CompareExchange(ref _isAnimating, 1, 0) != 0)
            return;

        try
        {
            await DodgeCursorAsync(snapshot, _shutdownCts.Token).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
        finally
        {
            Interlocked.Exchange(ref _isAnimating, 0);
        }
    }

    private async Task DodgeCursorAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        settings.Normalize();

        if (!NativeMethods.GetCursorPos(out var current))
            return;

        double rad = settings.AngleDegrees * Math.PI / 180d;
        int distance = settings.DistancePx;

        double dx = Math.Sin(rad) * distance;
        double dy = -Math.Cos(rad) * distance;

        var destination = new NativeMethods.POINT
        {
            X = (int)Math.Round(current.X + dx),
            Y = (int)Math.Round(current.Y + dy)
        };
        ClampToVirtualScreen(ref destination);

        int frames = Math.Max(1, (int)Math.Round(settings.MoveDurationMs / 1000.0 * settings.FrameRate));
        int delay = Math.Max(1, 1000 / settings.FrameRate);

        for (int i = 1; i <= frames; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            double t = i / (double)frames;
            double x = current.X + (destination.X - current.X) * t;
            double y = current.Y + (destination.Y - current.Y) * t;
            NativeMethods.SetCursorPos((int)Math.Round(x), (int)Math.Round(y));

            if (i < frames)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static void ClampToVirtualScreen(ref NativeMethods.POINT point)
    {
        var screen = SystemInformation.VirtualScreen;
        point.X = Math.Clamp(point.X, screen.Left, screen.Right - 1);
        point.Y = Math.Clamp(point.Y, screen.Top, screen.Bottom - 1);
    }

    protected override void ExitThreadCore()
    {
        _shutdownCts.Cancel();
        StopHooks();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
