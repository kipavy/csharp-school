namespace BattleShip.Models.Game;

public record Coordinates(int X, int Y);

public record ShipDefinition(char Code, int Size);

public record ShipPlacement(char ShipCode, Coordinates Start, bool Horizontal);

