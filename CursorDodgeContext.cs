using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CursorDodge;

internal sealed class CursorDodgeContext : ApplicationContext
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "CursorDodge";
    private const int TrayIconTextMaxLength = 63;
    private const int VkBackspace = 0x08;
    private const int VkTab = 0x09;
    private const int VkEnter = 0x0D;
    private const int VkShift = 0x10;
    private const int VkCtrl = 0x11;
    private const int VkAlt = 0x12;
    private const int VkPause = 0x13;
    private const int VkCapsLock = 0x14;
    private const int VkEsc = 0x1B;
    private const int VkInsert = 0x2D;
    private const int VkDelete = 0x2E;
    private const int VkHome = 0x24;
    private const int VkEnd = 0x23;
    private const int VkPageUp = 0x21;
    private const int VkPageDown = 0x22;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;
    private const int VkPrint = 0x2A;
    private const int VkHelp = 0x2F;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkApps = 0x5D;
    private const int VkNum0 = 0x30;
    private const int VkNum9 = 0x39;
    private const int VkAlphaA = 0x41;
    private const int VkAlphaZ = 0x5A;
    private const int VkSpace = 0x20;
    private const int VkOem1 = 0xBA;
    private const int VkOem2 = 0xBF;

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
    private int _typedCount;
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
            Icon = LoadTrayIcon(),
            Text = "CursorDodge",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OnOpenSettings();

        _mouseHook.MouseReleased += OnMouseReleased;
        _keyboardHook.KeyDowned += OnKeyDowned;

        StartHooks();
    }

    private static Icon LoadTrayIcon()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("CursorDodge.Resources.CursorDodge.ico");
        if (stream is null)
        {
            return SystemIcons.Application;
        }

        return new Icon(stream);
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
                    ArmTimeoutMs = _settings.ArmTimeoutMs,
                    MinCharsToTrigger = _settings.MinCharsToTrigger
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
            _typedCount = 0;
        }
    }

    private async void OnKeyDowned(int vkCode)
    {
        if (!_isEnabled)
            return;

        bool shouldDodge;
        bool isCountedChar;
        AppSettings snapshot;

        lock (_stateLock)
        {
            bool armExpired = DateTime.UtcNow > _armedUntilUtc;
            if (!_isArmed || armExpired)
            {
                if (armExpired)
                    _isArmed = false;
                return;
            }

            isCountedChar = IsTypingKey(vkCode);
            if (!isCountedChar)
            {
                return;
            }

            _typedCount += 1;
            shouldDodge = _typedCount >= _settings.MinCharsToTrigger;
            if (shouldDodge)
            {
                _isArmed = false;
            }

            snapshot = new AppSettings
            {
                DistancePx = _settings.DistancePx,
                AngleDegrees = _settings.AngleDegrees,
                FrameRate = _settings.FrameRate,
                MoveDurationMs = _settings.MoveDurationMs,
                ArmTimeoutMs = _settings.ArmTimeoutMs,
                MinCharsToTrigger = _settings.MinCharsToTrigger
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

    private static bool IsTypingKey(int vkCode)
    {
        return vkCode switch
        {
            >= VkNum0 and <= VkNum9 => true,
            >= VkAlphaA and <= VkAlphaZ => true,
            VkSpace => true,
            0x60 => true,
            0x61 => true,
            0x62 => true,
            0x63 => true,
            0x64 => true,
            0x65 => true,
            0x66 => true,
            0x67 => true,
            0x68 => true,
            0x69 => true,
            0x6A => true,
            0x6B => true,
            0x6C => true,
            0x6D => true,
            0x6E => true,
            0x6F => true,
            >= VkOem1 and <= VkOem2 => true,
            0xC0 => true,
            0xDB => true,
            0xDD => true,
            0xDE => true,
            0xDC => true,
            _ => false
        };
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
