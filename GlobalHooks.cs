using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CursorDodge;

internal sealed class LowLevelMouseHook : IDisposable
{
    public event Action<int>? MouseReleased;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelProc _proc;

    public LowLevelMouseHook()
    {
        _proc = HookProc;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
            return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        if (module is null)
            throw new Win32Exception("Failed to resolve current process module.");

        var hMod = NativeMethods.GetModuleHandle(module.ModuleName);
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
        if (_hookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is NativeMethods.WM_LBUTTONUP or NativeMethods.WM_RBUTTONUP or NativeMethods.WM_MBUTTONUP)
            {
                MouseReleased?.Invoke(msg);
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}

internal sealed class LowLevelKeyboardHook : IDisposable
{
    public event Action<int>? KeyDowned;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelProc _proc;

    public LowLevelKeyboardHook()
    {
        _proc = HookProc;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
            return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        if (module is null)
            throw new Win32Exception("Failed to resolve current process module.");

        var hMod = NativeMethods.GetModuleHandle(module.ModuleName);
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, hMod, 0);
        if (_hookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                KeyDowned?.Invoke(vkCode);
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
