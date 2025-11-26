using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BattleShip.Models.Game;

namespace BattleShip.API.Game;

public class GameService : IGameService
{
    private readonly ConcurrentDictionary<Guid, Models.Game.Game> _games = new();
    private readonly Dictionary<string, PlayerStats> _leaderboard = new();
    private readonly object _leaderboardLock = new();
    private const string DefaultPlayerId = "local-player";

    public Models.Game.Game StartNewGame(StartGameRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var gridSize = request.GridSize ?? 10;
        if (gridSize <= 0) throw new ArgumentException("Invalid grid size.");
        var definitions = ShipDefinitions.Default;
        var playerGrid = CreateGrid(gridSize);
        IReadOnlyList<ShipPlacement> playerPlacements;
        if (request.RandomizePlayerShips)
        {
            playerPlacements = PlaceShipsRandomly(playerGrid, definitions);
        }
        else
        {
            playerPlacements = ValidatePlayerPlacements(request.PlayerShips, definitions, gridSize);
            PlaceShips(playerGrid, playerPlacements, definitions);
        }
        var aiGrid = CreateGrid(gridSize);
        PlaceShipsRandomly(aiGrid, definitions);
        var opponentView = new bool?[gridSize, gridSize];
        var queue = BuildAiQueue(gridSize);
        var configuration = new GameConfiguration(gridSize, request.RandomizePlayerShips, request.RandomizePlayerShips ? null : playerPlacements.ToList());
        var game = new Models.Game.Game
        {
            GridSize = gridSize,
            PlayerGrid = playerGrid,
            AiGrid = aiGrid,
            OpponentView = opponentView,
            AiMovesQueue = queue,
            Configuration = configuration
        };
        _games[game.Id] = game;
        return game;
    }

    public AttackResponseDto Attack(AttackRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var game = GetGame(request.GameId);
        if (game.Status != GameStatus.InProgress) throw new InvalidOperationException("Game has already finished.");
        EnsureCoordinates(game, request.X, request.Y);
        var playerShot = new Coordinates(request.X, request.Y);
        var playerResult = ApplyShot(game, PlayerType.Human, playerShot);
        if (!HasRemainingShips(game.AiGrid)) FinalizeGame(game, GameStatus.PlayerWon);
        Coordinates? aiShot = null;
        ShotResult? aiResult = null;
        if (game.Status == GameStatus.InProgress)
        {
            var aiShotValue = DequeueAiMove(game);
            aiShot = aiShotValue;
            aiResult = ApplyShot(game, PlayerType.AI, aiShotValue);
            if (!HasRemainingShips(game.PlayerGrid)) FinalizeGame(game, GameStatus.AIWon);
        }
        var state = BuildState(game);
        return new AttackResponseDto(game.Id, game.Status, playerShot, playerResult, aiShot, aiResult, state);
    }

    public GameStateDto GetState(Guid gameId)
    {
        var game = GetGame(gameId);
        return BuildState(game);
    }

    public GameHistoryDto GetHistory(Guid gameId)
    {
        var game = GetGame(gameId);
        var moves = game.History.Select(entry => new MoveHistoryEntry
        {
            MoveNumber = entry.MoveNumber,
            Player = entry.Player,
            Target = entry.Target,
            Result = entry.Result,
            PlayedAt = entry.PlayedAt
        }).ToList();
        return new GameHistoryDto(game.Id, moves);
    }

    public GameStateDto UndoLastMove(Guid gameId)
    {
        var game = GetGame(gameId);
        if (game.History.Count == 0 || game.Snapshots.Count == 0) throw new InvalidOperationException("No moves can be undone.");
        if (game.LeaderboardRecorded)
        {
            RevertLeaderboard(game.Status);
            game.LeaderboardRecorded = false;
        }
        var removedType = game.History[^1].Player;
        RestoreLastMove(game);
        if (removedType == PlayerType.AI && game.History.Count > 0 && game.Snapshots.Count > 0) RestoreLastMove(game);
        game.Status = GameStatus.InProgress;
        return BuildState(game);
    }

