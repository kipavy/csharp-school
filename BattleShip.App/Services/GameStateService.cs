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
        LastMessage = BuildMessage(response);
        if (response.Status != GameStatus.InProgress)
        {
            LastMessage = response.Status.ToString();
        }
        NotifyStateChanged();
    }

    public void UpdateFromState(GameStateDto state)
    {
        ApplyState(state);
        NotifyStateChanged();
    }

    public void SetHistory(GameHistoryDto history)
    {
        Moves = history.Moves?.ToList() ?? new List<MoveHistoryEntry>();
        NotifyStateChanged();
    }

    public void SetStatusMessage(string? message)
    {
        LastMessage = message;
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
    }

    private string BuildMessage(AttackResponseDto response)
    {
        var isMultiplayer = response.GameState?.IsMultiplayer == true;
        if (!isMultiplayer)
        {
            var playerMessage = $"Player shot: ({response.PlayerShot.X},{response.PlayerShot.Y}) {response.PlayerShotResult}";
            var aiMessage = response.AiShot is Coordinates aiShot
                ? $"AI shot: ({aiShot.X},{aiShot.Y}) {response.AiShotResult?.ToString() ?? "Miss"}"
                : "AI did not play.";
            return $"{playerMessage} | {aiMessage}";
        }

        var actorIsLocal = response.ActingPlayer == PlayerRole;
        var actorLabel = actorIsLocal ? "You" : "Opponent";
        return $"{actorLabel} shot: ({response.PlayerShot.X},{response.PlayerShot.Y}) {response.PlayerShotResult}";
    }
}

