using Microsoft.AspNetCore.Components.Forms;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Files;

public sealed class InvoiceFileService(IConfiguration configuration)
{
    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    private readonly long _maxFileSize =
        configuration.GetValue<long?>("FileStorage:MaxFileSizeBytes") ?? 2 * 1024 * 1024;

    private readonly string _rootPath = ResolveRootPath(configuration);

    public async Task<InvoiceFileInfo> SaveAsync(
        IBrowserFile file,
        MongoDB.Bson.ObjectId itemId,
        CancellationToken cancellationToken = default)
    {
        if (file.Size <= 0 || file.Size > _maxFileSize)
            throw new InvalidOperationException($"發票檔案大小不得超過 {_maxFileSize / 1024 / 1024}MB。");

        if (!AllowedContentTypes.TryGetValue(file.ContentType, out var extension))
            throw new InvalidOperationException("只允許 JPG、PNG、WEBP 圖片檔案。");

        var now = DateTime.Now;
        var folder = Path.Combine(_rootPath, now.ToString("yyyy"), now.ToString("MM"));
        Directory.CreateDirectory(folder);

        var fileName = itemId.ToString() + extension;
        var fullPath = Path.Combine(folder, fileName);

        await using var source = file.OpenReadStream(_maxFileSize, cancellationToken);
        await using var target = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        await source.CopyToAsync(target, cancellationToken);

        return new InvoiceFileInfo
        {
            RelativePath = Path.Combine(now.ToString("yyyy"), now.ToString("MM"), fileName).Replace('\\', '/'),
            OriginalFileName = Path.GetFileName(file.Name),
            ContentType = file.ContentType,
            Size = file.Size
        };
    }

    public Task DeleteAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public string? GetFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var candidate = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var root = Path.GetFullPath(_rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }

    private static string ResolveRootPath(IConfiguration configuration)
    {
        var configured = configuration["FileStorage:RootPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (configured.StartsWith("~/", StringComparison.Ordinal))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, configured[2..]);
            }
            return configured;
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userHome, "dotCheck");
    }
}