    public GameStateDto Restart(Guid gameId)
    {
        var game = GetGame(gameId);
        var config = game.Configuration;
        _games.TryRemove(gameId, out _);
        var request = new StartGameRequestDto(config.GridSize, config.RandomizePlayerShips, config.RandomizePlayerShips ? null : config.PlayerShips?.Select(p => new ShipPlacement(p.ShipCode, new Coordinates(p.Start.X, p.Start.Y), p.Horizontal)).ToList());
        var newGame = StartNewGame(request);
        return BuildState(newGame);
    }

    public LeaderboardDto GetLeaderboard()
    {
        List<LeaderboardEntryDto> entries;
        lock (_leaderboardLock)
        {
            entries = _leaderboard.Select(kvp =>
            {
                var stats = kvp.Value;
                var winRate = stats.GamesPlayed == 0 ? 0d : (double)stats.GamesWon / stats.GamesPlayed;
                return new LeaderboardEntryDto(kvp.Key, stats.GamesPlayed, stats.GamesWon, stats.GamesLost, winRate);
            }).ToList();
        }
        return new LeaderboardDto(entries);
    }

    private static char[,] CreateGrid(int size) => new char[size, size];

    private static Queue<Coordinates> BuildAiQueue(int gridSize)
    {
        var coords = new List<Coordinates>(gridSize * gridSize);
        for (var y = 0; y < gridSize; y++)
        {
            for (var x = 0; x < gridSize; x++) coords.Add(new Coordinates(x, y));
        }
        Shuffle(coords);
        return new Queue<Coordinates>(coords);
    }

