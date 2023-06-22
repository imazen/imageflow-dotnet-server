using Tomlyn.Model;

namespace Imageflow.Server.Configuration.Tests;


public class TomlEnvPreprocessorTests
{
    [Fact]
    public void SimpleOverwriteWorks()
    {
        // Arrange
        const string testToml = @"
            [a]
            key=1
            [development.a]
            key=2
            [production.a]
            key=3
            [staging.a]
            key=4
        ";
        var preprocessor = new Imageflow.Server.Configuration.Parsing.TomlEnvironmentPreprocessor(testToml,
                                                   "development",
                                                   "test.toml");

        // Act
        var result = preprocessor.Preprocess();

        // Assert
        Assert.Equal(2L, (result.ProcessedModel["a"] as TomlTable)?["key"]);
    }


        [Fact]
    public void MergeTableWorks()
    {
        // Arrange
        const string testToml = @"
            [a]
            key=1
            keep='this'
            [development.a]
            key=2
            [production.a]
            key=3
            [staging.a]
            key=4
        ";
        var preprocessor = new Imageflow.Server.Configuration.Parsing.TomlEnvironmentPreprocessor(testToml,
                                                   "development",
                                                   "test.toml");

        // Act
        var result = preprocessor.Preprocess();

        // Assert
        Assert.Equal(2L, (result.ProcessedModel["a"] as TomlTable)?["key"]);
        
        Assert.Equal("this", (result.ProcessedModel["a"] as TomlTable)?["keep"]);
    }
}