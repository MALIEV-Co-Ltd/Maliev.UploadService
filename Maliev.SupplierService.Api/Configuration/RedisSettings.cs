namespace Maliev.SupplierService.Api.Configuration;

public class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public bool Enabled { get; set; } = true;
}
