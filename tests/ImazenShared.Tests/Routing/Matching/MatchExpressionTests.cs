using Imazen.Routing.Matching;
using Xunit;
namespace Imazen.Common.Tests.Routing.Matching;

public class MatchExpressionTests
{
    private static MatchingContext CaseSensitive = new MatchingContext
    {
        OrdinalIgnoreCase = false,
        SupportedImageExtensions = [],
    };
    private static MatchingContext CaseInsensitive = new MatchingContext
    {
        OrdinalIgnoreCase = true,
        SupportedImageExtensions = [],
    };
    [Theory]
    [InlineData(true, true, "/hi")]
    [InlineData(false, true, "/Hi")]
    [InlineData(true, false, "/Hi")]
    public void TestCaseSensitivity(bool isMatch, bool caseSensitive, string path)
    {
        var c = caseSensitive ? CaseSensitive : CaseInsensitive;
        var expr = MatchExpression.Parse(c, "/hi");
        Assert.Equal(isMatch, expr.IsMatch(c, path));
    }
    
    [Theory]
    [InlineData(true, "/{name}/{country}{:(/):?}", "/hi/usa", "/hi/usa/")]
    [InlineData(true, "/{name}/{country}{:eq(/):optional}", "/hi/usa", "/hi/usa/")]
    [InlineData(true, "/{name}/{country:len(3)}", "/hi/usa")]
    [InlineData(false, "/{name}/{country:length(3)}", "/hi/usa2")]
    [InlineData(false, "/{name}/{country:length(3)}", "/hi/usa/")]
    [InlineData(true, "/images/{seo_string_ignored}/{sku:guid}/{image_id:integer-range(0,1111111)}{width:integer:prefix(_):optional}.{format:equals(jpg|png|gif)}"
        , "/images/seo-string/12345678-1234-1234-1234-123456789012/12678_300.jpg", "/images/seo-string/12345678-1234-1234-1234-123456789012/12678.png")]
    
    public void TestAll(bool s, string expr, params string[] inputs)
    {
        var caseSensitive = expr.Contains("(i)");
        expr = expr.Replace("(i)", "");
        var c = caseSensitive ? CaseSensitive : CaseInsensitive;
        var me = MatchExpression.Parse(c, expr);
        foreach (var path in inputs)
        {
            var matched = me.TryMatchVerbose(c, path.AsMemory(), out var result, out var error);
            if (matched && !s)
            {
                Assert.Fail($"False positive! Expression {expr} should not have matched {path}! False positive.");
            }
            if (!matched && s)
            {
                Assert.Fail($"Expression {expr} incorrectly failed to match {path} with error {error}");
            }
        }
    }
    
    // Test MatchExpression.Parse
    [Fact]
    public void TestParse()
    {
        var c = CaseSensitive;
        var expr = MatchExpression.Parse(c, "/{name}/{country}{:equals(/):?}");
        Assert.Equal(5, expr.SegmentCount);
    }
}