using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Maliev.UploadService.Api.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FileValidationAttribute : ActionFilterAttribute
{
    private readonly long _maxFileSize;
    private readonly string[] _allowedContentTypes;
    private readonly string[] _blockedExtensions;

    public FileValidationAttribute(
        long maxFileSize = 100 * 1024 * 1024, // 100MB default
        string[]? allowedContentTypes = null,
        string[]? blockedExtensions = null)
    {
        _maxFileSize = maxFileSize;
        _allowedContentTypes = allowedContentTypes ?? Array.Empty<string>();
        _blockedExtensions = blockedExtensions ?? new[]
        {
            ".exe", ".bat", ".cmd", ".com", ".pif", ".scr", ".vbs", ".js", ".jar",
            ".ps1", ".sh", ".php", ".asp", ".aspx", ".jsp", ".war"
        };
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;

        if (!request.HasFormContentType)
        {
            context.Result = new BadRequestObjectResult(new { error = "Request must be multipart/form-data" });
            return;
        }

        var files = request.Form.Files;
        if (files.Count == 0)
        {
            context.Result = new BadRequestObjectResult(new { error = "No files provided" });
            return;
        }

        foreach (var file in files)
        {
            // File size validation
            if (file.Length == 0)
            {
                context.Result = new BadRequestObjectResult(new { error = "File is empty", fileName = file.FileName });
                return;
            }

            if (file.Length > _maxFileSize)
            {
                context.Result = new BadRequestObjectResult(new
                {
                    error = $"File size exceeds maximum allowed size of {_maxFileSize / (1024 * 1024)}MB",
                    fileName = file.FileName,
                    actualSize = file.Length
                });
                return;
            }

            // File extension validation
            var fileName = file.FileName.ToLowerInvariant();
            if (_blockedExtensions.Any(ext => fileName.EndsWith(ext)))
            {
                context.Result = new BadRequestObjectResult(new
                {
                    error = $"File type '{Path.GetExtension(fileName)}' is not allowed for security reasons",
                    fileName = file.FileName
                });
                return;
            }

            // Content type validation
            if (_allowedContentTypes.Length > 0 && !_allowedContentTypes.Contains(file.ContentType))
            {
                context.Result = new BadRequestObjectResult(new
                {
                    error = "File content type is not allowed",
                    fileName = file.FileName,
                    contentType = file.ContentType,
                    allowedTypes = _allowedContentTypes
                });
                return;
            }

            // Additional security validations
            if (ContainsMaliciousContent(file))
            {
                context.Result = new BadRequestObjectResult(new
                {
                    error = "File contains potentially malicious content",
                    fileName = file.FileName
                });
                return;
            }
        }

        base.OnActionExecuting(context);
    }

    private static bool ContainsMaliciousContent(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            var buffer = new byte[1024];
            stream.Read(buffer, 0, buffer.Length);

            // Check for executable signatures
            var executableSignatures = new[]
            {
                new byte[] { 0x4D, 0x5A }, // PE executable (EXE/DLL)
                new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, // ELF executable
                new byte[] { 0xFE, 0xED, 0xFA, 0xCE }, // Mach-O executable
                new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }, // Java class file
            };

            foreach (var signature in executableSignatures)
            {
                if (buffer.Take(signature.Length).SequenceEqual(signature))
                {
                    return true;
                }
            }

            // Check for script patterns in file content
            var maliciousPatterns = new[]
            {
                System.Text.Encoding.ASCII.GetBytes("<script"),
                System.Text.Encoding.ASCII.GetBytes("javascript:"),
                System.Text.Encoding.ASCII.GetBytes("<?php"),
                System.Text.Encoding.ASCII.GetBytes("<%"),
                System.Text.Encoding.ASCII.GetBytes("eval(")
            };

            foreach (var pattern in maliciousPatterns)
            {
                if (IndexOf(buffer, pattern) != -1)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If we can't read the file, consider it suspicious
            return true;
        }
    }

    private static int IndexOf(byte[] array, byte[] pattern)
    {
        for (int i = 0; i <= array.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (array[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }
}