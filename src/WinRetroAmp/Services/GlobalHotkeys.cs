using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WinRetroAmp.Controller;

namespace WinRetroAmp.Services;

/// <summary>System-wide media keys (play/pause, next, previous, stop) via RegisterHotKey.</summary>
public sealed partial class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint VkMediaNext = 0xB0, VkMediaPrev = 0xB1, VkMediaStop = 0xB2, VkMediaPlayPause = 0xB3;
    private readonly HwndSource? _source;
    private readonly PlayerController _c;
    private readonly List<int> _ids = [];

    public GlobalHotkeys(Window window, PlayerController c)
    {
        _c = c;
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(Hook);
        int id = 0x5A00;
        foreach (uint vk in new[] { VkMediaPlayPause, VkMediaNext, VkMediaPrev, VkMediaStop })
        {
            // Another player may already own a key; that is fine, the rest still work.
            if (RegisterHotKey(handle, id, 0, vk)) _ids.Add(id);
            id++;
        }
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        uint vk = (uint)((long)lParam >> 16) & 0xFFFF;
        switch (vk)
        {
            case VkMediaPlayPause:
                if (_c.State == Core.Audio.PlayerState.Stopped) _ = _c.PlayAsync();
                else _c.PauseToggle();
                break;
            case VkMediaNext: _ = _c.NextAsync(); break;
            case VkMediaPrev: _ = _c.PreviousAsync(); break;
            case VkMediaStop: _c.Stop(); break;
            default: return IntPtr.Zero;
        }
        handled = true;
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null) return;
        foreach (int id in _ids) UnregisterHotKey(_source.Handle, id);
        _source.RemoveHook(Hook);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
