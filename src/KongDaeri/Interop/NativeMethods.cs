using System.Runtime.InteropServices;

namespace KongDaeri.Interop;

/// <summary>Win32 P/Invoke 모음.</summary>
internal static class NativeMethods
{
    /// <summary>클립보드 내용이 바뀌면 대상 창에 전달되는 메시지.</summary>
    public const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
