using System.IO;
using System.Windows;
using KongDaeri.Core;
using Drawing = System.Drawing;
using Imaging = System.Drawing.Imaging;

namespace KongDaeri.Sources;

/// <summary>
/// 화면 스니퍼. 단축키로 트리거되면 영역 선택 오버레이를 띄우고, 확정된 물리 픽셀 영역을
/// CopyFromScreen 으로 캡처해 PNG(+썸네일) 저장 후 CaptureItem(ScreenSnip, Collected)을 push.
/// AI 는 호출하지 않음(수동 정책).
/// </summary>
public sealed class ScreenSnipSource : ICaptureSource
{
    private readonly string _snipDir;
    private bool _active;   // 오버레이가 떠 있는 동안 중복 트리거 방지

    public ScreenSnipSource()
    {
        _snipDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KongDaeri", "snips");
        Directory.CreateDirectory(_snipDir);
    }

    public string Name => "화면 스니퍼";
    public CaptureSourceType Type => CaptureSourceType.ScreenSnip;
    public event EventHandler<CaptureItem>? Captured;

    public void Start() { /* 트리거는 외부(단축키)에서 Trigger() 호출 */ }
    public void Stop() { }

    /// <summary>단축키 감지 시 호출. 영역 선택 오버레이를 띄운다(UI 스레드에서).</summary>
    public void Trigger()
    {
        if (_active) return;
        _active = true;

        var overlay = new SnipOverlayWindow();
        overlay.Completed += rect =>
        {
            _active = false;
            if (rect is { } r)
            {
                CaptureAndPush(r);
            }
        };
        overlay.Closed += (_, _) => _active = false;
        overlay.Show();
    }

    private void CaptureAndPush(Drawing.Rectangle r)
    {
        try
        {
            var id = Guid.NewGuid();
            var path = Path.Combine(_snipDir, $"{id}.png");

            using (var bmp = new Drawing.Bitmap(r.Width, r.Height, Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(r.X, r.Y, 0, 0, new Drawing.Size(r.Width, r.Height),
                        Drawing.CopyPixelOperation.SourceCopy);
                }
                bmp.Save(path, Imaging.ImageFormat.Png);
                SaveThumbnail(bmp, Path.Combine(_snipDir, $"{id}_thumb.png"));
            }

            var item = new CaptureItem
            {
                Id = id,
                SourceType = CaptureSourceType.ScreenSnip,
                CapturedAt = DateTime.Now,
                ImagePath = path,
                SourceContext = "화면 스니핑",
                Status = CaptureStatus.Collected,
            };
            Captured?.Invoke(this, item);
        }
        catch (Exception ex)
        {
            AppLog.Line($"[스니퍼] 캡처 실패: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void SaveThumbnail(Drawing.Bitmap src, string thumbPath)
    {
        const int max = 160;
        double scale = Math.Min(1.0, (double)max / Math.Max(src.Width, src.Height));
        int tw = Math.Max(1, (int)(src.Width * scale));
        int th = Math.Max(1, (int)(src.Height * scale));

        using var thumb = new Drawing.Bitmap(tw, th);
        using (var g = Drawing.Graphics.FromImage(thumb))
        {
            g.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, tw, th);
        }
        thumb.Save(thumbPath, Imaging.ImageFormat.Png);
    }
}
