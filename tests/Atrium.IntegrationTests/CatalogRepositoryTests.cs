using Atrium.Contracts;
using Atrium.Services.Catalog.Catalog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using CatalogDb = Atrium.ServiceDefaults.DatabaseInitializer;

namespace Atrium.IntegrationTests;

/// <summary>
/// I1 — the Catalog data path end to end against a real SQL Server: DbUp provisions the schema, seed,
/// and stored procedures (embedded in the service assembly), the real <see cref="CatalogRepository"/>
/// runs the sprocs through Dapper, and Mapperly maps the row back to a DTO. Concept on show: an
/// integration test that trusts nothing but the database — including the sproc's own error path.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CatalogRepositoryTests : IAsyncLifetime
{
    private readonly string _connectionString;

    public CatalogRepositoryTests(SqlServerFixture sql) =>
        _connectionString = sql.ConnectionStringFor("catalog_test");

    // Runs once per test; DbUp is idempotent (migrations journaled, sprocs CREATE OR ALTER).
    public ValueTask InitializeAsync()
    {
        CatalogDb.Initialize(
            _connectionString,
            typeof(CatalogRepository).Assembly,
            NullLogger.Instance
        );
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private CatalogRepository NewRepository() =>
        new(new SqlConnection(_connectionString), NullLogger<CatalogRepository>.Instance);

    [Fact]
    public async Task Create_then_list_round_trips_the_row_with_its_category_name()
    {
        var repository = NewRepository();
        var request = new CreateProductRequest(
            "Test Widget",
            "Desk",
            42.50m,
            "A widget for testing."
        );

        var created = await repository.CreateProductAsync(request);

        // The write sproc SELECTs the row back, joined to Categories; Mapperly renames CategoryName -> Category.
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
        Assert.Equal("Test Widget", created.Name);
        Assert.Equal("Desk", created.Category);
        Assert.Equal(42.50m, created.Price);

        // And it is really persisted: a fresh read via the filtered list sproc finds it.
        var deskProducts = await NewRepository().GetProductsAsync("Desk");
        Assert.Contains(deskProducts, p => p.Id == created.Id && p.Category == "Desk");
    }

    [Fact]
    public async Task Create_with_an_unknown_category_surfaces_the_sprocs_raiserror()
    {
        var repository = NewRepository();
        var request = new CreateProductRequest("Orphan", "NoSuchCategory", 10m, "blurb");

        // usp_Product_Create does `THROW 50001` when the category name resolves to no id.
        var ex = await Assert.ThrowsAsync<SqlException>(() =>
            repository.CreateProductAsync(request)
        );
        Assert.Equal(50001, ex.Number);
    }
}
