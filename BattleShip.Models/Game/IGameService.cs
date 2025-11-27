namespace BattleShip.Models.Game;

public interface IGameService
{
    Game StartNewGame(StartGameRequestDto request);
    AttackResponseDto Attack(AttackRequestDto request);
    GameStateDto GetState(Guid gameId);
    GameStateDto GetState(Guid gameId, PlayerSlot perspective);
    GameHistoryDto GetHistory(Guid gameId);
    GameStateDto UndoLastMove(Guid gameId);
    GameStateDto Restart(Guid gameId);
    LeaderboardDto GetLeaderboard();
    PlayerSlot RegisterPlayer(Guid gameId, string connectionId);
    void UnregisterPlayer(string connectionId);
    PlayerSlot GetPlayerSlot(Guid gameId, string connectionId);
    bool TryGetConnection(Guid gameId, PlayerSlot slot, out string? connectionId);
    IDictionary<PlayerSlot, AttackResponseDto> AttackMultiplayer(Guid gameId, PlayerSlot shooter, Coordinates target);
}

