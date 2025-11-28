using BattleShip.API.Endpoints;
using BattleShip.API.Game;
using BattleShip.API.Grpc;
using BattleShip.API.Hubs;
using BattleShip.API.Validation;
using BattleShip.Models.Game;
using FluentValidation;
using Grpc.AspNetCore.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGrpc();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IGameService, GameService>();
builder.Services.AddScoped<IValidator<AttackRequestDto>, AttackRequestValidator>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
    new[] { "http://localhost:5291", "https://localhost:7233" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("Frontend");
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.MapGameEndpoints();
app.MapGrpcService<BattleShipGrpcService>()
    .EnableGrpcWeb()
    .RequireCors("Frontend");
app.MapHub<GameHub>("/hubs/game");

app.Run();
