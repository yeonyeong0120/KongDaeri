using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using KongDaeri.Core;
using KongDaeri.Interop;
using Clipboard = System.Windows.Clipboard;

namespace KongDaeri.Sources;

/// <summary>
/// 클립보드 비서. Win32 AddClipboardFormatListener + WM_CLIPBOARDUPDATE 메시지로
/// 클립보드 변경을 감지(폴링 아님). WPF 창 핸들에 HwndSource 훅을 건다.
/// 텍스트만 처리하고, 직전과 같은 내용은 해시 비교로 무시한다.
/// </summary>
public sealed class ClipboardSource : ICaptureSource
{
    private readonly Window _window;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private byte[]? _lastHash;

    public ClipboardSource(Window window)
    {
        _window = window;
    }

    public string Name => "클립보드 비서";
    public CaptureSourceType Type => CaptureSourceType.Clipboard;

    /// <summary>일시정지 중이면 클립보드 변경을 무시(저장·AI 전부 안 함).</summary>
    public bool Paused { get; set; }

    public event EventHandler<CaptureItem>? Captured;

    public void Start()
    {
        // 창 핸들이 아직 없으면 생성하도록 보장.
        var helper = new WindowInteropHelper(_window);
        _hwnd = helper.EnsureHandle();

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        NativeMethods.AddClipboardFormatListener(_hwnd);
    }

    public void Stop()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_hwnd);
        }
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        _hwnd = IntPtr.Zero;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            HandleClipboardUpdate();
        }
        return IntPtr.Zero;
    }

    private void HandleClipboardUpdate()
    {
        // 일시정지 중이면 아무것도 하지 않음(읽기·저장·AI 모두 스킵).
        if (Paused)
        {
            return;
        }

        // 텍스트만 처리.
        if (!Clipboard.ContainsText())
        {
            return;
        }

        string text;
        try
        {
            text = Clipboard.GetText();
        }
        catch
        {
            // 다른 앱이 클립보드를 잠근 순간이면 조용히 무시.
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // 중복 제거: 직전 텍스트와 동일하면 무시(해시 비교).
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        if (_lastHash is not null && _lastHash.AsSpan().SequenceEqual(hash))
        {
            return;
        }
        _lastHash = hash;

        var item = new CaptureItem
        {
            Id = Guid.NewGuid(),
            SourceType = CaptureSourceType.Clipboard,
            CapturedAt = DateTime.Now,
            RawText = text,
            Status = CaptureStatus.Collected,
        };

        Captured?.Invoke(this, item);
    }
}
