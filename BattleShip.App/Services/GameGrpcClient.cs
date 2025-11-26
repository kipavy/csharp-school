using BattleShip.Grpc;
using BattleShip.Models.Game;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace BattleShip.App.Services;

public class GameGrpcClient
{
    private readonly GameApiClient _apiClient;
    private readonly BattleShipService.BattleShipServiceClient _client;

    public GameGrpcClient(ApiConfiguration apiConfiguration, GameApiClient apiClient)
    {
        _apiClient = apiClient;
        var handler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
        var channel = GrpcChannel.ForAddress(apiConfiguration.GrpcBaseAddress, new GrpcChannelOptions
        {
            HttpHandler = handler
        });
        _client = new BattleShipService.BattleShipServiceClient(channel);
    }

    public async Task<AttackResponseDto> AttackAsync(Guid gameId, int x, int y)
    {
        var request = new AttackRequest
        {
            GameId = gameId.ToString(),
            X = x,
            Y = y
        };

        var reply = await _client.AttackAsync(request);
        var state = await _apiClient.GetStateAsync(gameId);
        var aiShot = string.IsNullOrWhiteSpace(reply.AiResult) ? null : new Coordinates(reply.AiX, reply.AiY);
        ShotResult? aiResult = null;
        if (!string.IsNullOrWhiteSpace(reply.AiResult) && Enum.TryParse(reply.AiResult, true, out ShotResult parsedAiResult))
        {
            aiResult = parsedAiResult;
        }

        if (!Enum.TryParse(reply.Status, true, out GameStatus status))
        {
            status = GameStatus.InProgress;
        }

        if (!Enum.TryParse(reply.PlayerResult, true, out ShotResult playerResult))
        {
            playerResult = ShotResult.Miss;
        }

        return new AttackResponseDto(
            Guid.Parse(reply.GameId),
            status,
            new Coordinates(reply.PlayerX, reply.PlayerY),
            playerResult,
            aiShot,
            aiResult,
            state);
    }
}

