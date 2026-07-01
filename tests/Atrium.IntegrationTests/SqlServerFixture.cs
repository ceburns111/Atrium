using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Atrium.IntegrationTests;

/// <summary>
/// Spins up one real SQL Server in a throwaway Docker container for the whole test run (via
/// Testcontainers), then hands out a connection string per logical database. Sharing a single
/// container across the integration classes keeps the ~seconds of startup cost paid once; each class
/// still provisions its <em>own</em> database on it, so their schemas and data never collide.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-latest"
    ).Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A connection string to the named database on the shared container (DbUp creates it).</summary>
    public string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = database,
            TrustServerCertificate = true,
        }.ConnectionString;
}

/// <summary>The xUnit collection that shares one <see cref="SqlServerFixture"/> across the DB-backed tests.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql-server";
}
