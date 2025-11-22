using System.Net.Http.Json;

namespace Maliev.SupplierService.Api.Services.ExternalServices;

public class PurchaseOrderServiceClient : IPurchaseOrderServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PurchaseOrderServiceClient> _logger;

    public PurchaseOrderServiceClient(HttpClient httpClient, ILogger<PurchaseOrderServiceClient> logger)
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
                    ServiceName: "PurchaseOrderService",
                    ReferenceCount: result?.ReferenceCount ?? 0,
                    ErrorMessage: null,
                    ServiceUnavailable: false);
            }

            _logger.LogWarning("PurchaseOrderService returned {StatusCode} for supplier {SupplierId}",
                response.StatusCode, supplierId);

            return new DependencyCheckResult(
                HasReferences: false,
                ServiceName: "PurchaseOrderService",
                ReferenceCount: 0,
                ErrorMessage: $"Service returned {response.StatusCode}",
                ServiceUnavailable: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check references with PurchaseOrderService for supplier {SupplierId}", supplierId);
            return new DependencyCheckResult(
                HasReferences: false,
                ServiceName: "PurchaseOrderService",
                ReferenceCount: 0,
                ErrorMessage: ex.Message,
                ServiceUnavailable: true);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            _logger.LogError(ex, "Timeout checking references with PurchaseOrderService for supplier {SupplierId}", supplierId);
            return new DependencyCheckResult(
                HasReferences: false,
                ServiceName: "PurchaseOrderService",
                ReferenceCount: 0,
                ErrorMessage: "Request timed out",
                ServiceUnavailable: true);
        }
    }

    private record ReferenceCheckResponse(int ReferenceCount);
}
