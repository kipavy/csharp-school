namespace BattleShip.Models.Game;

public enum CellShotState
{
    Unknown,
    Hit,
    Miss
}

public enum GameStatus
{
    InProgress,
    PlayerWon,
    AIWon
}

public enum ShotResult
{
    Hit,
    Miss
}

public enum PlayerType
{
    Human,
    AI
}

public enum PlayerSlot
{
    None,
    PlayerOne,
    PlayerTwo
}

public enum AiDifficulty
{
    Easy,
    Normal,
    Hard
}

