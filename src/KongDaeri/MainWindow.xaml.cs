using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace KongDaeri;

/// <summary>
/// 콩대리 데스펫 오버레이 창 (투명 · 항상 위 · 드래그 이동).
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _bubbleTimer;

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

    /// <summary>말풍선을 1.5초간 표시한다(수집 반응 등).</summary>
    public void ShowBubble(string message)
    {
        BubbleText.Text = message;
        Bubble.Visibility = Visibility.Visible;
        _bubbleTimer.Stop();
        _bubbleTimer.Start();
    }

    // 왼쪽 버튼 드래그로 펫(=창)을 이동.
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
