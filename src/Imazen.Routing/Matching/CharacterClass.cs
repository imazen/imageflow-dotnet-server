using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Imazen.Abstractions.Internal;

namespace Imazen.Routing.Matching;

/// <summary>
/// Parsed and interned representation of a character class like [0-9a-zA-Z] or [^/] or [^a-z] or [\\\t\n\r\[\]\{\)\,]
/// </summary>
public record CharacterClass(
    bool IsNegated,
    ReadOnlyCollection<CharacterClass.CharRange> Ranges,
    ReadOnlyCollection<char> Characters)
{
    public override string ToString()
    {
        // 5 is our guess on escaped chars
        var sb = new StringBuilder(3 + Ranges.Count * 3 + Characters.Count + 5);
        sb.Append(IsNegated ? "[^" : "[");
        foreach (var range in Ranges)
        {
            AppendEscaped(sb, range.Start);
            sb.Append('-');
            AppendEscaped(sb, range.End);
        }
        foreach (var c in Characters)
        {
            AppendEscaped(sb, c);
        }
        sb.Append(']');
        return sb.ToString();
    }
    private void AppendEscaped(StringBuilder sb, char c)
    {
        if (ValidCharsToEscape.Contains(c))
        {
            sb.Append('\\');
        }
        sb.Append(c);
    }


    public record struct CharRange(char Start, char End);

    
    public bool Contains(char c)
    {
        return IsNegated ? !WithinSet(c) : WithinSet(c);
    }
    private bool WithinSet(char c)
    {
        if (Characters.Contains(c)) return true;
        foreach (var range in Ranges)
        {
            if (c >= range.Start && c <= range.End) return true;
        }
        return false;
    }
 
    
    private static ulong HashSpan(ReadOnlySpan<char> span)
    {
        var fnv = Fnv1AHash.Create();
        fnv.Add(span);
        return fnv.CurrentHash;
    }
    private record ParseResult(bool Success, string Input, CharacterClass? Result, string? Error);
    private static readonly Lazy<ConcurrentDictionary<string, ParseResult>> InternedParseResults = new Lazy<ConcurrentDictionary<string, ParseResult>>();
    private static readonly Lazy<ConcurrentDictionary<ulong, ParseResult>> InternedParseResultsByHash = new Lazy<ConcurrentDictionary<ulong, ParseResult>>();
    private static ParseResult? TryGetFromInterned(ReadOnlySpan<char> syntax)
    {
        var hash = HashSpan(syntax);
        if (InternedParseResultsByHash.Value.TryGetValue(hash, out var parseResult))
        {
            if (!syntax.Is(parseResult.Input))
            {
                // This is a hash collision, fall back to the slow path
                if (InternedParseResults.Value.TryGetValue(syntax.ToString(), out parseResult))
                {
                    return parseResult;
                }
                return default;
            }
            return parseResult;
        }
        return default;
    }
    
    
    public static bool TryParseInterned(ReadOnlyMemory<char> syntax, bool storeIfMissing,
        [NotNullWhen(true)] out CharacterClass? result,
        [NotNullWhen(false)] out string? error)
    {
        var span = syntax.Span;
        var existing = TryGetFromInterned(span);
        if (existing is not null)
        {
            result = existing.Result;
            error = existing.Error;
            return existing.Success;
        }
        var success = TryParse(syntax, out result, out error);
        if (storeIfMissing)
        {
            var hash = HashSpan(span);
            var str = syntax.ToString();
            InternedParseResultsByHash.Value.TryAdd(hash, new ParseResult(success, str, result, error));
            InternedParseResults.Value.TryAdd(str, new ParseResult(success, str, result, error));
        }
        return success;
    }
    
    
    public static bool TryParse(ReadOnlyMemory<char> syntax,
        [NotNullWhen(true)] out CharacterClass? result,
        [NotNullWhen(false)] out string? error)
    {
        var span = syntax.Span;
        if (span.Length < 3)
        {
            error = "Character class must be at least 3 characters long";
            result = default;
            return false;
        }
        if (span[0] != '[')
        {
            error = "Character class must start with [";
            result = default;
            return false;
        }

        if (span[^1] != ']')
        {
            error = "Character class must end with ]";
            result = default;
            return false;
        }

        var isNegated = span[1] == '^';
        if (isNegated)
        {
            if (span.Length < 4)
            {
                error = "Negated character class must be at least 4 characters long";
                result = default;
                return false;
            }
        }

        var startFrom = isNegated ? 2 : 1;
        return TryParseInner(isNegated, syntax[startFrom..^1], out result, out error);
    }

    private enum LexTokenType : byte
    {
        ControlDash,
        EscapedCharacter,
        SingleCharacter,
        PredefinedClass,
        DanglingEscape,
        IncorrectlyEscapedCharacter,
        SuspiciousEscapedCharacter
    }

    private readonly record struct LexToken(LexTokenType Type, char Value)
    {
        public bool IsValidCharacter => Type is LexTokenType.EscapedCharacter or LexTokenType.SingleCharacter;
    }

    private static readonly char[] SuspiciousCharsToEscape = ['d', 'D', 's', 'S', 'w', 'W', 'b', 'B'];

    private static readonly char[] ValidCharsToEscape =
        ['t', 'n', 'r', 'f', 'v', '0', '[', ']', '\\', '-', '^', ',', '(', ')', '{', '}', '|'];

    private static readonly LexToken ControlDashToken = new LexToken(LexTokenType.ControlDash, '-');

    private static IEnumerable<LexToken> LexInner(ReadOnlyMemory<char> syntax)
    {
        var i = 0;
        while (i < syntax.Length)
        {   
            var c = syntax.Span[i];
            if (c == '\\')
            {
                if (i == syntax.Length - 1)
                {
                    yield return new LexToken(LexTokenType.DanglingEscape, '\\');
                }
                var c2 = syntax.Span[i + 1];

                if (c2 == 'w')
                {
                    yield return new LexToken(LexTokenType.PredefinedClass, c2);
                    i += 2;
                    continue;
                }

                if (ValidCharsToEscape.Contains(c2))
                {
                    yield return new LexToken(LexTokenType.EscapedCharacter, c2);
                    i += 2;
                    continue;
                }

                if (SuspiciousCharsToEscape.Contains(c2))
                {
                    yield return new LexToken(LexTokenType.SuspiciousEscapedCharacter, c2);
                    i += 2;
                    continue;
                }

                yield return new LexToken(LexTokenType.IncorrectlyEscapedCharacter, c2);
            }

            if (c == '-')
            {
                yield return ControlDashToken;
                i++;
                continue;
            }

            yield return new LexToken(LexTokenType.SingleCharacter, c);
            i++;
        }
    }

    /// <summary>
    /// Here we parse the inside, like "0-9a-z\t\\r\n\[\]\{\}\,A-Z"
    /// First we break it into units - escaped characters, single characters, predefined classes, and the control character '-'
    /// </summary>
    /// <param name="negated"></param>
    /// <param name="syntax"></param>
    /// <param name="result"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    private static bool TryParseInner(bool negated, ReadOnlyMemory<char> syntax,
        [NotNullWhen(true)] out CharacterClass? result,
        [NotNullWhen(false)] out string? error)
    {

        List<CharRange>? ranges = null;
        List<char>? characters = null;

        var tokens = LexInner(syntax).ToList();
        // Reject if we have dangling escape, incorrectly escaped character, or suspicious escaped character
        if (tokens.Any(t => t.Type is LexTokenType.DanglingEscape))
        {
            error = "Dangling backslash in character class";
            result = default;
            return false;
        }

        if (tokens.Any(t => t.Type is LexTokenType.IncorrectlyEscapedCharacter))
        {
            error =
                $"Incorrectly escaped character '{tokens.First(t => t.Type is LexTokenType.IncorrectlyEscapedCharacter).Value}' in character class";
            result = default;
            return false;
        }

        if (tokens.Any(t => t.Type is LexTokenType.SuspiciousEscapedCharacter))
        {
            var t = tokens.First(t => t.Type is LexTokenType.SuspiciousEscapedCharacter);
            // This feature isn't supported
            error = $"You probably meant to use a predefined character range with \'{t.Value}'; it is not supported.";
            result = default;
            return false;
        }

        // Search for ranges
        int indexOfDash = tokens.IndexOf(ControlDashToken);
        while (indexOfDash != -1)
        {
            // if it's the first, the last, or bounded by a character class, then it's an error
            if (indexOfDash == 0 || indexOfDash == tokens.Count - 1 || !tokens[indexOfDash - 1].IsValidCharacter ||
                !tokens[indexOfDash + 1].IsValidCharacter)
            {
                //TODO: improve error message
                error = "Dashes can only be used between single characters in a character class";
                result = default;
                return false;
            }

            // Extract the range
            var start = tokens[indexOfDash - 1].Value;
            var end = tokens[indexOfDash + 1].Value;
            if (start > end)
            {
                error =
                    $"Character class range must go from lower to higher, here [{syntax.ToString()}] it goes from {(int)start} to {(int)end}";
                result = default;
                return false;
            }

            ranges ??= new List<CharRange>();
            ranges.Add(new CharRange(start, end));
            // Mutate the collection and re-search
            tokens.RemoveRange(indexOfDash - 1, 3);
            indexOfDash = tokens.IndexOf(ControlDashToken);
        }

        // The rest are single characters or predefined classes
        foreach (var token in tokens)
        {
            if (token.Type is LexTokenType.SingleCharacter or LexTokenType.EscapedCharacter)
            {
                characters ??= [];
                characters.Add(token.Value);
            }
            else if (token.Type is LexTokenType.PredefinedClass)
            {
                if (token.Value == 'w')
                {
                    ranges ??= [];
                    ranges.AddRange(new[]
                    {
                        new CharRange('a', 'z'), new CharRange('A', 'Z'),
                        new CharRange('0', '9')
                    });
                    characters ??= [];
                    characters.Add('_');
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported predefined character class {token.Value}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Unexpected token type {token.Type}");
            }
        }

        characters ??= [];
        ranges ??= [];
        result = new CharacterClass(negated, new ReadOnlyCollection<CharRange>(ranges),
            new ReadOnlyCollection<char>(characters));
        error = null;
        return true;
    }
}