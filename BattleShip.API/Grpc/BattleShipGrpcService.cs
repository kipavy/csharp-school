using System.Threading.Tasks;
using BattleShip.Grpc;
using BattleShip.Models.Game;
using Grpc.Core;

namespace BattleShip.API.Grpc;

public class BattleShipGrpcService : BattleShipService.BattleShipServiceBase
{
    private readonly IGameService _gameService;

    public BattleShipGrpcService(IGameService gameService)
    {
        _gameService = gameService;
    }

    public override Task<AttackReply> Attack(AttackRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new AttackRequestDto(Guid.Parse(request.GameId), request.X, request.Y);
            var result = _gameService.Attack(dto);
            var reply = new AttackReply
            {
                GameId = result.GameId.ToString(),
                Status = result.Status.ToString(),
                PlayerX = result.PlayerShot.X,
                PlayerY = result.PlayerShot.Y,
                PlayerResult = result.PlayerShotResult.ToString()
            };
            if (result.AiShot is Coordinates aiShot)
            {
                reply.AiX = aiShot.X;
                reply.AiY = aiShot.Y;
                reply.AiResult = result.AiShotResult?.ToString() ?? string.Empty;
            }
            return Task.FromResult(reply);
        }
        catch (KeyNotFoundException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Game not found."));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }
}

