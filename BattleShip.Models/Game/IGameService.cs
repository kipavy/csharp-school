namespace BattleShip.Models.Game;

public interface IGameService
{
    Game StartNewGame(StartGameRequestDto request);
    AttackResponseDto Attack(AttackRequestDto request);
    GameStateDto GetState(Guid gameId);
    GameHistoryDto GetHistory(Guid gameId);
    GameStateDto UndoLastMove(Guid gameId);
    GameStateDto Restart(Guid gameId);
    LeaderboardDto GetLeaderboard();
}

