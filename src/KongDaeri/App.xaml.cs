using System.Windows;
using KongDaeri.Core;
using KongDaeri.Interop;
using KongDaeri.Sources;
using KongDaeri.ViewModels;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace KongDaeri;

/// <summary>
/// 앱 진입점. 데스펫 오버레이, 시스템 트레이, 캡처 파이프라인(CaptureService),
/// 수집함 창의 생명주기를 관리한다.
/// </summary>
public partial class App : System.Windows.Application
{
    private WinForms.NotifyIcon? _trayIcon;
    private WinForms.ToolStripMenuItem? _countMenuItem;
    private MainWindow? _petWindow;

    private SqliteStorage? _storage;
    private ClipboardSource? _clipboardSource;
    private ScreenSnipSource? _snipSource;
    private CaptureService? _service;
    private GlobalHotkey? _snipHotkey;

    private CollectionViewModel? _collectionViewModel;
    private CollectionWindow? _collectionWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 데스펫 오버레이 창.
        _petWindow = new MainWindow();
        _petWindow.RequestOpenCollection += (_, _) => OpenCollectionWindow();
        _petWindow.Show();

        // 설정 로드 → AI/노션 구성(키 없거나 실패해도 앱은 계속 동작).
        var settings = AppSettings.Load();

        IAiProcessor? ai = null;
        try { ai = new GeminiProcessor(settings); }
        catch (Exception ex) { AppLog.Line($"[AI 비활성화] {ex.Message}"); }

        NotionExporter? notion = null;
        try { notion = new NotionExporter(settings); }
        catch (Exception ex) { AppLog.Line($"[노션 비활성화] {ex.Message}"); }

        // 저장소 + 파이프라인.
        _storage = new SqliteStorage();
        _service = new CaptureService(_storage, ai, notion);
        _service.StatusMessage += (_, msg) => ShowBubble(msg);

        // 클립보드 소스 → 서비스.
        _clipboardSource = new ClipboardSource(_petWindow);
        _clipboardSource.Captured += OnCaptured;
        _clipboardSource.Start();

        // 화면 스니퍼 소스 → 서비스(저장만, AI 수동 정책 유지).
        _snipSource = new ScreenSnipSource();
        _snipSource.Captured += OnSnipCaptured;
        _snipSource.Start();

        // 화면 스니퍼 전역 단축키. 등록 실패해도 앱은 계속(스니퍼만 비활성).
        try
        {
            _snipHotkey = new GlobalHotkey(_petWindow);
            if (_snipHotkey.Register(settings.SnipHotkey))
            {
                _snipHotkey.Pressed += OnSnipHotkeyPressed;
                AppLog.Line($"[스니퍼] 단축키 등록: {_snipHotkey.Description}");
            }
            else
            {
                AppLog.Line($"[스니퍼] 단축키 등록 실패({_snipHotkey.Description}) — 다른 앱과 충돌일 수 있음. " +
                            "스니퍼 비활성, 클립보드 수집은 계속 동작.");
                _snipHotkey = null;
            }
        }
        catch (Exception ex)
        {
            AppLog.Line($"[스니퍼] 단축키 초기화 오류: {ex.Message}");
            _snipHotkey = null;
        }

        InitTrayIcon();
    }

    // 단축키 감지 → 영역 선택 오버레이.
    private void OnSnipHotkeyPressed(object? sender, EventArgs e)
    {
        _snipSource?.Trigger();
    }

    // 스니핑 캡처 → 저장(공통 흐름) → 스니핑 전용 말풍선.
    private async void OnSnipCaptured(object? sender, CaptureItem item)
    {
        if (_service is null) return;
        await _service.HandleCapturedAsync(item);   // 저장 + "주워 담았어요"
        ShowBubble("찰칵! 주워 담았어요");            // 스니핑 전용 문구로 덮어쓰기
    }

    private async void OnCaptured(object? sender, CaptureItem item)
    {
        if (_service is null) return;
        await _service.HandleCapturedAsync(item);
    }

    // ===== 수집함 창 =====
    private void OpenCollectionWindow()
    {
        if (_service is null) return;

        // 뷰모델은 앱 수명 동안 1개 유지(닫혀 있어도 실시간 갱신 유지).
        _collectionViewModel ??= new CollectionViewModel(_service);

        if (_collectionWindow is null)
        {
            _collectionWindow = new CollectionWindow(_collectionViewModel);
            _collectionWindow.Closed += (_, _) => _collectionWindow = null;
            _collectionWindow.Show();
        }
        else
        {
            _collectionWindow.Activate();
        }
    }

    // ===== 트레이 =====
    private void InitTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();

        _countMenuItem = new WinForms.ToolStripMenuItem("수집 개수: -") { Enabled = false };
        menu.Items.Add(_countMenuItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("수집함 열기", null, (_, _) => OpenCollectionWindow());
        menu.Items.Add("콩대리 보이기/숨기기", null, (_, _) => TogglePetVisibility());
        menu.Items.Add("마지막 항목 노션 전송", null, (_, _) => _ = ExportLatestAsync());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => ExitApp());

        menu.Opening += async (_, _) =>
        {
            if (_service is null || _countMenuItem is null) return;
            var count = (await _service.ListAsync()).Count;
            _countMenuItem.Text = $"수집 개수: {count}";
        };

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "콩대리(KongDaeri)",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // 더블클릭 → 수집함 열기.
        _trayIcon.DoubleClick += (_, _) => OpenCollectionWindow();
    }

    // 임시 경로: 가장 최근 Processed 항목을 노션으로 전송.
    private async Task ExportLatestAsync()
    {
        if (_service is null) return;

        var items = await _service.ListAsync();
        var target = items.FirstOrDefault(i => i.Status == CaptureStatus.Processed);
        if (target is null)
        {
            AppLog.Line("[노션 전송] 전송할 Processed 항목이 없습니다.");
            ShowBubble("올릴 게 없어요");
            return;
        }

        try
        {
            await _service.ExportAsync(target);
        }
        catch (Exception ex)
        {
            AppLog.Line($"[노션 전송 실패] {ex.GetType().Name}: {ex.Message}");
            ShowBubble("앗, 실패했어요");
        }
    }

    /// <summary>전용 멀티사이즈 .ico 를 트레이 아이콘으로 직접 로드. 실패 시 시스템 기본.</summary>
    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/kongdr_glasses_icon.ico");
            var streamInfo = GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                // 트레이 권장 크기를 .ico 내에서 골라 또렷하게 표시.
                var size = WinForms.SystemInformation.SmallIconSize;
                return new Drawing.Icon(stream, size);
            }
        }
        catch
        {
            // 폴백.
        }
        return Drawing.SystemIcons.Application;
    }

    private void TogglePetVisibility()
    {
        if (_petWindow == null) return;
        if (_petWindow.IsVisible) _petWindow.Hide();
        else BringPetToFront();
    }

    private void BringPetToFront()
    {
        if (_petWindow == null) return;
        _petWindow.Show();
        _petWindow.Topmost = true;
        _petWindow.Activate();
    }

    // 말풍선을 UI 스레드에서 안전하게 표시.
    private void ShowBubble(string message)
    {
        var w = _petWindow;
        if (w is null) return;
        w.Dispatcher.Invoke(() => w.ShowBubble(message));
    }

    private void ExitApp()
    {
        _petWindow?.SavePosition();
        _snipHotkey?.Unregister();
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
