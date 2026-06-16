using Xunit;

namespace WebHomestay.Tests.Infrastructure;

[CollectionDefinition("postgres-integration")]
public sealed class PostgresIntegrationCollection : ICollectionFixture<PostgresIntegrationFixture>
{
}
