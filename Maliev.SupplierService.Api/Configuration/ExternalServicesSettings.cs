namespace Maliev.SupplierService.Api.Configuration;

public class ExternalServicesSettings
{
    public const string SectionName = "ExternalServices";

    public ServiceEndpoint PurchaseOrderService { get; set; } = new();
    public ServiceEndpoint InvoiceService { get; set; } = new();
    public ServiceEndpoint StockService { get; set; } = new();
}

public class ServiceEndpoint
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutInSeconds { get; set; } = 30;
}
