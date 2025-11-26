using System.Net.Http.Json;
using System.Text.Json;
using BattleShip.App.Shared;
using BattleShip.Models.Game;

namespace BattleShip.App.Services;

public class GameApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GameApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StartGameResponseDto> StartGameAsync(StartGameRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/games", request, SerializerOptions);
        return await ReadContentAsync<StartGameResponseDto>(response);
    }

    public async Task<GameStateDto> GetStateAsync(Guid gameId)
    {
        var response = await _httpClient.GetAsync($"api/games/{gameId}");
        return await ReadContentAsync<GameStateDto>(response);
    }

    public async Task<AttackResponseDto> AttackAsync(AttackRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/games/{request.GameId}/attack", request, SerializerOptions);
        return await ReadContentAsync<AttackResponseDto>(response);
    }

    public async Task<GameHistoryDto> GetHistoryAsync(Guid gameId)
    {
        var response = await _httpClient.GetAsync($"api/games/{gameId}/history");
        return await ReadContentAsync<GameHistoryDto>(response);
    }

    public async Task<GameStateDto> UndoAsync(Guid gameId)
    {
        var response = await _httpClient.PostAsync($"api/games/{gameId}/undo", null);
        return await ReadContentAsync<GameStateDto>(response);
    }

    public async Task<StartGameResponseDto> RestartAsync(Guid gameId)
    {
        var response = await _httpClient.PostAsync($"api/games/{gameId}/restart", null);
        return await ReadContentAsync<StartGameResponseDto>(response);
    }

    public async Task<LeaderboardDto> GetLeaderboardAsync()
    {
        var response = await _httpClient.GetAsync("api/leaderboard");
        return await ReadContentAsync<LeaderboardDto>(response);
    }

    private static async Task<T> ReadContentAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(SerializerOptions);
            if (data is null)
            {
                throw new InvalidOperationException("Empty response payload.");
            }

            return data;
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(SerializerOptions);
        if (error is not null)
        {
            throw new InvalidOperationException(error.Message);
        }

        response.EnsureSuccessStatusCode();
        throw new InvalidOperationException("Request failed.");
    }
}

