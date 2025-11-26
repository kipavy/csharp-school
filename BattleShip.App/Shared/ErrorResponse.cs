namespace BattleShip.App.Shared;

public record ErrorDetail(string Code, string Message, string? Field);

public record ErrorResponse(string Message, IReadOnlyList<ErrorDetail> Errors);

