using Enrollify.Application.Common;
using Xunit;

namespace Enrollify.Application.Tests;

public class GradeLevelsTests
{
    [Theory]
    [InlineData("Kindergarten", "Grade 1")]
    [InlineData("Grade 1", "Grade 2")]
    [InlineData("Grade 6", "Grade 7")]
    [InlineData("Grade 10", "Grade 11")]
    [InlineData("Grade 11", "Grade 12")]
    public void Promote_ReturnsNextGrade(string current, string expected)
    {
        Assert.Equal(expected, GradeLevels.Promote(current));
    }

    [Fact]
    public void Promote_Grade12_StaysGrade12()
    {
        Assert.Equal("Grade 12", GradeLevels.Promote("Grade 12"));
    }

    [Fact]
    public void Promote_UnknownInput_ReturnsInputUnchanged()
    {
        Assert.Equal("Nursery", GradeLevels.Promote("Nursery"));
    }

    [Fact]
    public void Promote_IsCaseInsensitive_AndReturnsCanonicalSpelling()
    {
        Assert.Equal("Grade 4", GradeLevels.Promote("grade 3"));
    }

    [Fact]
    public void All_MatchesFrontendGradeLevelList()
    {
        // Must stay in sync with GRADE_LEVELS in enrollify.client/src/app/core/constants.ts.
        Assert.Equal(new[]
        {
            "Kindergarten",
            "Grade 1", "Grade 2", "Grade 3", "Grade 4", "Grade 5", "Grade 6",
            "Grade 7", "Grade 8", "Grade 9", "Grade 10", "Grade 11", "Grade 12",
        }, GradeLevels.All);
    }
}
