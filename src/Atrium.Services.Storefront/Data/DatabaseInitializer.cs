using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Storefront.Data;

/// <summary>
/// Applies this service's own database with DbUp: Migrations run once (schema), Programmability runs
/// always (stored procedures via CREATE OR ALTER). Same disciplined split as the Catalog service.
/// </summary>
public static class DatabaseInitializer
{
    public static void Initialize(string connectionString, ILogger logger)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var assembly = Assembly.GetExecutingAssembly();

        Run(
            "migrations",
            DeployChanges
                .To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name => name.Contains(".Migrations."))
                .WithTransactionPerScript()
                .LogToConsole()
                .Build(),
            logger
        );

        Run(
            "programmability",
            DeployChanges
                .To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name => name.Contains(".Programmability."))
                .JournalTo(new NullJournal())
                .WithTransactionPerScript()
                .LogToConsole()
                .Build(),
            logger
        );
    }

    private static void Run(string lane, UpgradeEngine engine, ILogger logger)
    {
        var result = engine.PerformUpgrade();
        if (!result.Successful)
        {
            logger.LogError(
                result.Error,
                "Database {Lane} upgrade failed after {ScriptCount} script(s)",
                lane,
                result.Scripts.Count()
            );
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        logger.LogInformation(
            "Database {Lane} upgrade applied {ScriptCount} script(s)",
            lane,
            result.Scripts.Count()
        );
    }
}
