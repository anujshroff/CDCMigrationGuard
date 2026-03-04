# CDCMigrationGuard

A .NET CLI tool that validates SQL Server schema migrations against CDC (Change Data Capture) tracked tables. It compares source (post-migration) and destination (pre-migration) database schemas to detect breaking changes that could cause migration failures or data loss in systems using CDC for replication.

## Features

- **Column Change Detection**: Identifies added, dropped, renamed, and type-changed columns on CDC-tracked tables
- **Nullability Validation**: Detects nullability changes on tracked columns
- **Primary Key Monitoring**: Catches primary key modifications that would break CDC logic
- **Table-Level Detection**: Identifies dropped, renamed, and schema-changed tables
- **CDC Index Validation**: Detects changes to CDC capture instance indexes
- **Capture Instance Warnings**: Alerts when multiple capture instances exist on a table
- **Multiple Output Formats**: Text (color-coded console), Markdown, and JSON
- **CI/CD Integration**: Exit codes indicate severity for use in automated pipelines

## Detected Issues

| Issue | Severity | Description |
|-------|----------|-------------|
| Column Added | Low | New column on a tracked table is not automatically tracked by CDC |
| Column Dropped | Critical | A tracked column was removed; requires CDC disable/re-enable |
| Column Type Changed | High | Data type, length, or precision changed on a tracked column |
| Column Renamed | Critical | Heuristic detection via matching type and ordinal position; requires CDC recreation |
| Nullability Changed | Low | NOT NULL / NULL change on a tracked column |
| Primary Key Changed | Critical | Primary key columns differ between source and destination |
| Table Dropped | Critical | A CDC-tracked table is missing from the source schema |
| Table Renamed | Critical | Heuristic detection when a tracked table is missing but a similar-name table exists |
| Table Schema Changed | Critical | A tracked table exists under a different schema in the source |
| CDC Index Changed | Critical | CDC capture instance index columns were modified or removed |
| Capture Instance Limit | Info | Multiple capture instances exist on a table |

## Technologies

- **.NET 10.0** - Target framework
- **Microsoft.Data.SqlClient** - SQL Server database access
- **Spectre.Console** - Rich console output with tables and colors
- **System.CommandLine** - CLI argument and option parsing
- **xUnit** - Test framework
- **Testcontainers.MsSql** - Containerized SQL Server for integration testing

## Installation

### As a .NET Global Tool

```bash
dotnet tool install --global AnujShroff.CDCMigrationGuard
```

### As a .NET Local Tool

```bash
dotnet new tool-manifest # if you don't have one already
dotnet tool install AnujShroff.CDCMigrationGuard
```

## Prerequisites

- **.NET 10.0 SDK**
- **SQL Server** with CDC enabled on the destination database

## Building from Source

### Clone and Build

```bash
git clone https://github.com/anujshroff/CDCMigrationGuard.git
cd CDCMigrationGuard
dotnet build
```

### Run Tests

Tests use Testcontainers to spin up SQL Server 2022 instances, so Docker must be available.

```bash
dotnet test
```

## Usage

### Check Connectivity

Verify a connection string can reach the database:

Using SQL authentication:

```bash
cdcmigrationguard check "Server=localhost;Database=mydb;User Id=sa;Password=pass;TrustServerCertificate=True"
```

Using Active Directory Default authentication (supports Managed Identity, Workload Identity, Azure CLI, and other Azure Identity methods):

```bash
cdcmigrationguard check "Server=your-server.database.windows.net;Database=mydb;Authentication=Active Directory Default;Encrypt=True"
```

### Compare Schemas

Compare a source (post-migration) database against a destination (pre-migration, CDC-tracked) database:

Using SQL authentication:

```bash
cdcmigrationguard compare "Server=source;Database=db;User Id=sa;Password=pass;TrustServerCertificate=True" "Server=dest;Database=db;User Id=sa;Password=pass;TrustServerCertificate=True"
```

Using Active Directory Default authentication:

```bash
cdcmigrationguard compare "Server=source.database.windows.net;Database=db;Authentication=Active Directory Default;Encrypt=True" "Server=dest.database.windows.net;Database=db;Authentication=Active Directory Default;Encrypt=True"
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--format` | Output format: `text`, `markdown`, or `json` | `text` |
| `--output` | Write report to a file instead of stdout | - |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | No critical issues found |
| 1 | Errors or warnings detected |
| 2 | Critical issues found; migration will likely fail |

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## AI Notice

This project was entirely generated using AI, leveraging **Claude Code** with **Claude Opus 4.6** by Anthropic. It serves as a testament to the capabilities of modern AI in automating complex development tasks and streamlining the software creation process.
