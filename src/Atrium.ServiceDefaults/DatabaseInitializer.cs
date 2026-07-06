using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;
using Microsoft.Extensions.Logging;

namespace Atrium.ServiceDefaults;

/// <summary>
/// Applies a service's database on startup with DbUp, in two disciplined lanes:
/// <list type="bullet">
/// <item><b>Migrations</b> — run-once, journaled (schema + seed). Each runs at most once, ever.</item>
/// <item><b>Programmability</b> — run-always, not journaled (stored procedures via CREATE OR ALTER),
/// so procs are redeployed to their latest definition on every start.</item>
/// </list>
/// This is the "sprocs, but disciplined" split: schema changes are versioned and irreversible; the
/// programmable surface is declarative and always current. Shared here so the data-owning services
/// (Catalog, Storefront) get byte-identical behavior instead of copy-drifting their own runners; each
/// passes the assembly that embeds its <c>Data/Scripts</c> SQL files.
/// </summary>
public static class DatabaseInitializer
{
    public static void Initialize(string connectionString, Assembly scriptsAssembly, ILogger logger)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        Run(
            "migrations",
            DeployChanges
                // OPEN QUESTION (from Ted): CAN WE MODULARIZE SO WE CAN EASILY SWAP IN PG?
                // WHAT ELSE WILL WILL WE NEED TO REFACTOR IF WE DO? MAKE SURE TO CONSIDER ACROSS
                // EVERYTHING IN ATRIUM
                .To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    scriptsAssembly,
                    name => name.Contains(".Migrations.")
                )
                .WithTransactionPerScript()
                .LogToConsole()
                .Build(),
            logger
        );

        Run(
            "programmability",
            DeployChanges
                .To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    scriptsAssembly,
                    name => name.Contains(".Programmability.")
                )
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
