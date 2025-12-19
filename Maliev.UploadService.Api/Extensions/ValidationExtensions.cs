using System.Text.RegularExpressions;

namespace Maliev.UploadService.Api.Extensions;

public static partial class ValidationExtensions
{
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars()
        .Concat(new[] { '<', '>', ':', '"', '|', '?', '*' })
        .ToArray();

    private static readonly string[] DangerousPatterns = new[]
    {
        "..",           // Path traversal
        "~",            // Home directory reference
        "\\\\",         // UNC paths
        "//",           // Double slashes
    };

    /// <summary>
    /// Sanitizes a file path to prevent path traversal and injection attacks
    /// </summary>
    public static string SanitizePath(this string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
        }

        // Remove leading/trailing whitespace
        path = path.Trim();

        // Normalize path separators to forward slash
        path = path.Replace('\\', '/');

        // Remove any leading slashes
        path = path.TrimStart('/');

        // Check for path traversal attempts
        if (DangerousPatterns.Any(pattern => path.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Path contains dangerous patterns", nameof(path));
        }

        // Check for invalid characters
        if (InvalidPathChars.Any(c => path.Contains(c)))
        {
            throw new ArgumentException("Path contains invalid characters", nameof(path));
        }

        // Check for absolute paths (Windows drive letters or Unix root)
        if (AbsolutePathRegex().IsMatch(path))
        {
            throw new ArgumentException("Absolute paths are not allowed", nameof(path));
        }

        // Prevent paths that start with special characters
        if (path.StartsWith('.') || path.StartsWith('~'))
        {
            throw new ArgumentException("Path cannot start with '.' or '~'", nameof(path));
        }

        // Normalize consecutive slashes
        path = MultipleSlashRegex().Replace(path, "/");

        // Remove trailing slashes
        path = path.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is empty after sanitization", nameof(path));
        }

        return path;
    }

    /// <summary>
    /// Validates that a path is safe and doesn't contain malicious content
    /// </summary>
    public static bool IsValidPath(this string path)
    {
        try
        {
            path.SanitizePath();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves placeholders in a path (e.g., {id}, {timestamp})
    /// </summary>
    public static string ResolvePlaceholders(this string path, Dictionary<string, string> placeholders)
    {
        if (placeholders == null || placeholders.Count == 0)
        {
            return path;
        }

        foreach (var (key, value) in placeholders)
        {
            // Sanitize the value before replacing
            var sanitizedValue = value.Replace("/", "_").Replace("\\", "_");
            path = path.Replace($"{{{key}}}", sanitizedValue, StringComparison.OrdinalIgnoreCase);
        }

        // Ensure no unresolved placeholders remain
        if (PlaceholderRegex().IsMatch(path))
        {
            throw new ArgumentException("Path contains unresolved placeholders", nameof(path));
        }

        return path;
    }

    [GeneratedRegex(@"^[a-zA-Z]:[\\/]|^/", RegexOptions.Compiled)]
    private static partial Regex AbsolutePathRegex();

    [GeneratedRegex(@"/+", RegexOptions.Compiled)]
    private static partial Regex MultipleSlashRegex();

    [GeneratedRegex(@"\{[^}]+\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}
