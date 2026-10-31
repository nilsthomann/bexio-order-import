## Project

WPF desktop app (.NET 10, Windows only) that parses Excel order forms and imports customers and order positions into Bexio through its REST API. Solution: `BexioOrderImport.slnx`.

- `src/BexioOrderImport.Domain`: models (`Order`, `OrderPosition`, Bexio DTOs)
- `src/BexioOrderImport.Abstractions`: shared contracts (`IBexioService`, `IFormatAdapter`, `IFormatRegistry`, `IImportUserInteractionService`, `ImportResult`), `ExcelColumnHelper`
- `src/BexioOrderImport.Abstractions.Wpf`: UI contracts of the formats (`IFormatUiProvider`, `IProfileConfigControl`, `IImportPreviewControl`), `ViewModelBase`, `RelayCommand`, converters and the shared theme `Styles/AppStyles.xaml`
- `src/BexioOrderImport.Format.Peakmile`: Peakmile import format (`PeakmileFormatAdapter`, `PeakmileSettings`, `PeakmileExcelParser`, `PeakmileImporter`), its profile editor and preview controls (`PeakmileUiProvider`) and its own translations
- `src/BexioOrderImport.Format.EanList`: EAN list import format (`EanListFormatAdapter`, `EanListSettings`, `EanListExcelParser`, `EanListImporter`, `EanListUiProvider`) with its own translations
- `src/BexioOrderImport.Format.Sgd`: SGD (Sports Group Denmark) import format (`SgdFormatAdapter`, `SgdSettings`, `SgdExcelParser`, `SgdArticleMatcher`, `SgdImporter`, `SgdUiProvider`) with its own translations
- `src/BexioOrderImport.Infrastructure`: `BexioApiClient`
- `src/BexioOrderImport.Wpf`: MVVM UI, mapping profiles (`ProfileSerializer`), settings, auto-updater; registers the formats in `App.xaml.cs`
- `src/BexioOrderImport.Tests`: host tests (Bexio client, profiles and settings, registries, view models, host views) with TUnit, Moq and FluentAssertions. They use `Utils/FakeFormat.cs` instead of a real format; only the legacy profile migration uses Peakmile.
- `src/BexioOrderImport.Format.Peakmile.Tests`: Peakmile parser, import, settings, controls and translations, with the read-only fixture `Fixtures/PeakmileOrder.xlsx`
- `src/BexioOrderImport.Format.EanList.Tests`: EAN list parser, import, view models, controls and translations, with the read-only fixture `Fixtures/EanListOrder.xlsx`
- `src/BexioOrderImport.Format.Sgd.Tests`: SGD parser, article matcher, import, view models, controls and translations, with the read-only fixture `Fixtures/SgdOrder.xlsx`

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
- Tests never use the real `%LocalAppData%\BexioOrderImport` files; view model tests pass a temp config path, `UpdateServiceTests` pass temp folders for the check cache and the installer download.
- Moq `Setup`/`Verify` calls must pass optional parameters explicitly, eg. `It.IsAny<string?>()` for the position text of `IBexioService.AddArticlePositionAsync`.

## Conventions

- Work on a branch and open a pull request against `main`. Pull requests are squash merged, so the PR title must be a Conventional Commit.
- Commit messages follow Conventional Commits (`feat:`, `fix:`, `test:`, `ci:`). A Husky.NET `commit-msg` hook enforces this locally.
- The version lives in `Directory.Build.props`. Pushing a `v*` tag runs `.github/workflows/release.yml`, which builds the installer and publishes the GitHub release. Do not bump the version or tag unless asked.
- Keep the implementation simple: standard library over custom utilities, no new dependencies without need.
- Formats are independent: a format project never references another format project, and each format has its own article matcher, even if its logic equals another format's (eg. `SgdArticleMatcher` is a copy of `PeakmileArticleMatcher`). Share only through the Abstractions projects.
- Builds run the Recommended .NET analyzers and the code style rules of `.editorconfig` (`Directory.Build.props`), and unused usings, members, parameters and values are warnings. Keep the build free of warnings; suppress a false positive with a justified `SuppressMessage` (eg. `PeakmileSettings.LegacyPositionTextTemplate`, which only System.Text.Json sets).
- Parse data from Excel cells, ids and files with an explicit culture; UI texts are formatted with the culture of the user.
- User-facing strings are never hardcoded in XAML. Each string needs three entries: `Translations.resx` (German, default), `Translations.en.resx` (English) and a property in `Translations.cs`. Strings of a format go into the format's own triple (eg. `PeakmileTranslations.*`), not into the host. German uses Swiss spelling ("ss", never "ß"); `TranslationIntegrityTests` checks both.
- Format controls use the keys of the shared theme (`Abstractions.Wpf/Styles/AppStyles.xaml`), which the host merges in `App.xaml`. `FormatControlsLoadTests` loads every format and host view with that theme.

