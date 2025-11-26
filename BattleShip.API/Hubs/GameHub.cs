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

    public async Task JoinGame(Guid gameId)
    {
        try
        {
            var slot = _gameService.RegisterPlayer(gameId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());
            var state = _gameService.GetState(gameId, slot);
            await Clients.Caller.SendAsync("OnGameStarted", state);

            var otherSlot = slot == PlayerSlot.PlayerOne ? PlayerSlot.PlayerTwo : PlayerSlot.PlayerOne;
            if (_gameService.TryGetConnection(gameId, otherSlot, out var otherConnection) && !string.IsNullOrWhiteSpace(otherConnection))
            {
                var otherState = _gameService.GetState(gameId, otherSlot);
                await Clients.Client(otherConnection).SendAsync("OnGameStarted", otherState);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task Shoot(Guid gameId, int x, int y)
    {
        try
        {
            var slot = _gameService.GetPlayerSlot(gameId, Context.ConnectionId);
            var responses = _gameService.AttackMultiplayer(gameId, slot, new Coordinates(x, y));
            foreach (var response in responses)
            {
                if (_gameService.TryGetConnection(gameId, response.Key, out var connectionId) && !string.IsNullOrWhiteSpace(connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("OnShotResolved", response.Value);
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            throw new HubException(ex.Message);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _gameService.UnregisterPlayer(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

