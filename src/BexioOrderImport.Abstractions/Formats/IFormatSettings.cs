namespace BexioOrderImport.Abstractions.Formats;

/// <summary>
/// Profile settings of an import format. Serialized as is into appsettings.json and profile exports,
/// so older settings files must still deserialize.
/// </summary>
public interface IFormatSettings
{
}
