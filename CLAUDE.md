## Project

WPF desktop app (.NET 10, Windows only) that parses Excel order forms and imports customers and order positions into Bexio through its REST API. Solution: `BexioOrderImport.slnx`.

- `src/BexioOrderImport.Domain`: models (`Order`, `OrderPosition`, Bexio DTOs)
- `src/BexioOrderImport.Abstractions`: shared contracts (`IBexioService`, `IFormatAdapter`, `IFormatRegistry`, `IImportUserInteractionService`, `ImportResult`), `ExcelColumnHelper`
- `src/BexioOrderImport.Abstractions.Wpf`: UI contracts of the formats (`IFormatUiProvider`, `IProfileConfigControl`, `IImportPreviewControl`), `ViewModelBase`, `RelayCommand`, converters and the shared theme `Styles/AppStyles.xaml`
- `src/BexioOrderImport.Format.Peakmile`: Peakmile import format (`PeakmileFormatAdapter`, `PeakmileSettings`, `PeakmileExcelParser`, `PeakmileImporter`), its profile editor and preview controls (`PeakmileUiProvider`) and its own translations
- `src/BexioOrderImport.Format.EanList`: EAN list import format (`EanListFormatAdapter`, `EanListSettings`, `EanListExcelParser`, `EanListImporter`, `EanListUiProvider`) with its own translations
- `src/BexioOrderImport.Infrastructure`: `BexioApiClient`
- `src/BexioOrderImport.Wpf`: MVVM UI, mapping profiles (`ProfileSerializer`), settings, auto-updater; registers the formats in `App.xaml.cs`
- `src/BexioOrderImport.Tests`: host tests (Bexio client, profiles and settings, registries, view models, host views) with TUnit, Moq and FluentAssertions. They use `Utils/FakeFormat.cs` instead of a real format; only the legacy profile migration uses Peakmile.
- `src/BexioOrderImport.Format.Peakmile.Tests`: Peakmile parser, import, settings, controls and translations, with the read-only fixture `Fixtures/PeakmileOrder.xlsx`
- `src/BexioOrderImport.Format.EanList.Tests`: EAN list parser, import, view models, controls and translations, with the read-only fixture `Fixtures/EanListOrder.xlsx`

`src/BexioOrderImport.Cli` holds only build output and is not part of the solution.

Human docs: `README.md`, `CONTRIBUTING.md`, `doc/UserManual.md`. Describe user-visible changes in the user manual.

## Build and run

```powershell
dotnet build BexioOrderImport.slnx
dotnet run --project src\BexioOrderImport.Wpf\BexioOrderImport.Wpf.csproj
```

## Tests

Run the tests from the repository root with the coverage script (`dotnet test` does not work with this SDK). It runs every test project listed in the script and merges the coverage into `TestResults/coverage.cobertura.xml`:

```powershell
powershell -ExecutionPolicy Bypass -File build\run-coverage.ps1
```

Run selected tests only (the filter path is assembly/namespace/class/test):

```powershell
dotnet run --project src\BexioOrderImport.Format.Peakmile.Tests -- --treenode-filter "/*/*/*/*Fallback*"
```

- Pull requests fail below 90% code coverage, so new logic needs tests.
- A new test project must be added to `$testProjects` in `build\run-coverage.ps1`.
- Fixtures are never written by tests; tests that need a changed workbook save a temp copy.
- Tests never use the real `%LocalAppData%\BexioOrderImport\appsettings.json`; view model tests pass a temp config path.
- Moq `Setup`/`Verify` calls must pass optional parameters explicitly, eg. `It.IsAny<IReadOnlyList<string>>()` for the fallback codes of `IPeakmileArticleMatcher.FindArticleAsync`.

## Conventions

- Work on a branch and open a pull request against `main`. Pull requests are squash merged, so the PR title must be a Conventional Commit.
- Commit messages follow Conventional Commits (`feat:`, `fix:`, `test:`, `ci:`). A Husky.NET `commit-msg` hook enforces this locally.
- The version lives in `Directory.Build.props`. Pushing a `v*` tag runs `.github/workflows/release.yml`, which builds the installer and publishes the GitHub release. Do not bump the version or tag unless asked.
- Keep the implementation simple: standard library over custom utilities, no new dependencies without need.
- User-facing strings are never hardcoded in XAML. Each string needs three entries: `Translations.resx` (German, default), `Translations.en.resx` (English) and a property in `Translations.cs`. Strings of a format go into the format's own triple (eg. `PeakmileTranslations.*`), not into the host. German uses Swiss spelling ("ss", never "ß"); `TranslationIntegrityTests` checks both.
- Format controls use the keys of the shared theme (`Abstractions.Wpf/Styles/AppStyles.xaml`), which the host merges in `App.xaml`. `FormatControlsLoadTests` loads every format and host view with that theme.

## Adding a profile setting

Profiles store a format id (`Format`) and the format's settings (`Settings`) in `%LocalAppData%\BexioOrderImport\appsettings.json` and in profile exports. Profiles from v1.6.0 and older have no format and keep their Peakmile settings in `ExcelMapping`; `ProfileSerializer` still reads them. For a Peakmile setting:

1. Add the property with a default to `PeakmileSettings`. It is serialized as is, so existing profiles must load without it.
2. Add it to `ExcelMappingEvaluator.CopyOptions`, which copies property by property, and extend `CopyOptions_ShouldCopyAllValues`.
3. Add the input to `PeakmileProfileConfigControl.xaml` and load, validate and save it in `PeakmileProfileConfigViewModel`. `ProfileEditWindow` only hosts the format's control and owns the profile name.
4. Add the label and hint to the three `PeakmileTranslations` files.
5. If the import needs it, add it to `PeakmileImportOptions` and map it in `PeakmileFormatAdapter.CreateImportOptions`.

## Bexio article lookup

`PeakmileArticleMatcher.FindArticleAsync` (in the Peakmile format) matches the Bexio `intern_code` exactly as `{SeasonCode}{ArticleNumber}{Color}`, first with the season code of the profile, then with each season fallback code (multi-season articles, eg. `NOOS`, `EX`). Only if all miss, it searches by article number and filters by color and season code in the article name. An article that is not found aborts the import. It uses only the generic `IBexioService.FindArticlesByInternCodeAsync` and `SearchArticlesAsync`, and caches the articles of one import; `PeakmileArticleMatcherTests` run it against the real `BexioApiClient` with a mocked HTTP handler.

The EAN list format uses `FindArticlesByInternCodeAsync(ean)`: the EAN is the `intern_code`, exactly one article must match, and the position price is the article's `sale_price`.

## Order and customer number

`OrderTargetResolver` (in `Abstractions/Import`) is shared by the formats: with an order number it uses that existing order (a customer number must match its contact), otherwise with a customer number it creates a new order for that contact. Peakmile adds its email lookup and customer creation when neither number is set; the EAN list cancels without a customer number. Keep the log messages and results of the resolver unchanged, the Peakmile import tests assert them.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
