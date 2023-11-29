namespace Imazen.Abstractions.Resulting;

public sealed class Empty
{
    static readonly Empty Singleton = new Empty();
    public static Empty Value => Singleton;
}