using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class LanguageExceptionTests
{
    [Fact]
    public void LanguageAndFormatExceptionsPreserveMessagesAndInnerErrors()
    {
        var cause = new FormatException("bad literal");
        var language = new LanguageException("language failure", cause);
        var invalidFormat = new InvalidFormatException("invalid format", cause);

        Assert.Equal("language failure", language.Message);
        Assert.Same(cause, language.InnerException);
        Assert.IsAssignableFrom<LanguageException>(invalidFormat);
        Assert.Equal("invalid format", invalidFormat.Message);
        Assert.Same(cause, invalidFormat.InnerException);
        Assert.Equal("format failure", new InvalidFormatException("format failure").Message);
    }

    [Fact]
    public void SyntaxExceptionRetainsSourceCoordinates()
    {
        var exception = new SyntaxException("unexpected token", 12, 2, 4);

        Assert.Equal("unexpected token", exception.Message);
        Assert.Equal(12, exception.Position);
        Assert.Equal(2, exception.Line);
        Assert.Equal(4, exception.Column);
        Assert.Equal("empty", new SyntaxException("empty").Message);
    }
}
