## Project

WPF desktop app (.NET 10, Windows only) that parses Excel order forms and imports customers and order positions into Bexio through its REST API. Solution: `BexioOrderImport.slnx`.

- `src/BexioOrderImport.Domain`: models (`Order`, `OrderPosition`, Bexio DTOs)
- `src/BexioOrderImport.Abstractions`: shared contracts (`IBexioService`, `IFormatAdapter`, `IFormatRegistry`, `IImportUserInteractionService`, `ImportResult`), `ExcelColumnHelper`
- `src/BexioOrderImport.Format.Peakmile`: Peakmile import format (`PeakmileFormatAdapter`, `PeakmileSettings`, `PeakmileExcelParser`, `PeakmileImporter`) with its own translations
- `src/BexioOrderImport.Infrastructure`: `BexioApiClient`
- `src/BexioOrderImport.Wpf`: MVVM UI, mapping profiles (`ProfileSerializer`), settings, auto-updater; registers the formats in `App.xaml.cs`
- `src/BexioOrderImport.Tests`: TUnit tests with Moq and FluentAssertions

`src/BexioOrderImport.Cli` holds only build output and is not part of the solution.

Human docs: `README.md`, `CONTRIBUTING.md`, `doc/UserManual.md`. Describe user-visible changes in the user manual.

## Build and run

```powershell
dotnet build BexioOrderImport.slnx
dotnet run --project src\BexioOrderImport.Wpf\BexioOrderImport.Wpf.csproj
```

## Tests

Run the tests from the repository root with the coverage script (`dotnet test` does not work with this SDK):

```powershell
powershell -ExecutionPolicy Bypass -File build\run-coverage.ps1
```

Run selected tests only (the filter path is assembly/namespace/class/test):

```powershell
dotnet run --project src\BexioOrderImport.Tests -- --treenode-filter "/*/*/*/*Fallback*"
```

- Pull requests fail below 90% code coverage, so new logic needs tests.
- A test run modifies `src/BexioOrderImport.Tests/AnonymizedOrder.xlsx`. Restore it with `git checkout -- src/BexioOrderImport.Tests/AnonymizedOrder.xlsx` and do not commit it.
- Moq `Setup`/`Verify` calls must pass optional parameters explicitly, eg. `It.IsAny<IReadOnlyList<string>>()` for the fallback codes of `IBexioService.FindArticleAsync`.

## Conventions

- Work on a branch and open a pull request against `main`. Pull requests are squash merged, so the PR title must be a Conventional Commit.
- Commit messages follow Conventional Commits (`feat:`, `fix:`, `test:`, `ci:`). A Husky.NET `commit-msg` hook enforces this locally.
- The version lives in `Directory.Build.props`. Pushing a `v*` tag runs `.github/workflows/release.yml`, which builds the installer and publishes the GitHub release. Do not bump the version or tag unless asked.
- Keep the implementation simple: standard library over custom utilities, no new dependencies without need.
- User-facing strings are never hardcoded in XAML. Each string needs three entries: `Translations.resx` (German, default), `Translations.en.resx` (English) and a property in `Translations.cs`.

## Adding a profile setting

Profiles store a format id (`Format`) and the format's settings (`Settings`) in `%LocalAppData%\BexioOrderImport\appsettings.json` and in profile exports. Profiles from v1.6.0 and older have no format and keep their Peakmile settings in `ExcelMapping`; `ProfileSerializer` still reads them. For a Peakmile setting:

1. Add the property with a default to `PeakmileSettings`. It is serialized as is, so existing profiles must load without it.
2. Add it to `ExcelMappingEvaluator.CopyOptions`, which copies property by property, and extend `CopyOptions_ShouldCopyAllValues`.
3. Add the input to `ProfileEditWindow.xaml` and load/save it in `ProfileEditWindow.xaml.cs`.
4. Add the label and hint to the three translation files.
5. If the import needs it, add it to `PeakmileImportOptions` and map it in `PeakmileFormatAdapter.CreateImportOptions`.

## Bexio article lookup

`BexioApiClient.FindArticleAsync` matches the Bexio `intern_code` exactly as `{SeasonCode}{ArticleNumber}{Color}`, first with the season code of the profile, then with each season fallback code (multi-season articles, eg. `NOOS`, `EX`). Only if all miss, it searches by article number and filters by color and season code in the article name. An article that is not found aborts the import.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
