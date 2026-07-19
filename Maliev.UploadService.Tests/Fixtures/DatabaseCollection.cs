using Maliev.Aspire.ServiceDefaults.IAM;
using Xunit;

namespace Maliev.UploadService.Tests.Fixtures;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<TestWebApplicationFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}


