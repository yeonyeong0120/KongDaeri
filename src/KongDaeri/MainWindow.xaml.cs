using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KongDaeri;

/// <summary>
/// 콩대리 데스펫 오버레이 창 (투명 · 항상 위 · 드래그 이동).
/// 고정 크기 창이라 말풍선이 떠도 펫 위치가 변하지 않는다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _bubbleTimer;

    // 표정
    private readonly ImageSource _normalImg;
    private readonly ImageSource _blinkImg;
    private readonly ImageSource _sadImg;
    private readonly ImageSource? _happyImg;     // 파일 없으면 null → 기본 유지
    private readonly DispatcherTimer _blinkTimer = new();
    private readonly DispatcherTimer _blinkRevertTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _moodRevertTimer = new();
    private readonly Random _rng = new();
    private bool _moodActive;                     // sad/happy 표시 중 — 깜빡임 억제

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

        // 표정 이미지 로드. (펫 위치/크기는 동일 Image 의 Source 만 교체하므로 불변)
        _normalImg = LoadImage("kongdr_clean_transparent.png")!;
        _blinkImg = LoadImage("kongdr_blink.png") ?? _normalImg;
        _sadImg = LoadImage("kongdr_sad.png") ?? _normalImg;
        _happyImg = LoadImage("kongdr_happy.png");   // 파일 없으면 null
        PetImage.Source = _normalImg;

        _blinkRevertTimer.Tick += (_, _) =>
        {
            _blinkRevertTimer.Stop();
            if (!_moodActive) PetImage.Source = _normalImg;
            ScheduleNextBlink();
        };
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkTimer.Stop();
            if (!_moodActive) { PetImage.Source = _blinkImg; _blinkRevertTimer.Start(); }
            else ScheduleNextBlink();
        };
        ScheduleNextBlink();

        _moodRevertTimer.Tick += (_, _) =>
        {
            _moodRevertTimer.Stop();
            _moodActive = false;
            PetImage.Source = _normalImg;
        };
    }

    private static ImageSource? LoadImage(string fileName)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{fileName}");
            if (System.Windows.Application.GetResourceStream(uri) is null) return null;
            return new BitmapImage(uri);
        }
        catch { return null; }
    }

    private void ScheduleNextBlink()
    {
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(_rng.Next(3000, 6000));
        _blinkTimer.Start();
    }

    /// <summary>실패 시 시무룩한 표정으로 잠시 전환 후 복귀.</summary>
    public void ShowSad() => ShowMood(_sadImg);

    /// <summary>성공 시 기쁜 표정(파일 있을 때만). 없으면 기본 유지.</summary>
    public void ShowHappy() { if (_happyImg is not null) ShowMood(_happyImg); }

    private void ShowMood(ImageSource img)
    {
        _moodActive = true;
        PetImage.Source = img;
        _moodRevertTimer.Stop();
        _moodRevertTimer.Interval = TimeSpan.FromSeconds(1.8);
        _moodRevertTimer.Start();
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

        // 세로 가운데에서 화면 높이의 조금만 아래로...
        double top = wa.Top + (wa.Height - Height) / 2 + wa.Height * 0.03;
        double maxTop = wa.Bottom - Height - 20;
        Top = Math.Min(top, maxTop);
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

    /// <summary>일시정지 중이면 펫을 흐리게(시각 피드백).</summary>
    public void SetDimmed(bool dimmed)
    {
        PetImage.Opacity = dimmed ? 0.4 : 1.0;
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

    /// <summary>펫 우클릭 → 컨텍스트 메뉴 요청.</summary>
    public event EventHandler? RequestContextMenu;

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        RequestContextMenu?.Invoke(this, EventArgs.Empty);
    }

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
