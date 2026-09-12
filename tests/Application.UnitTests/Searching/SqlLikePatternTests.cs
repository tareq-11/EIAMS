using Application.Abstractions.Searching;

namespace Application.UnitTests.Searching;

public sealed class SqlLikePatternTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateContains_ShouldReturnNull_ForBlankInput(string? input)
    {
        SqlLikePattern.CreateContains(input).ShouldBeNull();
    }

    [Fact]
    public void CreateContains_ShouldEscapeLikeOperatorsAndPreserveUnicode()
    {
        string? result = SqlLikePattern.CreateContains("  مَخزن%_\\AmMaN  ", normalizeToUpper: true);

        result.ShouldBe("%مَخزن\\%\\_\\\\AMMAN%");
    }
}
