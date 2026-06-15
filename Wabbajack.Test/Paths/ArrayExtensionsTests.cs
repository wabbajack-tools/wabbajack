namespace Wabbajack.Paths.Test;

public class ArrayExtensionsTests
{
    [Test]
    public async Task AreEqualTests()
    {
        await Assert.That(ArrayExtensions.AreEqual(new[] {1, 2, 3}, 0, new[] {1, 2}, 0, 2)).IsTrue();
        await Assert.That(ArrayExtensions.AreEqual(new[] {1, 2, 3}, 0, new[] {1, 2}, 0, 3)).IsFalse();
        await Assert.That(ArrayExtensions.AreEqual(new[] {1, 2}, 1, new[] {1, 2, 3}, 0, 2)).IsFalse();
    }

    [Test]
    public async Task CompareTo()
    {
        await Assert.That(ArrayExtensions.Compare(new[] {1, 1}, new[] {1, 1})).IsEqualTo(0);
        await Assert.That(ArrayExtensions.Compare(new[] {1, 1, 1}, new[] {1, 1})).IsEqualTo(1);
        await Assert.That(ArrayExtensions.Compare(new[] {1, 1}, new[] {1, 1, 1})).IsEqualTo(-1);
        await Assert.That(ArrayExtensions.Compare(new[] {1, 2}, new[] {1, 1, 1})).IsEqualTo(1);

        await Assert.That(ArrayExtensions.CompareString(new[] {"1", "1"}, new[] {"1", "1"})).IsEqualTo(0);
        await Assert.That(ArrayExtensions.CompareString(new[] {"1", "1", "1"}, new[] {"1", "1"})).IsEqualTo(1);
        await Assert.That(ArrayExtensions.CompareString(new[] {"1", "1"}, new[] {"1", "1", "1"})).IsEqualTo(-1);
        await Assert.That(ArrayExtensions.CompareString(new[] {"1", "2"}, new[] {"1", "1", "1"})).IsEqualTo(1);
    }
}
