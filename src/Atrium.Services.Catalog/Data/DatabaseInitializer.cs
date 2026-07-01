using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

namespace Atrium.Services.Catalog.Data;

/// <summary>
/// Applies the database on startup with DbUp, in two disciplined lanes:
/// <list type="bullet">
/// <item><b>Migrations</b> — run-once, journaled (schema + seed). Each runs at most once, ever.</item>
/// <item><b>Programmability</b> — run-always, not journaled (stored procedures via CREATE OR ALTER),
/// so procs are redeployed to their latest definition on every start.</item>
/// </list>
/// This is the "sprocs, but disciplined" split: schema changes are versioned and irreversible; the
/// programmable surface is declarative and always current.
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
