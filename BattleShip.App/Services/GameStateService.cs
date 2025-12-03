using System.Linq;
using BattleShip.Models.Game;

namespace BattleShip.App.Services;

public class GameStateService
{
    public Guid? GameId { get; private set; }
    public GameStatus Status { get; private set; } = GameStatus.InProgress;
    public int GridSize { get; private set; } = 10;
    public string[][] PlayerGrid { get; private set; } = Array.Empty<string[]>();
    public CellShotState[][] OpponentGrid { get; private set; } = Array.Empty<CellShotState[]>();
    public IList<MoveHistoryEntry> Moves { get; private set; } = new List<MoveHistoryEntry>();
    public Coordinates? LastAiShot { get; private set; }
    public ShotResult? LastAiShotResult { get; private set; }
    public bool IsMultiplayer { get; private set; }
    public bool IsPlayerTurn { get; private set; } = true;
    public PlayerSlot PlayerRole { get; private set; } = PlayerSlot.PlayerOne;
    public string? LastMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsWaitingForOpponent =>
        IsMultiplayer &&
        Status == GameStatus.InProgress &&
        PlayerRole == PlayerSlot.PlayerOne &&
        !HasOpponentConnected();
    public event Action? StateChanged;

    public void InitializeFromStart(StartGameResponseDto response, bool isMultiplayer)
    {
        GameId = response.GameId;
        ApplyState(response.InitialState);
        Moves = new List<MoveHistoryEntry>();
        IsMultiplayer = response.InitialState.IsMultiplayer || isMultiplayer;
        IsPlayerTurn = response.InitialState.IsPlayerTurn;
        PlayerRole = response.InitialState.Perspective;
        LastMessage = null;
        ErrorMessage = null;
        UpdateMessageFromState();
        NotifyStateChanged();
    }

    public void UpdateFromAttack(AttackResponseDto response)
    {
        if (response.GameState is GameStateDto updatedState)
        {
            ApplyState(updatedState);
        }

        if (!response.GameState.IsMultiplayer && response.AiShot is Coordinates aiShotUpdate)
        {
            PlayerGrid = ApplyAiShot(PlayerGrid, aiShotUpdate, response.AiShotResult);
        }

        var isMultiplayer = response.GameState?.IsMultiplayer == true;
        LastAiShot = isMultiplayer ? null : response.AiShot;
        LastAiShotResult = isMultiplayer ? null : response.AiShotResult;
        AppendMoves(response);
        UpdateMessageFromState(response.Status);
        NotifyStateChanged();
    }

    public void UpdateFromState(GameStateDto state)
    {
        ApplyState(state);
        UpdateMessageFromState();
        NotifyStateChanged();
    }

    public void SetHistory(GameHistoryDto history)
    {
        Moves = history.Moves?.ToList() ?? new List<MoveHistoryEntry>();
        NotifyStateChanged();
    }

    public void SetErrorMessage(string? message)
    {
        ErrorMessage = message;
        NotifyStateChanged();
    }

    private void ApplyState(GameStateDto state)
    {
        GameId = state.GameId;
        Status = state.Status;
        GridSize = state.GridSize;
        PlayerGrid = ClonePlayerGrid(state.PlayerGrid.Cells);
        OpponentGrid = CloneOpponentGrid(state.OpponentGrid.Cells);
        IsMultiplayer = state.IsMultiplayer;
        PlayerRole = state.Perspective;
        IsPlayerTurn = state.IsPlayerTurn;
        LogDebug($"ApplyState: status={Status}, isMultiplayer={IsMultiplayer}, perspective={PlayerRole}, isPlayerTurn={IsPlayerTurn}");
    }

    private static string[][] ClonePlayerGrid(string[][]? source)
    {
        if (source is null || source.Length == 0)
        {
            return Array.Empty<string[]>();
        }

        return source.Select(row => row.ToArray()).ToArray();
    }

    private static string[][] ApplyAiShot(string[][] current, Coordinates shot, ShotResult? result)
    {
        var clone = ClonePlayerGrid(current);
        if (clone.Length == 0 || shot.Y < 0 || shot.Y >= clone.Length)
        {
            return clone;
        }

        var row = clone[shot.Y];
        if (row.Length == 0 || shot.X < 0 || shot.X >= row.Length)
        {
            return clone;
        }

        row[shot.X] = result == ShotResult.Hit ? "X" : "O";
        return clone;
    }

