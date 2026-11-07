$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

$articlesJson = Get-Content -Raw "client/src/features/journey/admin/curatedArticles24.json" | ConvertFrom-Json
$adminId = "727e8d35-f719-48ea-8397-0f6b76cde623"

try {
    $conn.Open()
    $inserted = 0
    foreach ($art in $articlesJson) {
        $checkCmd = $conn.CreateCommand()
        $checkCmd.CommandText = "SELECT COUNT(*) FROM Articles WHERE Slug = @slug"
        [void]$checkCmd.Parameters.AddWithValue("@slug", $art.slug)
        $exists = [int]$checkCmd.ExecuteScalar()

        if ($exists -eq 0) {
            $thumb = if ([string]::IsNullOrEmpty($art.thumbnailUrl)) { [DBNull]::Value } else { $art.thumbnailUrl }
            $tags = if ([string]::IsNullOrEmpty($art.keyword)) { [DBNull]::Value } else { "[$($art.keyword)]" }

            $insertCmd = $conn.CreateCommand()
            $insertCmd.CommandText = @"
INSERT INTO Articles (Id, AuthorId, Title, Slug, Excerpt, Content, ThumbnailUrl, Category, TagsJson, IsPublished, PublishedAt, ViewCount, CreatedAt, UpdatedAt)
VALUES (@id, @authorId, @title, @slug, @excerpt, @content, @thumbnailUrl, @category, @tagsJson, 1, GETUTCDATE(), @viewCount, GETUTCDATE(), GETUTCDATE())
"@
            [void]$insertCmd.Parameters.AddWithValue("@id", [Guid]::NewGuid())
            [void]$insertCmd.Parameters.AddWithValue("@authorId", [Guid]$adminId)
            [void]$insertCmd.Parameters.AddWithValue("@title", $art.title)
            [void]$insertCmd.Parameters.AddWithValue("@slug", $art.slug)
            [void]$insertCmd.Parameters.AddWithValue("@excerpt", $art.excerpt)
            [void]$insertCmd.Parameters.AddWithValue("@content", $art.content)
            [void]$insertCmd.Parameters.AddWithValue("@thumbnailUrl", $thumb)
            [void]$insertCmd.Parameters.AddWithValue("@category", $art.category)
            [void]$insertCmd.Parameters.AddWithValue("@tagsJson", $tags)
            [void]$insertCmd.Parameters.AddWithValue("@viewCount", 120 + $art.num * 14)

            [void]$insertCmd.ExecuteNonQuery()
            $inserted++
        }
    }
    Write-Output "Successfully inserted $inserted articles into SQL Server!"
} catch {
    Write-Output "Error: $_"
} finally {
    $conn.Close()
}