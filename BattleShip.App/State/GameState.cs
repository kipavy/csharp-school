namespace BattleShip.App.State;

public class GameState
{
    // Player grid: contains the player ship layout and/or shot results
    public char[,] PlayerGrid { get; set; }

    // Opponent grid: bool? to represent unknown / hit / miss
    // null  => unknown (not yet fired)
    // true  => hit
    // false => miss
    public bool?[,] OpponentGrid { get; set; }

    public int GridSize { get; }

    public GameState(int gridSize = 10)
    {
        GridSize = gridSize;
        PlayerGrid = new char[gridSize, gridSize];
        OpponentGrid = new bool?[gridSize, gridSize];
    }
}
