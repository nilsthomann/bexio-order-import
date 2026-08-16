using System;
using System.IO;
using System.Text.Json;
using BexioOrderImport.Application.Helpers;
using BexioOrderImport.Application.Options;
using FluentAssertions;
using Xunit;

namespace BexioOrderImport.Tests;

public class ProfileColumnMigrationTests : IDisposable
{
    private readonly string _tempFile;

    public ProfileColumnMigrationTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public void LoadLegacyJson_WithNumericColumns_ShouldMigrateToLetters()
    {
        string legacyJson = @"
{
  ""Bexio"": { ""ApiToken"": ""test"" },
  ""ActiveProfileName"": ""Default"",
  ""Profiles"": [
    {
      ""Name"": ""Default"",
      ""ExcelMapping"": {
        ""SizeMatrix"": {
          ""StartRow"": 10,
          ""EndRow"": 17,
          ""CategoryColumn"": 4,
          ""StartSizeColumn"": 5,
          ""EndSizeColumn"": 18
        },
        ""Data"": {
          ""StartRow"": 18,
          ""ArticleNumberColumn"": 1,
          ""ArticleNameColumn"": 2,
          ""ColorColumn"": 3,
          ""CategoryColumn"": 4,
          ""StartQtyColumn"": 5,
          ""EndQtyColumn"": 18,
          ""UnitPriceColumn"": 20,
          ""EnableRowDiscount"": false,
          ""RowDiscountColumn"": 21
        }
      }
    }
  ]
}";
        File.WriteAllText(_tempFile, legacyJson);

        ExcelColumnJsonConverter.ResetMigrationTracker();
        var deserialized = JsonSerializer.Deserialize<BexioOrderImport.Wpf.Models.AppSettingsDto>(File.ReadAllText(_tempFile));
        deserialized.Should().NotBeNull();
        var profile = deserialized!.Profiles[0];

        bool jsonConverterMigrated = ExcelColumnJsonConverter.HasPerformedNumericConversion;
        bool helperMigrated = ExcelColumnHelper.MigrateProfileColumnMappings(profile.ExcelMapping);
        (jsonConverterMigrated || helperMigrated).Should().BeTrue();

        profile.ExcelMapping.SizeMatrix.CategoryColumn.Should().Be("D");
        profile.ExcelMapping.SizeMatrix.StartSizeColumn.Should().Be("E");
        profile.ExcelMapping.SizeMatrix.EndSizeColumn.Should().Be("R");

        profile.ExcelMapping.Data.ArticleNumberColumn.Should().Be("A");
        profile.ExcelMapping.Data.ArticleNameColumn.Should().Be("B");
        profile.ExcelMapping.Data.ColorColumn.Should().Be("C");
        profile.ExcelMapping.Data.CategoryColumn.Should().Be("D");
        profile.ExcelMapping.Data.StartQtyColumn.Should().Be("E");
        profile.ExcelMapping.Data.EndQtyColumn.Should().Be("R");
        profile.ExcelMapping.Data.UnitPriceColumn.Should().Be("T");
        profile.ExcelMapping.Data.RowDiscountColumn.Should().Be("U");

        // Resave updated json
        string updatedJson = JsonSerializer.Serialize(deserialized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_tempFile, updatedJson);

        // Verify saved JSON contains letter strings
        string savedText = File.ReadAllText(_tempFile);
        savedText.Should().Contain("\"CategoryColumn\": \"D\"");
        savedText.Should().Contain("\"ArticleNumberColumn\": \"A\"");
        savedText.Should().Contain("\"UnitPriceColumn\": \"T\"");
    }
}
