using AnujShroff.CDCMigrationGuard.Models;

namespace AnujShroff.CDCMigrationGuard.Output;

public interface IReportFormatter
{
    string Format(string sourceName, string destName, List<DiffResult> results);
}
