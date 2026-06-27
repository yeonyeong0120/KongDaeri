using System.Windows;
using KongDaeri.Core;
using KongDaeri.Sources;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace KongDaeri;

/// <summary>
/// 앱 진입점. 데스펫 오버레이 창과 시스템 트레이 아이콘의 생명주기를 관리한다.
/// </summary>
public partial class App : System.Windows.Application
{
    private WinForms.NotifyIcon? _trayIcon;
    private WinForms.ToolStripMenuItem? _countMenuItem;
    private MainWindow? _petWindow;

    private SqliteStorage? _storage;
    private ClipboardSource? _clipboardSource;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 데스펫 오버레이 창 생성 및 표시.
        _petWindow = new MainWindow();
        _petWindow.Show();

        // 저장소 + 클립보드 소스 배선.
        _storage = new SqliteStorage();
        _clipboardSource = new ClipboardSource(_petWindow);
        _clipboardSource.Captured += OnCaptured;
        _clipboardSource.Start();

        InitTrayIcon();
    }

    // 캡처 → 저장 → 펫 반응. (AI/노션은 이 단계에서 다루지 않음)
    private async void OnCaptured(object? sender, CaptureItem item)
    {
        if (_storage is null) return;

        await _storage.SaveAsync(item);
        var count = await _storage.CountAsync();

        // 검증용 로그: 저장될 때마다 개수와 미리보기를 파일에 기록.
        LogCapture(count, item);

        // 저장 성공 시 말풍선.
        _petWindow?.ShowBubble("주워 담았어요");
    }

    // 검증용: %LOCALAPPDATA%\KongDaeri\capture.log 에 한 줄씩 남긴다.
    private static void LogCapture(int count, CaptureItem item)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KongDaeri");
            var logPath = System.IO.Path.Combine(dir, "capture.log");

            var preview = (item.RawText ?? "").Replace("\r", " ").Replace("\n", " ");
            if (preview.Length > 40) preview = preview[..40] + "…";

            var line = $"{DateTime.Now:HH:mm:ss}  count={count}  [{item.SourceType}]  \"{preview}\"";
            System.IO.File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch
        {
            // 로그 실패는 무시.
        }
    }

    private void InitTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();

        // 검증용: 현재 수집 개수 표시(클릭 불가). 메뉴 열 때 갱신.
        _countMenuItem = new WinForms.ToolStripMenuItem("수집 개수: -") { Enabled = false };
        menu.Items.Add(_countMenuItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("콩대리 보이기/숨기기", null, (_, _) => TogglePetVisibility());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => ExitApp());

        menu.Opening += async (_, _) =>
        {
            if (_storage is null || _countMenuItem is null) return;
            var count = await _storage.CountAsync();
            _countMenuItem.Text = $"수집 개수: {count}";
        };

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "콩대리(KongDaeri)",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // 좌클릭 더블클릭 → 오버레이를 앞으로.
        _trayIcon.DoubleClick += (_, _) => BringPetToFront();
    }

    /// <summary>
    /// Assets PNG를 트레이 아이콘으로 변환. 실패 시 시스템 기본 아이콘으로 대체.
    /// </summary>
    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/kongdr_clean_transparent.png");
            var streamInfo = GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                using var bmp = new Drawing.Bitmap(stream);
                using var resized = new Drawing.Bitmap(bmp, new Drawing.Size(32, 32));
                return Drawing.Icon.FromHandle(resized.GetHicon());
            }
        }
        catch
        {
            // 변환 실패 시 아래 기본 아이콘으로 폴백.
        }

        return Drawing.SystemIcons.Application;
    }

    private void TogglePetVisibility()
    {
        if (_petWindow == null) return;

        if (_petWindow.IsVisible)
        {
            _petWindow.Hide();
        }
        else
        {
            BringPetToFront();
        }
    }

    private void BringPetToFront()
    {
        if (_petWindow == null) return;

        _petWindow.Show();
        _petWindow.Topmost = true;
        _petWindow.Activate();
    }

    private void ExitApp()
    {
        _clipboardSource?.Stop();

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
