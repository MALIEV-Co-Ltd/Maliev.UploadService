using System.Net.Http.Json;

namespace Maliev.SupplierService.Api.Services.ExternalServices;

public class InvoiceServiceClient : IInvoiceServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InvoiceServiceClient> _logger;

    public InvoiceServiceClient(HttpClient httpClient, ILogger<InvoiceServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DependencyCheckResult> CheckReferencesAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/suppliers/{supplierId}/references", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ReferenceCheckResponse>(cancellationToken: cancellationToken);
                return new DependencyCheckResult(
                    HasReferences: result?.ReferenceCount > 0,
                    ServiceName: "InvoiceService",
                    ReferenceCount: result?.ReferenceCount ?? 0,
                    ErrorMessage: null,
                    ServiceUnavailable: false);
            }

            _logger.LogWarning("InvoiceService returned {StatusCode} for supplier {SupplierId}",
                response.StatusCode, supplierId);

            return new DependencyCheckResult(
                HasReferences: false,
                ServiceName: "InvoiceService",
                ReferenceCount: 0,
                ErrorMessage: $"Service returned {response.StatusCode}",
                ServiceUnavailable: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check references with InvoiceService for supplier {SupplierId}", supplierId);
            return new DependencyCheckResult(
                HasReferences: false,
                ServiceName: "InvoiceService",
                ReferenceCount: 0,
                ErrorMessage: ex.Message,
                ServiceUnavailable: true);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            _logger.LogError(ex, "Timeout checking references with InvoiceService for supplier {SupplierId}", supplierId);
            return new DependencyCheckResult(
                HasReferences: false,
                ServiceName: "InvoiceService",
                ReferenceCount: 0,
                ErrorMessage: "Request timed out",
                ServiceUnavailable: true);
        }
    }

    private record ReferenceCheckResponse(int ReferenceCount);
}
