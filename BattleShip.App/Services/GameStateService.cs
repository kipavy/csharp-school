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
    public string? LastMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public event Action? StateChanged;

    public void InitializeFromStart(StartGameResponseDto response, bool isMultiplayer)
    {
        GameId = response.GameId;
        ApplyState(response.InitialState);
        Moves = new List<MoveHistoryEntry>();
        IsMultiplayer = isMultiplayer;
        IsPlayerTurn = true;
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

        if (response.AiShot is Coordinates aiShotUpdate)
        {
            PlayerGrid = ApplyAiShot(PlayerGrid, aiShotUpdate, response.AiShotResult);
        }

        LastAiShot = response.AiShot;
        LastAiShotResult = response.AiShotResult;

        var playerMessage = $"Player shot: ({response.PlayerShot.X},{response.PlayerShot.Y}) {response.PlayerShotResult}";
        var aiMessage = response.AiShot is Coordinates aiShot
            ? $"AI shot: ({aiShot.X},{aiShot.Y}) {response.AiShotResult?.ToString() ?? "Miss"}"
            : "AI did not play.";
        LastMessage = $"{playerMessage} | {aiMessage}";
        if (response.Status != GameStatus.InProgress)
        {
            LastMessage = response.Status.ToString();
        }
        if (IsMultiplayer && response.Status == GameStatus.InProgress)
        {
            IsPlayerTurn = !IsPlayerTurn;
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

    public void SetMultiplayerMode(bool isMultiplayer)
    {
        IsMultiplayer = isMultiplayer;
        NotifyStateChanged();
    }

    public void SetMultiplayerTurn(bool isPlayerTurn)
    {
        IsPlayerTurn = isPlayerTurn;
        NotifyStateChanged();
    }

    private void ApplyState(GameStateDto state)
    {
        GameId = state.GameId;
        Status = state.Status;
        GridSize = state.GridSize;
        PlayerGrid = ClonePlayerGrid(state.PlayerGrid.Cells);
        OpponentGrid = CloneOpponentGrid(state.OpponentGrid.Cells);
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
}