    private static void Shuffle(IList<Coordinates> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static IReadOnlyList<ShipPlacement> PlaceShipsRandomly(char[,] grid, IReadOnlyList<ShipDefinition> definitions)
    {
        var placements = new List<ShipPlacement>();
        foreach (var definition in definitions)
        {
            var placement = FindRandomPlacement(grid, definition);
            placements.Add(placement);
            ApplyPlacement(grid, placement, definition.Size);
        }
        return placements;
    }

    private static ShipPlacement FindRandomPlacement(char[,] grid, ShipDefinition definition)
    {
        var size = grid.GetLength(0);
        var attempts = 0;
        while (attempts < 1000)
        {
            var horizontal = Random.Shared.Next(2) == 0;
            var maxX = horizontal ? size - definition.Size : size - 1;
            var maxY = horizontal ? size - 1 : size - definition.Size;
            var startX = Random.Shared.Next(0, maxX + 1);
            var startY = Random.Shared.Next(0, maxY + 1);
            var placement = new ShipPlacement(definition.Code, new Coordinates(startX, startY), horizontal);
            if (CanPlace(grid, placement, definition.Size)) return placement;
            attempts++;
        }
        throw new InvalidOperationException("Unable to place ship after several attempts.");
    }

    private static IReadOnlyList<ShipPlacement> ValidatePlayerPlacements(IList<ShipPlacement>? provided, IReadOnlyList<ShipDefinition> definitions, int gridSize)
    {
        if (provided is null) throw new ArgumentException("Ship placements are required.");
        if (provided.Count != definitions.Count) throw new ArgumentException("Invalid number of placements.");
        var placements = new List<ShipPlacement>();
        var definitionMap = definitions.ToDictionary(d => d.Code);
        var seen = new HashSet<char>();
        foreach (var definition in definitions)
        {
            var placement = provided.FirstOrDefault(p => char.ToUpperInvariant(p.ShipCode) == char.ToUpperInvariant(definition.Code));
            if (placement == null) throw new ArgumentException($"Missing placement for ship {definition.Code}.");
            var code = char.ToUpperInvariant(definition.Code);
            if (!seen.Add(code)) throw new ArgumentException($"Duplicate placement detected for {code}.");
            placement = new ShipPlacement(code, new Coordinates(placement.Start.X, placement.Start.Y), placement.Horizontal);
            if (!IsWithinBounds(placement, definition.Size, gridSize)) throw new ArgumentException($"Placement out of bounds for {definition.Code}.");
            placements.Add(placement);
        }
        foreach (var placement in provided)
        {
            var code = char.ToUpperInvariant(placement.ShipCode);
            if (!definitionMap.ContainsKey(code)) throw new ArgumentException($"Unknown ship code {placement.ShipCode}.");
        }
        var tempGrid = CreateGrid(gridSize);
        foreach (var placement in placements)
        {
            var definition = definitionMap[placement.ShipCode];
            if (!CanPlace(tempGrid, placement, definition.Size)) throw new ArgumentException($"Overlapping placement detected for {placement.ShipCode}.");
            ApplyPlacement(tempGrid, placement, definition.Size);
        }
        return placements;
    }

    private static bool IsWithinBounds(ShipPlacement placement, int shipSize, int gridSize)
    {
        if (placement.Start.X < 0 || placement.Start.Y < 0) return false;
        if (placement.Horizontal) return placement.Start.X + shipSize <= gridSize && placement.Start.Y < gridSize;
        return placement.Start.Y + shipSize <= gridSize && placement.Start.X < gridSize;
    }

    private static bool CanPlace(char[,] grid, ShipPlacement placement, int shipSize)
    {
        var size = grid.GetLength(0);
        if (!IsWithinBounds(placement, shipSize, size)) return false;
        for (var i = 0; i < shipSize; i++)
        {
            var x = placement.Horizontal ? placement.Start.X + i : placement.Start.X;
            var y = placement.Horizontal ? placement.Start.Y : placement.Start.Y + i;
            if (grid[y, x] != '\0') return false;
        }
        return true;
    }

    private static void PlaceShips(char[,] grid, IReadOnlyList<ShipPlacement> placements, IReadOnlyList<ShipDefinition> definitions)
    {
        var definitionMap = definitions.ToDictionary(d => d.Code);
        foreach (var placement in placements)
        {
            var definition = definitionMap[placement.ShipCode];
            ApplyPlacement(grid, placement, definition.Size);
        }
    }

    private static void ApplyPlacement(char[,] grid, ShipPlacement placement, int shipSize)
    {
        for (var i = 0; i < shipSize; i++)
        {
            var x = placement.Horizontal ? placement.Start.X + i : placement.Start.X;
            var y = placement.Horizontal ? placement.Start.Y : placement.Start.Y + i;
            grid[y, x] = placement.ShipCode;
        }
    }

    private static void EnsureCoordinates(Models.Game.Game game, int x, int y)
    {
        if (x < 0 || y < 0 || x >= game.GridSize || y >= game.GridSize) throw new ArgumentException("Coordinates are outside of the grid.");
    }

    private static ShotResult ApplyShot(Models.Game.Game game, PlayerType shooter, Coordinates target)
    {
        var grid = shooter == PlayerType.Human ? game.AiGrid : game.PlayerGrid;
        var current = grid[target.Y, target.X];
        if (current == 'X' || current == 'O') throw new InvalidOperationException("Cell was already targeted.");
        var result = current == '\0' ? ShotResult.Miss : ShotResult.Hit;
        grid[target.Y, target.X] = result == ShotResult.Hit ? 'X' : 'O';
        if (shooter == PlayerType.Human) game.OpponentView[target.Y, target.X] = result == ShotResult.Hit;
        var snapshot = new GridSnapshot(shooter, target, current, shooter == PlayerType.Human);
        game.Snapshots.Push(snapshot);
        game.MoveCounter++;
        game.History.Add(new MoveHistoryEntry
        {
            MoveNumber = game.MoveCounter,
            Player = shooter,
            Target = target,
            Result = result,
            PlayedAt = DateTime.UtcNow
        });
        return result;
    }

    private static Coordinates DequeueAiMove(Models.Game.Game game)
    {
        if (game.AiMovesQueue.Count == 0) throw new InvalidOperationException("AI cannot play any remaining moves.");
        return game.AiMovesQueue.Dequeue();
    }

    private static bool HasRemainingShips(char[,] grid)
    {
        var size = grid.GetLength(0);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var cell = grid[y, x];
                if (cell >= 'A' && cell <= 'F') return true;
            }
        }
        return false;
    }

    private void FinalizeGame(Models.Game.Game game, GameStatus status)
    {
        if (game.Status == status) return;
        game.Status = status;
        if (!game.LeaderboardRecorded)
        {
            lock (_leaderboardLock)
            {
                var stats = GetPlayerStats(DefaultPlayerId);
                stats.GamesPlayed++;
                if (status == GameStatus.PlayerWon) stats.GamesWon++;
                if (status == GameStatus.AIWon) stats.GamesLost++;
            }
            game.LeaderboardRecorded = true;
        }
    }

    private void RevertLeaderboard(GameStatus status)
    {
        lock (_leaderboardLock)
        {
            if (!_leaderboard.TryGetValue(DefaultPlayerId, out var stats)) return;
            if (stats.GamesPlayed > 0) stats.GamesPlayed--;
            if (status == GameStatus.PlayerWon && stats.GamesWon > 0) stats.GamesWon--;
            if (status == GameStatus.AIWon && stats.GamesLost > 0) stats.GamesLost--;
        }
    }

    private void RestoreLastMove(Models.Game.Game game)
    {
        if (game.History.Count == 0 || game.Snapshots.Count == 0) throw new InvalidOperationException("History does not allow undo.");
        var entry = game.History[^1];
        var snapshot = game.Snapshots.Pop();
        if (entry.Player != snapshot.Player) throw new InvalidOperationException("Snapshot stack is inconsistent.");
        var grid = snapshot.TargetsAiGrid ? game.AiGrid : game.PlayerGrid;
        grid[snapshot.Target.Y, snapshot.Target.X] = snapshot.PreviousValue;
        if (snapshot.TargetsAiGrid)
        {
            game.OpponentView[snapshot.Target.Y, snapshot.Target.X] = null;
        }
        else
        {
            ReinsertAiMove(game, snapshot.Target);
        }
        game.History.RemoveAt(game.History.Count - 1);
        if (game.MoveCounter > 0) game.MoveCounter--;
    }

    private static void ReinsertAiMove(Models.Game.Game game, Coordinates coordinates)
    {
        var restored = new Queue<Coordinates>();
        restored.Enqueue(coordinates);
        foreach (var item in game.AiMovesQueue) restored.Enqueue(item);
        game.AiMovesQueue = restored;
    }

    private GameStateDto BuildState(Models.Game.Game game)
    {
        var playerGrid = BuildPlayerGridDto(game.PlayerGrid, game.GridSize);
        var opponentGrid = BuildOpponentGridDto(game.OpponentView, game.GridSize);
        return new GameStateDto(game.Id, game.Status, game.GridSize, playerGrid, opponentGrid);
    }

    private static PlayerGridDto BuildPlayerGridDto(char[,] grid, int size)
    {
        var cells = new string[size][];
        for (var y = 0; y < size; y++)
        {
            var row = new string[size];
            for (var x = 0; x < size; x++)
            {
                var value = grid[y, x];
                row[x] = value == '\0' ? string.Empty : value.ToString();
            }
            cells[y] = row;
        }
        return new PlayerGridDto(cells);
    }

    private static OpponentGridDto BuildOpponentGridDto(bool?[,] view, int size)
    {
        var cells = new CellShotState[size][];
        for (var y = 0; y < size; y++)
        {
            var row = new CellShotState[size];
            for (var x = 0; x < size; x++)
            {
                row[x] = view[y, x] switch
                {
                    true => CellShotState.Hit,
                    false => CellShotState.Miss,
                    _ => CellShotState.Unknown
                };
            }
            cells[y] = row;
        }
        return new OpponentGridDto(cells);
    }

    private Models.Game.Game GetGame(Guid gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) throw new KeyNotFoundException("Game not found.");
        return game;
    }

    private PlayerStats GetPlayerStats(string playerId)
    {
        if (!_leaderboard.TryGetValue(playerId, out var stats))
        {
            stats = new PlayerStats();
            _leaderboard[playerId] = stats;
        }
        return stats;
    }

    private sealed class PlayerStats
    {
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
    }
}