    private static CellShotState[][] CloneOpponentGrid(CellShotState[][]? source)
    {
        if (source is null || source.Length == 0)
        {
            return Array.Empty<CellShotState[]>();
        }

        return source.Select(row => row.ToArray()).ToArray();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private void AppendMoves(AttackResponseDto response)
    {
        var updatedMoves = Moves.ToList();
        var nextNumber = updatedMoves.Count == 0 ? 1 : updatedMoves[^1].MoveNumber + 1;
        var isMultiplayer = response.GameState?.IsMultiplayer == true;
        if (isMultiplayer)
        {
            updatedMoves.Add(new MoveHistoryEntry
            {
                MoveNumber = nextNumber,
                Player = response.ActingPlayer == PlayerSlot.PlayerTwo ? PlayerType.AI : PlayerType.Human,
                Slot = response.ActingPlayer,
                Target = response.PlayerShot,
                Result = response.PlayerShotResult,
                PlayedAt = DateTime.UtcNow
            });
            Moves = updatedMoves;
            LogDebug($"AppendMoves multiplayer: moves={Moves.Count}, acting={response.ActingPlayer}, result={response.PlayerShotResult}");
            return;
        }

        updatedMoves.Add(new MoveHistoryEntry
        {
            MoveNumber = nextNumber,
            Player = PlayerType.Human,
            Slot = PlayerSlot.PlayerOne,
            Target = response.PlayerShot,
            Result = response.PlayerShotResult,
            PlayedAt = DateTime.UtcNow
        });

        if (response.AiShot is Coordinates aiShot && response.AiShotResult is ShotResult aiResult)
        {
            updatedMoves.Add(new MoveHistoryEntry
            {
                MoveNumber = nextNumber + 1,
                Player = PlayerType.AI,
                Slot = PlayerSlot.PlayerTwo,
                Target = aiShot,
                Result = aiResult,
                PlayedAt = DateTime.UtcNow
            });
        }

        Moves = updatedMoves;
        LogDebug($"AppendMoves solo: moves={Moves.Count}, lastResult={response.PlayerShotResult}");
    }

    private void UpdateMessageFromState(GameStatus? overrideStatus = null)
    {
        var status = overrideStatus ?? Status;
        if (!IsMultiplayer)
        {
            if (status != GameStatus.InProgress)
            {
                LastMessage = BuildOutcomeMessage(status);
                LogDebug($"UpdateMessageFromState solo finished: status={status}, message={LastMessage}");
                return;
            }

            LastMessage = IsPlayerTurn ? "Your turn" : "Opponent turn";
            LogDebug($"UpdateMessageFromState solo in-progress: isPlayerTurn={IsPlayerTurn}, message={LastMessage}");
            return;
        }

        if (status != GameStatus.InProgress)
        {
            LastMessage = status switch
            {
                GameStatus.PlayerWon => "Player One",
                GameStatus.AIWon => "Player Two",
                _ => string.Empty
            };
            LogDebug($"UpdateMessageFromState multi finished: status={status}, message={LastMessage}");
            return;
        }

        LastMessage = IsWaitingForOpponent ? "Waiting for opponent..." : string.Empty;
        LogDebug($"UpdateMessageFromState multi in-progress: waiting={IsWaitingForOpponent}, message={LastMessage}");
    }

    private string BuildOutcomeMessage(GameStatus status)
    {
        if (!IsMultiplayer)
        {
            return status switch
            {
                GameStatus.PlayerWon => "You won!",
                GameStatus.AIWon => "AI won.",
                _ => status.ToString()
            };
        }

        var localWins = status switch
        {
            GameStatus.PlayerWon => PlayerRole == PlayerSlot.PlayerOne,
            GameStatus.AIWon => PlayerRole == PlayerSlot.PlayerTwo,
            _ => false
        };

        return status switch
        {
            GameStatus.PlayerWon or GameStatus.AIWon => localWins ? "You won!" : "Opponent won.",
            _ => status.ToString()
        };
    }

    private bool HasOpponentConnected()
    {
        if (!IsMultiplayer)
        {
            return true;
        }

        if (PlayerRole == PlayerSlot.PlayerTwo)
        {
            LogDebug("HasOpponentConnected: role=PlayerTwo, returning true");
            return true;
        }

        if (Moves.Count > 0)
        {
            LogDebug($"HasOpponentConnected: moves>0 ({Moves.Count}), returning true");
            return true;
        }

        LogDebug($"HasOpponentConnected: isPlayerTurn={IsPlayerTurn}");
        return IsPlayerTurn;
    }

    private void LogDebug(string message)
    {
        Console.WriteLine($"{DateTime.UtcNow:O} [GameStateService] {message}");
    }
}

