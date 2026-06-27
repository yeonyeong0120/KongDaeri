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
    private IAiProcessor? _aiProcessor;
    private NotionExporter? _notionExporter;

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

        // 설정 로드 후 AI/노션 구성. 키가 없거나 실패해도 앱은 계속 동작(수집/저장 유지).
        var settings = AppSettings.Load();

        try
        {
            _aiProcessor = new GeminiProcessor(settings);
        }
        catch (Exception ex)
        {
            _aiProcessor = null;
            LogLine($"[AI 비활성화] {ex.Message}");   // 키 값은 메시지에 포함되지 않음
        }

        try
        {
            _notionExporter = new NotionExporter(settings);
        }
        catch (Exception ex)
        {
            _notionExporter = null;
            LogLine($"[노션 비활성화] {ex.Message}");  // 토큰 값은 메시지에 포함되지 않음
        }

        InitTrayIcon();
    }

    // 캡처 → 저장(즉시 완료) → 펫 반응. 그 뒤 AI는 백그라운드로 분리 처리.
    private async void OnCaptured(object? sender, CaptureItem item)
    {
        if (_storage is null) return;

        await _storage.SaveAsync(item);
        var count = await _storage.CountAsync();

        // 검증용 로그: 저장될 때마다 개수와 미리보기를 파일에 기록.
        LogCapture(count, item);

        // 저장 성공 시 말풍선.
        _petWindow?.ShowBubble("주워 담았어요");

        // 저장과 분리: AI가 느리거나 실패해도 위 저장은 이미 끝나 있음.
        _ = ProcessWithAiAsync(item);
    }

    // 백그라운드 AI 정리: Processing → (성공)Processed / (실패)Failed.
    private async Task ProcessWithAiAsync(CaptureItem item)
    {
        if (_aiProcessor is null || _storage is null) return;

        try
        {
            item.Status = CaptureStatus.Processing;
            await _storage.UpdateAsync(item);
            ShowBubble("정리 중…");

            await _aiProcessor.ProcessAsync(item);   // AiTitle/AiTags/AiMarkdown + Status=Processed
            await _storage.UpdateAsync(item);

            LogAiResult(item);
            ShowBubble("정리 끝!");
        }
        catch (Exception ex)
        {
            item.Status = CaptureStatus.Failed;
            try { await _storage.UpdateAsync(item); } catch { /* 업데이트 실패는 무시 */ }

            LogLine($"[AI 실패] {ex.GetType().Name}: {ex.Message}");
            ShowBubble("앗, 실패했어요");
        }
    }

    // '노션 공유' 임시 경로: 가장 최근 Processed 항목을 노션 페이지로 전송.
    private async Task ExportLatestAsync()
    {
        if (_storage is null) return;

        if (_notionExporter is null)
        {
            LogLine("[노션 전송] 노션이 비활성화됨(토큰/페이지ID 확인). settings.json 점검.");
            ShowBubble("앗, 실패했어요");
            return;
        }

        // 가장 최근의 Processed(AI 정리 완료) 항목 찾기.
        var items = await _storage.ListAsync();
        var target = items.FirstOrDefault(i => i.Status == CaptureStatus.Processed);
        if (target is null)
        {
            LogLine("[노션 전송] 전송할 Processed 항목이 없습니다. (먼저 복사→AI 정리 필요)");
            ShowBubble("올릴 게 없어요");
            return;
        }

        try
        {
            ShowBubble("노션에 올리는 중…");

            await _notionExporter.ExportAsync(target);   // NotionPageId + Status=Exported
            await _storage.UpdateAsync(target);

            LogLine($"[노션 전송 완료] title=\"{target.AiTitle}\"  url={_notionExporter.LastExportedUrl}");
            ShowBubble("노션에 올렸어요!");
        }
        catch (Exception ex)
        {
            LogLine($"[노션 전송 실패] {ex.GetType().Name}: {ex.Message}");
            ShowBubble("앗, 실패했어요");
        }
    }

    // 말풍선을 UI 스레드에서 안전하게 표시.
    private void ShowBubble(string message)
    {
        var w = _petWindow;
        if (w is null) return;
        w.Dispatcher.Invoke(() => w.ShowBubble(message));
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

    // 검증용: AI 정리 결과(제목/태그/마크다운 길이)를 capture.log 에 기록.
    private static void LogAiResult(CaptureItem item)
    {
        var tags = item.AiTags is { Length: > 0 } ? string.Join(", ", item.AiTags) : "(없음)";
        var mdLen = item.AiMarkdown?.Length ?? 0;
        LogLine($"[AI {item.Status}]  title=\"{item.AiTitle}\"  tags=[{tags}]  md={mdLen}자");
    }

    private static void LogLine(string text)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KongDaeri");
            var logPath = System.IO.Path.Combine(dir, "capture.log");
            System.IO.File.AppendAllText(logPath,
                $"{DateTime.Now:HH:mm:ss}  {text}" + Environment.NewLine);
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
        menu.Items.Add("마지막 항목 노션 전송", null, (_, _) => _ = ExportLatestAsync());
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
