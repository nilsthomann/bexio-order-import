namespace BexioOrderImport.Application.Models;

public record ImportResult(
    bool Success,
    int? OrderId = null,
    int UploadedPositionsCount = 0,
    string? ErrorMessage = null
);
