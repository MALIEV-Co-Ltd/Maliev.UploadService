using Prometheus;

namespace Maliev.SupplierService.Api.Services;

public static class SupplierMetrics
{
    private static readonly string ServiceName = "supplier-service";
    private static readonly string Version = typeof(SupplierMetrics).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // Counter for supplier operations
    public static readonly Counter SupplierOperationsTotal = Metrics.CreateCounter(
        "supplier_operations_total",
        "Total number of supplier operations",
        new CounterConfiguration
        {
            LabelNames = ["operation", "status", "service_name", "version"]
        });

    // Counter for evaluations
    public static readonly Counter SupplierEvaluationsTotal = Metrics.CreateCounter(
        "supplier_evaluations_total",
        "Total number of supplier evaluations recorded",
        new CounterConfiguration
        {
            LabelNames = ["rating_category", "service_name", "version"]
        });

    // Gauge for suppliers by status
    public static readonly Gauge SuppliersByStatus = Metrics.CreateGauge(
        "suppliers_by_status",
        "Current count of suppliers by status",
        new GaugeConfiguration
        {
            LabelNames = ["status", "service_name", "version"]
        });

    // Gauge for expiring certifications
    public static readonly Gauge CertificationsExpiringSoon = Metrics.CreateGauge(
        "certifications_expiring_soon",
        "Count of certifications expiring within specified days",
        new GaugeConfiguration
        {
            LabelNames = ["days", "service_name", "version"]
        });

    // Histogram for operation duration
    public static readonly Histogram SupplierOperationDuration = Metrics.CreateHistogram(
        "supplier_operation_duration_seconds",
        "Duration of supplier operations in seconds",
        new HistogramConfiguration
        {
            LabelNames = ["operation", "service_name", "version"],
            Buckets = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0]
        });

    // Histogram for dependency check duration
    public static readonly Histogram DependencyCheckDuration = Metrics.CreateHistogram(
        "dependency_check_duration_seconds",
        "Duration of external service dependency checks",
        new HistogramConfiguration
        {
            LabelNames = ["service", "service_name", "version"],
            Buckets = [0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0]
        });

    public static void RecordOperation(string operation, string status)
    {
        SupplierOperationsTotal.WithLabels(operation, status, ServiceName, Version).Inc();
    }

    public static void RecordEvaluation(string ratingCategory)
    {
        SupplierEvaluationsTotal.WithLabels(ratingCategory, ServiceName, Version).Inc();
    }

    public static void UpdateSuppliersByStatus(string status, double count)
    {
        SuppliersByStatus.WithLabels(status, ServiceName, Version).Set(count);
    }

    public static void UpdateCertificationsExpiring(int days, double count)
    {
        CertificationsExpiringSoon.WithLabels(days.ToString(), ServiceName, Version).Set(count);
    }

    public static IDisposable MeasureOperationDuration(string operation)
    {
        return SupplierOperationDuration.WithLabels(operation, ServiceName, Version).NewTimer();
    }

    public static IDisposable MeasureDependencyCheck(string service)
    {
        return DependencyCheckDuration.WithLabels(service, ServiceName, Version).NewTimer();
    }
}
