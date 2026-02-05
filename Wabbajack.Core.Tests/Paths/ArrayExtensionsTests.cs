using Xunit;

namespace Wabbajack.Paths.Test;

public class ArrayExtensionsTests
{
    [Fact]
    public void AreEqualTests()
    {
        Assert.True(ArrayExtensions.AreEqual(new[] {1, 2, 3}, 0, new[] {1, 2}, 0, 2));
        Assert.False(ArrayExtensions.AreEqual(new[] {1, 2, 3}, 0, new[] {1, 2}, 0, 3));
        Assert.False(ArrayExtensions.AreEqual(new[] {1, 2}, 1, new[] {1, 2, 3}, 0, 2));
    }

    [Fact]
    public void CompareTo()
    {
        Assert.Equal(0, ArrayExtensions.Compare(new[] {1, 1}, new[] {1, 1}));
        Assert.Equal(1, ArrayExtensions.Compare(new[] {1, 1, 1}, new[] {1, 1}));
        Assert.Equal(-1, ArrayExtensions.Compare(new[] {1, 1}, new[] {1, 1, 1}));
        Assert.Equal(1, ArrayExtensions.Compare(new[] {1, 2}, new[] {1, 1, 1}));

        Assert.Equal(0, ArrayExtensions.CompareString(new[] {"1", "1"}, new[] {"1", "1"}));
        Assert.Equal(1, ArrayExtensions.CompareString(new[] {"1", "1", "1"}, new[] {"1", "1"}));
        Assert.Equal(-1, ArrayExtensions.CompareString(new[] {"1", "1"}, new[] {"1", "1", "1"}));
        Assert.Equal(1, ArrayExtensions.CompareString(new[] {"1", "2"}, new[] {"1", "1", "1"}));
    }
}