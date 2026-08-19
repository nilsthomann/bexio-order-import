namespace BexioOrderImport.Application.Models;

public record ImportResult(
    bool Success,
    string? OrderNumber = null,
    int UploadedPositionsCount = 0,
    string? ErrorMessage = null
);
