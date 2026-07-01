using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

namespace Atrium.Services.Storefront.Data;

/// <summary>
/// Applies this service's own database with DbUp: Migrations run once (schema), Programmability runs
/// always (stored procedures via CREATE OR ALTER). Same disciplined split as the Catalog service.
/// </summary>
public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var assembly = Assembly.GetExecutingAssembly();

        Run(
            DeployChanges
                .To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name => name.Contains(".Migrations."))
                .WithTransactionPerScript()
                .LogToConsole()
                .Build()
        );

        Run(
            DeployChanges
                .To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name => name.Contains(".Programmability."))
                .JournalTo(new NullJournal())
                .WithTransactionPerScript()
                .LogToConsole()
                .Build()
        );
    }

    private static void Run(UpgradeEngine engine)
    {
        var result = engine.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }
    }
}
