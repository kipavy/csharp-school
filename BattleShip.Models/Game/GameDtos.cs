using System.Collections.Generic;

namespace BattleShip.Models.Game;

public record PlayerGridDto(string[][] Cells);

public record OpponentGridDto(CellShotState[][] Cells);

public record GameStateDto(Guid GameId, GameStatus Status, int GridSize, PlayerGridDto PlayerGrid, OpponentGridDto OpponentGrid, bool IsMultiplayer, PlayerSlot Perspective, bool IsPlayerTurn);

public record StartGameRequestDto(int? GridSize, bool RandomizePlayerShips, IList<ShipPlacement>? PlayerShips, bool IsMultiplayer);

public record StartGameResponseDto(Guid GameId, GameStateDto InitialState);

public record AttackRequestDto(Guid GameId, int X, int Y);

public record AttackResponseDto(Guid GameId, GameStatus Status, Coordinates PlayerShot, ShotResult PlayerShotResult, Coordinates? AiShot, ShotResult? AiShotResult, GameStateDto GameState, PlayerSlot ActingPlayer);

public record GameHistoryDto(Guid GameId, IList<MoveHistoryEntry> Moves);

public record LeaderboardEntryDto(string PlayerId, int GamesPlayed, int GamesWon, int GamesLost, double WinRate);

public record LeaderboardDto(IList<LeaderboardEntryDto> Entries);

