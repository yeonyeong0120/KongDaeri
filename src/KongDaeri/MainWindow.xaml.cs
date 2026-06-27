using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace KongDaeri;

/// <summary>
/// 콩대리 데스펫 오버레이 창 (투명 · 항상 위 · 드래그 이동).
/// 고정 크기 창이라 말풍선이 떠도 펫 위치가 변하지 않는다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _bubbleTimer;

    private static string PositionPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KongDaeri", "window.json");

    public MainWindow()
    {
        InitializeComponent();

        _bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _bubbleTimer.Tick += (_, _) =>
        {
            _bubbleTimer.Stop();
            Bubble.Visibility = Visibility.Collapsed;
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        PositionWindow();
    }

    // 저장된 위치가 있으면 그걸, 없으면(첫 실행) 주 모니터 작업영역 오른쪽 가장자리 안쪽 + 세로 가운데.
    // 좌표는 모두 WPF 논리좌표(DIP)라 DPI 스케일은 자동 반영된다.
    private void PositionWindow()
    {
        var saved = LoadSavedPosition();
        if (saved is { } p)
        {
            Left = p.Left;
            Top = p.Top;
            return;
        }

        var wa = SystemParameters.WorkArea;            // DIP 기준 작업영역
        Left = wa.Right - Width - 40;                   // 오른쪽 가장자리에서 40 안쪽
        Top = wa.Top + (wa.Height - Height) / 2;        // 세로 가운데
    }

    private static (double Left, double Top)? LoadSavedPosition()
    {
        try
        {
            if (!File.Exists(PositionPath)) return null;
            var json = File.ReadAllText(PositionPath);
            var p = JsonSerializer.Deserialize<WindowPos>(json);
            if (p is null) return null;
            return (p.Left, p.Top);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>현재 위치를 저장(앱 종료 시 호출).</summary>
    public void SavePosition()
    {
        try
        {
            var dir = Path.GetDirectoryName(PositionPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(PositionPath,
                JsonSerializer.Serialize(new WindowPos { Left = Left, Top = Top }));
        }
        catch
        {
            // 위치 저장 실패는 무시.
        }
    }

    private sealed class WindowPos
    {
        public double Left { get; set; }
        public double Top { get; set; }
    }

    /// <summary>말풍선을 1.5초간 표시한다(수집 반응 등).</summary>
    public void ShowBubble(string message)
    {
        BubbleText.Text = message;
        Bubble.Visibility = Visibility.Visible;
        _bubbleTimer.Stop();
        _bubbleTimer.Start();
    }

    /// <summary>펫 더블클릭 → 수집함 열기 요청.</summary>
    public event EventHandler? RequestOpenCollection;

    // 더블클릭 → 수집함 열기. (드래그는 MouseMove 에서 처리해 더블클릭과 충돌 안 나게)
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            RequestOpenCollection?.Invoke(this, EventArgs.Empty);
        }
    }

    // 왼쪽 버튼을 누른 채 움직이면 펫(=창) 이동. (즉시 DragMove 하지 않아 더블클릭 보존)
    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch { /* 버튼이 막 떼진 순간이면 무시 */ }
        }
    }
}
