using ImageShot;
using Xunit;

namespace ImageShot.Tests
{
    public class OutputTests
    {
        [Theory]
        [InlineData("Dokument1 - Word", "Dokument1 - Word")]
        [InlineData("a<b>c:d\"e/f\\g|h?i*j~k", "abcdefghijk")]
        [InlineData("  ..navn..  ", "navn")]
        [InlineData("to   mellemrum", "to mellemrum")]
        [InlineData(null, "")]
        public void Clean_RemovesIllegalCharacters(string input, string expected)
        {
            Assert.Equal(expected, Output.Clean(input));
        }
    }
}
