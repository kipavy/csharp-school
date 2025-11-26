namespace BattleShip.App.Services;

public sealed class ApiConfiguration
{
    public ApiConfiguration(Uri restBaseAddress, Uri grpcBaseAddress)
    {
        RestBaseAddress = restBaseAddress;
        GrpcBaseAddress = grpcBaseAddress;
    }

    public Uri RestBaseAddress { get; }
    public Uri GrpcBaseAddress { get; }
    public Uri HubBaseAddress => RestBaseAddress;
}

