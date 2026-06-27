using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KongDaeri.Core;

/// <summary>
/// SQLite 수집함. %LOCALAPPDATA%\KongDaeri\kongdaeri.db 에 captures 테이블을 둔다.
/// 기획서 §5.2 스키마 기준. ai_tags 는 JSON 배열 문자열로 저장.
/// </summary>
public sealed class SqliteStorage : IStorage
{
    private readonly string _connectionString;

    public SqliteStorage()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KongDaeri");
        Directory.CreateDirectory(dir);

        var dbPath = Path.Combine(dir, "kongdaeri.db");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS captures (
                id             TEXT PRIMARY KEY,
                source_type    INTEGER NOT NULL,
                captured_at    TEXT NOT NULL,
                raw_text       TEXT,
                image_path     TEXT,
                source_context TEXT,
                ai_title       TEXT,
                ai_markdown    TEXT,
                ai_tags        TEXT,
                status         INTEGER NOT NULL,
                notion_page_id TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task SaveAsync(CaptureItem item)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO captures
                (id, source_type, captured_at, raw_text, image_path, source_context,
                 ai_title, ai_markdown, ai_tags, status, notion_page_id)
            VALUES
                ($id, $source_type, $captured_at, $raw_text, $image_path, $source_context,
                 $ai_title, $ai_markdown, $ai_tags, $status, $notion_page_id);
            """;
        BindItem(cmd, item);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(CaptureItem item)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE captures SET
                source_type    = $source_type,
                captured_at    = $captured_at,
                raw_text       = $raw_text,
                image_path     = $image_path,
                source_context = $source_context,
                ai_title       = $ai_title,
                ai_markdown    = $ai_markdown,
                ai_tags        = $ai_tags,
                status         = $status,
                notion_page_id = $notion_page_id
            WHERE id = $id;
            """;
        BindItem(cmd, item);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<CaptureItem>> ListAsync()
    {
        var result = new List<CaptureItem>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, source_type, captured_at, raw_text, image_path, source_context,
                   ai_title, ai_markdown, ai_tags, status, notion_page_id
            FROM captures
            ORDER BY captured_at DESC;
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadItem(reader));
        }
        return result;
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM captures WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>검증용: 현재 수집 개수.</summary>
    public async Task<int> CountAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM captures;";
        var scalar = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(scalar);
    }

    private static void BindItem(SqliteCommand cmd, CaptureItem item)
    {
        cmd.Parameters.AddWithValue("$id", item.Id.ToString());
        cmd.Parameters.AddWithValue("$source_type", (int)item.SourceType);
        cmd.Parameters.AddWithValue("$captured_at", item.CapturedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$raw_text", (object?)item.RawText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$image_path", (object?)item.ImagePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$source_context", (object?)item.SourceContext ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ai_title", (object?)item.AiTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ai_markdown", (object?)item.AiMarkdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ai_tags",
            item.AiTags is null ? DBNull.Value : JsonSerializer.Serialize(item.AiTags));
        cmd.Parameters.AddWithValue("$status", (int)item.Status);
        cmd.Parameters.AddWithValue("$notion_page_id", (object?)item.NotionPageId ?? DBNull.Value);
    }

    private static CaptureItem ReadItem(SqliteDataReader r)
    {
        string? GetString(int i) => r.IsDBNull(i) ? null : r.GetString(i);
        var tagsJson = GetString(8);

        return new CaptureItem
        {
            Id = Guid.Parse(r.GetString(0)),
            SourceType = (CaptureSourceType)r.GetInt32(1),
            CapturedAt = DateTime.Parse(r.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            RawText = GetString(3),
            ImagePath = GetString(4),
            SourceContext = GetString(5),
            AiTitle = GetString(6),
            AiMarkdown = GetString(7),
            AiTags = tagsJson is null ? null : JsonSerializer.Deserialize<string[]>(tagsJson),
            Status = (CaptureStatus)r.GetInt32(9),
            NotionPageId = GetString(10),
        };
    }
}
