# Contributing to Bexio Order Importer

First off, thank you for taking the time to contribute! Contributions are what make the open-source community such an amazing place to learn, inspire, and create.

The following is a set of guidelines for contributing to Bexio Order Importer.

## Code of Conduct

Please help maintain a professional, respectful, and welcoming environment for everyone contributing to this project.

## How Can I Contribute?

### Reporting Bugs
- Search existing issues to ensure the bug hasn't already been reported.
- Open a new issue with a clear title and description, providing as much relevant information as possible, including:
  - Steps to reproduce the issue.
  - Expected vs. actual behavior.
  - Relevant log outputs or screenshots.

### Suggesting Enhancements
- Check existing discussions or issues to see if the feature has been proposed.
- Open a new issue detailing:
  - The goal of the enhancement.
  - A description of how it should behave or look.
  - The reasoning behind why this feature would be beneficial.

### Pull Requests
1. Fork the repository and create your branch from `main`.
2. If you've added code that should be tested, add unit tests.
3. Ensure the test suite passes locally.
4. Format your commit messages clearly and concisely.
5. Submit a pull request against the `main` branch.

---

## Development Guidelines

### Coding Style
- Follow standard C# styling conventions.
- Keep implementation simple and direct. Prefer standard library features over custom utilities, and keep external dependencies to a minimum.

### Testing Changes
- Tests are built using **TUnit** and run on **Microsoft Testing Platform**. Extend or modify tests inside the `src/BexioOrderImport.Tests/` directory when modifying parsing or business workflows.
- Run all tests before submitting your changes:
  ```bash
  dotnet test BexioOrderImport.slnx
  ```
  *or directly via the TUnit executable:*
  ```bash
  dotnet run --project src/BexioOrderImport.Tests/BexioOrderImport.Tests.csproj
  ```
- Run a specific test class:
  ```bash
  dotnet run --project src/BexioOrderImport.Tests/BexioOrderImport.Tests.csproj -- --filter "Name~ImportOrderUseCaseTests"
  ```
- Run a single test:
  ```bash
  dotnet run --project src/BexioOrderImport.Tests/BexioOrderImport.Tests.csproj -- --filter "Name=ExecuteAsync_WithNoPositions_ReturnsFalse"
  ```
- To collect code coverage and generate/view the HTML report locally:
  ```powershell
  ./build/run-coverage.ps1
  ```
- **PR Quality Gate**: Pull requests are automatically analyzed by `bibipkins/dotnet-test-reporter` in CI. PRs with failing tests or code coverage below **90%** will automatically fail and cannot be merged.

### Building the Installer

To compile the Windows installer executable (`BexioOrderImportSetup.exe`) locally:

1. Ensure **Inno Setup 6** is installed on your machine.
2. Publish the WPF application:
   ```bash
   dotnet publish src/BexioOrderImport.Wpf/BexioOrderImport.Wpf.csproj -c Release -o publish
   ```
3. Compile the Inno Setup script:
   ```cmd
   iscc /DAppVersion=1.0.0 build/setup.iss
   ```
   The generated setup executable will be created in `build/Output/BexioOrderImportSetup.exe` (or in the working directory).

### Adding New Excel Field Mappings

When adding a new field from an Excel template, follow these steps in order to keep all layers in sync:

1. **Domain model**: Add the property to the relevant class in `src/BexioOrderImport.Domain/Models/`
2. **ExcelMappingOptions**: Add the corresponding cell/column config property in `src/BexioOrderImport.Application/Options/ExcelMappingOptions.cs`
3. **Parser**: Read the new cell in `src/BexioOrderImport.Infrastructure/Excel/ClosedXmlExcelParser.cs`
4. **AppSettingsDto**: Add the new property to the matching DTO class in `src/BexioOrderImport.Wpf/Models/AppSettingsDto.cs`
5. **MainViewModel**: Add the corresponding VM property and wire it in `CopyVmToProfile`/`CopyProfileToVm` in `MainViewModel.Settings.cs`
6. **XAML**: Add the UI binding in the Settings tab in `MainWindow.xaml` or `ProfileEditWindow.xaml`
7. **Translations**: Add the label key in `Translations.resx` and `Translations.en.resx`
8. **Tests**: Add a test case in `ExcelParserTests.cs` covering the new field

### Commit Message Convention
- Commit messages must follow the [Conventional Commits](https://www.conventionalcommits.org/) specification.
- This convention is automatically enforced locally using [Husky.NET](https://github.com/alirezanet/Husky.Net) as a `commit-msg` git hook.
- In pull requests, the PR Title is validated in CI using `amannn/action-semantic-pull-request` to ensure squash merges stay conventional.

### Localization
- User-facing strings in the WPF application should be maintained in [Translations.resx](src/BexioOrderImport.Wpf/Resources/Translations.resx) rather than hardcoded in XAML to ensure localization support.

Thank you for contributing!
