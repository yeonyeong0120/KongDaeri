using System.Windows;
using System.Windows.Interop;

namespace KongDaeri.Interop;

/// <summary>
/// Win32 RegisterHotKey 기반 전역 단축키. WPF 창 핸들(HwndSource)에 WM_HOTKEY 훅을 걸어 수신한다.
/// 단축키 문자열("Ctrl+Alt+S")을 파싱하며, 비거나 실패하면 기본 Ctrl+Alt+S 를 쓴다.
/// </summary>
public sealed class GlobalHotkey
{
    private const int HotkeyId = 0x4B44;   // 임의 ID

    private readonly Window _window;
    private HwndSource? _source;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _registered;

    public GlobalHotkey(Window window) => _window = window;

    /// <summary>단축키가 눌렸을 때 발생.</summary>
    public event EventHandler? Pressed;

    /// <summary>사람이 읽을 수 있는 현재 단축키 표기(예: "Ctrl+Alt+S").</summary>
    public string Description { get; private set; } = "Ctrl+Alt+S";

    /// <summary>
    /// 단축키를 등록한다. 성공하면 true. (실패해도 예외를 던지지 않음 — 호출부가 비활성 처리)
    /// </summary>
    public bool Register(string? hotkeyText)
    {
        var (mods, vk, desc) = ParseOrDefault(hotkeyText);
        Description = desc;

        var helper = new WindowInteropHelper(_window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        _registered = NativeMethods.RegisterHotKey(_hwnd, HotkeyId, mods | NativeMethods.MOD_NOREPEAT, vk);
        if (!_registered)
        {
            // 훅은 걸어뒀으니 정리.
            _source?.RemoveHook(WndProc);
        }
        return _registered;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        }
        _source?.RemoveHook(WndProc);
        _source = null;
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// "Ctrl+Alt+S" 형태를 (수정자, 가상키, 정규화표기)로 파싱. 실패 시 기본 Ctrl+Alt+S.
    /// 키는 A~Z, 0~9, F1~F12 지원.
    /// </summary>
    public static (uint mods, uint vk, string desc) ParseOrDefault(string? text)
    {
        var def = (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)'S', "Ctrl+Alt+S");
        if (string.IsNullOrWhiteSpace(text)) return def;

        uint mods = 0;
        uint vk = 0;
        var modNames = new List<string>();
        string? keyName = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    mods |= NativeMethods.MOD_CONTROL; modNames.Add("Ctrl"); break;
                case "alt":
                    mods |= NativeMethods.MOD_ALT; modNames.Add("Alt"); break;
                case "shift":
                    mods |= NativeMethods.MOD_SHIFT; modNames.Add("Shift"); break;
                case "win" or "windows":
                    mods |= NativeMethods.MOD_WIN; modNames.Add("Win"); break;
                default:
                    keyName = raw; break;   // 마지막 비-수정자 토큰을 키로
            }
        }

        if (mods == 0 || keyName is null || !TryParseKey(keyName, out vk, out var keyDisp))
        {
            return def;
        }

        var desc = string.Join("+", modNames) + "+" + keyDisp;
        return (mods, vk, desc);
    }

    private static bool TryParseKey(string key, out uint vk, out string display)
    {
        key = key.Trim().ToUpperInvariant();
        vk = 0; display = key;

        if (key.Length == 1)
        {
            char c = key[0];
            if (c is >= 'A' and <= 'Z') { vk = c; display = c.ToString(); return true; }
            if (c is >= '0' and <= '9') { vk = c; display = c.ToString(); return true; }
            return false;
        }

        // F1~F12
        if (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out var fn) && fn is >= 1 and <= 12)
        {
            vk = (uint)(0x70 + (fn - 1));   // VK_F1 = 0x70
            display = "F" + fn;
            return true;
        }

        return false;
    }
}
