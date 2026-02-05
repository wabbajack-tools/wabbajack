namespace Wabbajack.GameFinder.Paths.Trees.Traits.Interfaces;

/// <summary>
///     Represents a filter that can be applied to tree items.
/// </summary>
/// <typeparam name="T">Type to be filtered</typeparam>
public interface IFilter<T>
{
    /// <summary>
    ///     Checks if the item should be returned.
    /// </summary>
    /// <param name="item">The item to be checked.</param>
    /// <returns>True if the item should be returned, else false.</returns>
    static abstract bool Match(T item);
}

