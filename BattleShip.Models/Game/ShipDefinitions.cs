using System.Collections.Generic;

namespace BattleShip.Models.Game;

public static class ShipDefinitions
{
    public static IReadOnlyList<ShipDefinition> Default { get; } =
    [
        new ShipDefinition('A', 1),
        new ShipDefinition('B', 2),
        new ShipDefinition('C', 2),
        new ShipDefinition('D', 3),
        new ShipDefinition('E', 3),
        new ShipDefinition('F', 4)
    ];
}

