using BattleShip.API.Endpoints;
using BattleShip.API.Game;
using BattleShip.API.Grpc;
using BattleShip.API.Hubs;
using BattleShip.API.Validation;
using BattleShip.Models.Game;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGrpc();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IGameService, GameService>();
builder.Services.AddScoped<IValidator<AttackRequestDto>, AttackRequestValidator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapGameEndpoints();
app.MapGrpcService<BattleShipGrpcService>();
app.MapHub<GameHub>("/hubs/game");

app.Run();
