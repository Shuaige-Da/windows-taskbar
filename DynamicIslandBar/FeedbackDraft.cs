using System.IO;

namespace DynamicIslandBar;

public sealed record FeedbackImageAttachment(string FilePath, string FileName, long FileSizeBytes);

public sealed class FeedbackDraft
{
    public string Text { get; set; } = string.Empty;
    public List<FeedbackImageAttachment> Images { get; } = [];
    public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
}

public static class FeedbackAttachmentPolicy
{
    public const int MaximumImageCount = 5;
    public const long MaximumImageSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions = new(
        [".png", ".jpg", ".jpeg", ".webp", ".bmp"],
        StringComparer.OrdinalIgnoreCase);

    public static bool TryCreate(
        string filePath,
        int existingCount,
        out FeedbackImageAttachment? attachment,
        out string error)
    {
        attachment = null;
        error = string.Empty;
        if (existingCount >= MaximumImageCount)
        {
            error = $"最多只能添加 {MaximumImageCount} 张图片。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            error = "图片文件不存在。";
            return false;
        }

        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            error = "仅支持 PNG、JPG、WebP 和 BMP 图片。";
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaximumImageSizeBytes)
        {
            error = "单张图片不能超过 10MB。";
            return false;
        }

        attachment = new FeedbackImageAttachment(fileInfo.FullName, fileInfo.Name, fileInfo.Length);
        return true;
    }
}
