using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using KongDaeri.Interop;
using Drawing = System.Drawing;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace KongDaeri;

/// <summary>
/// 전체(가상) 화면을 덮는 영역 선택 오버레이. 드래그한 DIP 영역을 물리 픽셀로 변환해
/// Completed 로 넘긴다(null = 취소). 좌표 정확도가 핵심 — Per-Monitor V2 + DPI 환산.
/// </summary>
public partial class SnipOverlayWindow : Window
{
    private int _vx, _vy;          // 가상 화면 물리 원점
    private bool _dragging;
    private Point _start;          // 드래그 시작(DIP, Root 기준)

    /// <summary>선택 완료. 인자는 물리 픽셀 사각형(취소 시 null).</summary>
    public event Action<Drawing.Rectangle?>? Completed;

    public SnipOverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 가상 화면 전체(물리 픽셀)를 덮도록 창을 배치 — 음수 좌표/멀티모니터 포함.
        _vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        _vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, _vx, _vy, vw, vh,
            NativeMethods.SWP_SHOWWINDOW);

        Activate();
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Finish(null);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragging = true;
        _start = e.GetPosition(Root);
        CaptureMouse();
        SelBorder.Visibility = Visibility.Visible;
        SizeBox.Visibility = Visibility.Visible;
        UpdateSelection(_start);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            UpdateSelection(e.GetPosition(Root));
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var sel = MakeRect(_start, e.GetPosition(Root));
        if (sel.Width < 3 || sel.Height < 3)
        {
            Finish(null);   // 너무 작으면 취소로 간주
            return;
        }

        // DIP → 물리 픽셀 변환 (오버레이 창의 DPI 사용).
        var dpi = VisualTreeHelper.GetDpi(this);
        int px = _vx + (int)Math.Round(sel.X * dpi.DpiScaleX);
        int py = _vy + (int)Math.Round(sel.Y * dpi.DpiScaleY);
        int pw = (int)Math.Round(sel.Width * dpi.DpiScaleX);
        int ph = (int)Math.Round(sel.Height * dpi.DpiScaleY);

        Finish(new Drawing.Rectangle(px, py, pw, ph));
    }

    private void UpdateSelection(Point cur)
    {
        var sel = MakeRect(_start, cur);

        // 어두운 막에 선택 영역만 구멍(EvenOdd).
        var full = new RectangleGeometry(new Rect(0, 0, Root.ActualWidth, Root.ActualHeight));
        var grp = new GeometryGroup { FillRule = FillRule.EvenOdd };
        grp.Children.Add(full);
        grp.Children.Add(new RectangleGeometry(sel));
        Mask.Data = grp;

        Canvas.SetLeft(SelBorder, sel.X);
        Canvas.SetTop(SelBorder, sel.Y);
        SelBorder.Width = sel.Width;
        SelBorder.Height = sel.Height;

        SizeText.Text = $"{(int)sel.Width} × {(int)sel.Height}";
        Canvas.SetLeft(SizeBox, sel.X);
        Canvas.SetTop(SizeBox, Math.Max(0, sel.Y - 26));
    }

    private static Rect MakeRect(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        double w = Math.Abs(a.X - b.X), h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }

    private async void Finish(Drawing.Rectangle? rect)
    {
        // 캡처 전에 오버레이를 화면에서 치운다(어두운 막이 캡처에 안 찍히도록).
        Hide();

        if (rect is { Width: > 0, Height: > 0 })
        {
            await Task.Delay(90);   // DWM 이 오버레이 자리를 다시 그릴 시간
            Completed?.Invoke(rect);
        }
        else
        {
            Completed?.Invoke(null);
        }

        Close();
    }
}
