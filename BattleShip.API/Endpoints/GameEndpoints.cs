using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BattleShip.Models.Game;
using FluentValidation;

namespace BattleShip.API.Endpoints;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games");
        group.MapPost("/", StartGame);
        group.MapGet("/{gameId:guid}", GetState);
        group.MapPost("/{gameId:guid}/attack", Attack);
        group.MapGet("/{gameId:guid}/history", GetHistory);
        group.MapPost("/{gameId:guid}/undo", Undo);
        group.MapPost("/{gameId:guid}/restart", Restart);
        app.MapGet("/api/leaderboard", (IGameService gameService) => Results.Ok(gameService.GetLeaderboard()));
        return app;
    }

    private static async Task<IResult> StartGame(IGameService gameService, StartGameRequestDto request)
    {
        try
        {
            var game = gameService.StartNewGame(request);
            var state = gameService.GetState(game.Id);
            var response = new StartGameResponseDto(game.Id, state);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult GetState(Guid gameId, IGameService gameService)
    {
        try
        {
            var state = gameService.GetState(gameId);
            return Results.Ok(state);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> Attack(Guid gameId, AttackRequestDto request, IGameService gameService, IValidator<AttackRequestDto> validator)
    {
        var payload = request with { GameId = gameId };
        var validation = await validator.ValidateAsync(payload);
        if (!validation.IsValid) return Results.BadRequest(CreateValidationProblem(validation));
        try
        {
            var result = gameService.Attack(payload);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult GetHistory(Guid gameId, IGameService gameService)
    {
        try
        {
            var history = gameService.GetHistory(gameId);
            return Results.Ok(history);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static IResult Undo(Guid gameId, IGameService gameService)
    {
        try
        {
            var state = gameService.UndoLastMove(gameId);
            return Results.Ok(state);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult Restart(Guid gameId, IGameService gameService)
    {
        try
        {
            var state = gameService.Restart(gameId);
            var response = new StartGameResponseDto(state.GameId, state);
            return Results.Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static IDictionary<string, string[]> CreateValidationProblem(FluentValidation.Results.ValidationResult result)
    {
        return result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}

