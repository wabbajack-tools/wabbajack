namespace Wabbajack.GameFinder.Paths.Trees.Traits.Interfaces;

/// <summary>
///     Represents an interface that selects an item from a given type denoted T.
/// </summary>
/// <typeparam name="T">Type to get the item from.</typeparam>
/// <typeparam name="TResult">The returned item.</typeparam>
public interface ISelector<T, TResult>
{
    /// <summary>
    ///     Checks if the item should be returned.
    /// </summary>
    /// <param name="item">The parameter from which the item is to be returned.</param>
    /// <returns>The returned item from the method.</returns>
    static abstract TResult Select(T item);
}
