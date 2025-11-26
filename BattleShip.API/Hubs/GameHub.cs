using System.Threading.Tasks;
using BattleShip.Models.Game;
using Microsoft.AspNetCore.SignalR;

namespace BattleShip.API.Hubs;

public class GameHub : Hub
{
    private readonly IGameService _gameService;

    public GameHub(IGameService gameService)
    {
        _gameService = gameService;
    }

    public Task JoinGame(Guid gameId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());
    }

    public async Task Shoot(Guid gameId, int x, int y)
    {
        try
        {
            var result = _gameService.Attack(new AttackRequestDto(gameId, x, y));
            await Clients.Group(gameId.ToString()).SendAsync("OnShotResolved", result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            throw new HubException(ex.Message);
        }
    }
}