## Adding a profile setting

Profiles store a format id (`Format`) and the format's settings (`Settings`) in `%LocalAppData%\BexioOrderImport\appsettings.json` and in profile exports. Profiles from v1.6.0 and older have no format and keep their Peakmile settings in `ExcelMapping`; `ProfileSerializer` still reads them. For a Peakmile setting:

1. Add the property with a default to `PeakmileSettings`. It is serialized as is, so existing profiles must load without it.
2. Add the input to `PeakmileProfileConfigControl.xaml` and load, validate and save it in `PeakmileProfileConfigViewModel`. `ProfileEditWindow` only hosts the format's control and owns the profile name.
3. Add the label and hint to the three `PeakmileTranslations` files.
4. If the import needs it, add it to `PeakmileImportOptions` and map it in `PeakmileFormatAdapter.CreateImportOptions`.

## Bexio article lookup

Each format finds its articles with an `IArticleMatcher<TCriteria>` (`Abstractions/Bexio`): the criteria is a record of the format, the result an `ArticleMatch` with the article or the reason it was not matched (eg. `not found`), which the importer logs before it aborts. The importers take an optional matcher, so a new criterion is a new property of the criteria record, and a fallback is another matcher of the same criteria that runs when the first one finds nothing.

`PeakmileArticleMatcher` (`PeakmileArticleCriteria`) matches the Bexio `intern_code` as `{SeasonCode}{ArticleNumber}{Color}` ignoring whitespace and case, with the color as written (`1001 Black`), only its name (`Black`) or only its code (`1001`), so `FS26 E183801 1001 Black`, `FS26E183801Black` and `FS26E1838011001` all match; first with the season code of the profile, then with each season fallback code (multi-season articles, eg. `NOOS`, `EX`). It never guesses: several articles with the same normalized code are `not unique`, an article of another color or season is not taken. To keep Bexio requests low, it loads articles only with `SearchArticlesAsync` (`like`): once per season code in `PreFetchArticlesAsync` and once per article number that is not found there; all lookups of an import run on these articles. `BexioApiClient.SearchArticlesAsync` loads all result pages (Bexio returns 500 by default). `PeakmileImporter` matches all positions before it changes Bexio and lists every missing article, so a missing article creates neither a customer nor an order. `PeakmileArticleMatcherTests` run the matcher against the real `BexioApiClient` with a mocked HTTP handler and count the searches.

`EanArticleMatcher` (`EanArticleCriteria`) of the EAN list format uses `FindArticlesByInternCodeAsync(ean)`: the EAN is the `intern_code` and exactly one article must match (`ArticleMatch.FromCandidates`). `EanListExcelParser` only accepts valid EAN-13 codes (checked with `Nager.ArticleNumber`) and pads shorter codes with leading zeros first, as Excel drops them from number cells; the matcher therefore compares exactly. The position price is the article's `sale_price`.

`SgdArticleMatcher` (`SgdArticleCriteria`) of the SGD format is a copy of the Peakmile matcher; change both together. `SgdImporter` matches all positions before it changes Bexio and lists every missing article, so a missing article neither creates an order nor deletes positions. The position price is the article's `sale_price`; the discount of the profile is one discount position at the end.

## Order and customer number

`OrderTargetResolver` (in `Abstractions/Import`) is shared by the formats: with an order number it uses that existing order (a customer number must match its contact), otherwise with a customer number it creates a new order for that contact. Peakmile adds its email lookup and customer creation when neither number is set; the EAN list cancels without a customer number. Keep the log messages and results of the resolver unchanged, the Peakmile import tests assert them.

The SGD import asks during the import through `IImportUserInteractionService` (`AskTextAsync`, `ChooseAsync`, `ConfirmWarningAsync`; the host shows them with `CustomDialog`): the customer number (asked again until Bexio finds the contact), then new or existing order, and for an existing order its number and a warning. It then sets both numbers on the order for the resolver and replaces the positions of an existing order with `IBexioService.DeleteAllPositionsAsync` (reads the `positions` of the order and deletes each at the endpoint of its type, eg. `kb_position_article`). It calls `OrderPositionsDeleted()` before deleting, so a failed import shows `Import_OrderReplacedError` (import again) instead of "remove the added positions". Without delegates `DelegateImportUserInteractionService` cancels inputs and declines warnings.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
