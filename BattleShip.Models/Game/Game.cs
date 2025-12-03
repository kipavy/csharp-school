using System.Collections.Generic;

namespace BattleShip.Models.Game;

public class Game
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int GridSize { get; init; } = 10;
    public char[,] PlayerGrid { get; init; } = default!;
    public char[,] AiGrid { get; init; } = default!;
    public bool?[,] OpponentView { get; init; } = default!;
    public bool?[,] PlayerTwoView { get; init; } = default!;
    public GameStatus Status { get; set; } = GameStatus.InProgress;
    public List<MoveHistoryEntry> History { get; } = new();
    public Queue<Coordinates> AiMovesQueue { get; set; } = new();
    public Stack<GridSnapshot> Snapshots { get; } = new();
    public GameConfiguration Configuration { get; init; } = default!;
    public int MoveCounter { get; set; }
    public bool LeaderboardRecorded { get; set; }
    public bool IsMultiplayer { get; set; }
    public PlayerSlot CurrentTurn { get; set; } = PlayerSlot.PlayerOne;
    public Dictionary<PlayerSlot, string> PlayerConnections { get; } = new();
    public AiDifficulty Difficulty { get; init; } = AiDifficulty.Easy;
    public Queue<Coordinates> AiPriorityTargets { get; } = new();
    public Coordinates? HuntOrigin { get; set; }
    public Coordinates? HuntDirection { get; set; }
    public bool HuntReverseQueued { get; set; }
}

public class MoveHistoryEntry
{
    public int MoveNumber { get; init; }
    public PlayerType Player { get; init; }
    public PlayerSlot Slot { get; init; } = PlayerSlot.PlayerOne;
    public Coordinates Target { get; init; } = default!;
    public ShotResult Result { get; init; }
    public DateTime PlayedAt { get; init; }
}

public record GridSnapshot(PlayerType Player, Coordinates Target, char PreviousValue, bool TargetsAiGrid);

public record GameConfiguration(int GridSize, bool RandomizePlayerShips, IReadOnlyList<ShipPlacement>? PlayerShips, bool IsMultiplayer, AiDifficulty Difficulty);

