using Imazen.Routing.Layers;

namespace Imazen.Routing.Engine;

public readonly record struct ExtensionlessPath(string Prefix, StringComparison StringComparison = StringComparison.OrdinalIgnoreCase)
    : IStringAndComparison
{
    public string StringToCompare => Prefix;
}