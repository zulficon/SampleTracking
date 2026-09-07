namespace SampleAnalysisTracking.DTOs;

// Liste ekranlarının ihtiyacı olan küçük katalog verileri.
public sealed record LocationItem(long Id, string Name, string? Description);
public sealed record UserItem(long Id, string Username, string Role, DateTimeOffset CreatedAt);
