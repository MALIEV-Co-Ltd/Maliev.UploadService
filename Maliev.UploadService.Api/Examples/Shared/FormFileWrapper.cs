using Microsoft.AspNetCore.Http;

namespace Maliev.UploadService.Api.Examples.Shared;

/// <summary>
/// Helper class to wrap byte arrays as IFormFile for internal operations
/// Shared across all example domain services
/// </summary>
public class FormFileWrapper : IFormFile
{
    private readonly Stream _stream;
    public string ContentType { get; }
    public string ContentDisposition => $"form-data; filename=\"{FileName}\"";
    public IHeaderDictionary Headers { get; } = new HeaderDictionary();
    public long Length => _stream.Length;
    public string Name { get; } = "file";
    public string FileName { get; }

    public FormFileWrapper(Stream stream, string fileName, string contentType)
    {
        _stream = stream;
        FileName = fileName;
        ContentType = contentType;
    }

    public void CopyTo(Stream target) => _stream.CopyTo(target);
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => _stream.CopyToAsync(target, cancellationToken);
    public Stream OpenReadStream() => _stream;
}