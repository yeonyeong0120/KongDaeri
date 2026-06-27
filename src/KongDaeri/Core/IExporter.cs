namespace KongDaeri.Core;

public interface IExporter         // 내보내기 (Notion). 추후 확장 가능
{
    Task<string> ExportAsync(CaptureItem item); // 반환: 노션 page id
}
