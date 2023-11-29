namespace Imazen.Routing.Health;

public enum BehaviorTask: byte
{
    FetchData,
    FetchMetadata,
    Put,
    Delete,
    SearchByTag,
    PurgeByTag,
    HealthCheck
}

internal static class BehaviorTaskHelpers
{
    internal const int BehaviorTaskCount = 8;
}