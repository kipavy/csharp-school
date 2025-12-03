using System;
using BattleShip.Models.Game;
using Microsoft.AspNetCore.SignalR.Client;

namespace BattleShip.App.Services;

public class GameHubClient : IAsyncDisposable
{
    private readonly GameStateService _gameState;
    private readonly Uri _hubUri;
    private HubConnection? _connection;

    public GameHubClient(GameStateService gameState, ApiConfiguration apiConfiguration)
    {
        _gameState = gameState;
        _hubUri = new Uri(apiConfiguration.HubBaseAddress, "/hubs/game");
    }

    public async Task EnsureConnectedAsync()
    {
        if (_connection is not null)
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
            }

            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUri)
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers(_connection);
        await _connection.StartAsync();
    }

    public async Task JoinGameAsync(Guid gameId)
    {
        await EnsureConnectedAsync();
        await _connection!.InvokeAsync("JoinGame", gameId);
    }

    public async Task ShootAsync(Guid gameId, int x, int y)
    {
        await EnsureConnectedAsync();
        await _connection!.InvokeAsync("Shoot", gameId, x, y);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<GameStateDto>("OnGameStarted", state =>
        {
            _gameState.UpdateFromState(state);
        });

        connection.On<AttackResponseDto>("OnShotResolved", response =>
        {
            _gameState.UpdateFromAttack(response);
        });

        connection.On<GameStateDto>("OnGameEnded", state =>
        {
            _gameState.UpdateFromState(state);
        });
    }
}

