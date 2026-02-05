using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Wabbajack.GameFinder.Paths.Trees.Traits.Interfaces;

namespace Wabbajack.GameFinder.Paths.Trees.Traits;

/// <summary>
///     An interface used by Tree implementations to indicate that they have a value inside the node.
/// </summary>
/// <typeparam name="TValue">The type of the value used.</typeparam>
public interface IHaveValue<out TValue>
{
    /// <summary>
    ///     The value of the node.
    /// </summary>
    TValue Value { get; }
}

/// <summary>
///    Extensions for <see cref="IHaveBoxedChildren{TSelf}"/>
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveValueExtensionsForIHaveBoxedChildren
{
    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The boxed node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesBfs<TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveValue<TValue>
        => item.Item.EnumerateValuesBfs<TSelf, TValue>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesBfs<TSelf, TValue>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveValue<TValue>
        => item.EnumerateChildrenBfs<TSelf, TValue, BoxValueSelector<TSelf, TValue>>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The boxed node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesDfs<TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveValue<TValue>
        => item.Item.EnumerateValuesDfs<TSelf, TValue>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesDfs<TSelf, TValue>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveValue<TValue>
        => item.EnumerateChildrenDfs<TSelf, TValue, BoxValueSelector<TSelf, TValue>>();

    /// <summary>
    ///     Recursively returns all the values of the children of this node.
    /// </summary>
    /// <param name="item">The boxed node whose child values to obtain.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An array of all the values of the children of this node.</returns>
    public static TValue[] GetValues<TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveValue<TValue>
        => item.Item.GetValues<TSelf, TValue>();

    /// <summary>
    ///     Recursively returns all the values of the children of this node.
    /// </summary>
    /// <param name="item">The node whose child values to obtain.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An array of all the values of the children of this node.</returns>
    public static TValue[] GetValues<TSelf, TValue>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveValue<TValue> =>
        item.GetChildrenRecursive<TSelf, TValue, ValueSelector<TSelf, TValue>>();

    /// <summary>
    ///     Helper method to populate values recursively.
    /// </summary>
    /// <param name="item">The current node.</param>
    /// <param name="buffer">
    ///     The span to fill with values.
    ///     Should be at least as big as <see cref="IHaveBoxedChildrenExtensions.CountChildren{TSelf}(Box{TSelf})"/>
    /// </param>
    /// <param name="index">The current index in the array.</param>
    [ExcludeFromCodeCoverage]
    public static void GetValuesUnsafe<TSelf, TValue>(TSelf item, Span<TValue> buffer, ref int index)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveValue<TValue> =>
        item.GetChildrenRecursiveUnsafe<TSelf, TValue, ValueSelector<TSelf, TValue>>(buffer, ref index);
}

/// <summary>
///    Extensions for <see cref="IHaveObservableChildren{TSelf}"/>
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveValueExtensionsForIHaveObservableChildren
{
    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The boxed node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesBfs<TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveValue<TValue>
        => item.Item.EnumerateValuesBfs<TSelf, TValue>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesBfs<TSelf, TValue>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveValue<TValue>
        => item.EnumerateChildrenBfs<TSelf, TValue, BoxValueSelector<TSelf, TValue>>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The boxed node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesDfs<TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveValue<TValue>
        => item.Item.EnumerateValuesDfs<TSelf, TValue>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesDfs<TSelf, TValue>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveValue<TValue>
        => item.EnumerateChildrenDfs<TSelf, TValue, BoxValueSelector<TSelf, TValue>>();

    /// <summary>
    ///     Recursively returns all the values of the children of this node.
    /// </summary>
    /// <param name="item">The boxed node whose child values to obtain.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An array of all the values of the children of this node.</returns>
    public static TValue[] GetValues<TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveValue<TValue>
        => item.Item.GetValues<TSelf, TValue>();

    /// <summary>
    ///     Recursively returns all the values of the children of this node.
    /// </summary>
    /// <param name="item">The node whose child values to obtain.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An array of all the values of the children of this node.</returns>
    public static TValue[] GetValues<TSelf, TValue>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveValue<TValue> =>
        item.GetChildrenRecursive<TSelf, TValue, ValueSelector<TSelf, TValue>>();

    /// <summary>
    ///     Helper method to populate values recursively.
    /// </summary>
    /// <param name="item">The current node.</param>
    /// <param name="buffer">
    ///     The span to fill with values.
    ///     Should be at least as big as <see cref="IHaveBoxedChildrenExtensions.CountChildren{TSelf}(Box{TSelf})"/>
    /// </param>
    /// <param name="index">The current index in the array.</param>
    [ExcludeFromCodeCoverage]
    public static void GetValuesUnsafe<TSelf, TValue>(TSelf item, Span<TValue> buffer, ref int index)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveValue<TValue> =>
        item.GetChildrenRecursiveUnsafe<TSelf, TValue, ValueSelector<TSelf, TValue>>(buffer, ref index);
}

/// <summary>
///    Extensions for <see cref="IHaveBoxedChildrenWithKey{TValue,TSelf}"/>
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveValueExtensionsForIHaveBoxedChildrenWithKey
{
    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The boxed node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesBfs<TSelf, TKey, TValue>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
        where TValue : notnull
        where TKey : notnull => item.Item.EnumerateValuesBfs<TSelf, TKey, TValue>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">The type of key.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesBfs<TSelf, TKey, TValue>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
        where TValue : notnull
        where TKey : notnull
        => item.EnumerateChildrenBfs<TSelf, TKey, TValue, KeyValueValueSelector<TSelf, TKey, TValue>>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The boxed node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesDfs<TSelf, TKey, TValue>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
        where TValue : notnull
        where TKey : notnull => item.Item.EnumerateValuesDfs<TSelf, TKey, TValue>();

    /// <summary>
    ///     Enumerates all values of the child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose child values are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">The type of key used.</typeparam>
    /// <returns>An IEnumerable of all child values of the current node.</returns>
    public static IEnumerable<TValue> EnumerateValuesDfs<TSelf, TKey, TValue>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
        where TValue : notnull
        where TKey : notnull
        => item.EnumerateChildrenDfs<TSelf, TKey, TValue, KeyValueValueSelector<TSelf, TKey, TValue>>();

    /// <summary>
    ///     Recursively returns all the values of the children of this node.
    /// </summary>
    /// <param name="item">The boxed node with keyed children whose values to obtain.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An array of all the values of the children of this node.</returns>
    public static TValue[] GetValues<TKey, TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
        where TKey : notnull
        => item.Item.GetValues<TKey, TSelf, TValue>();

    /// <summary>
    ///     Recursively returns all the values of the children of this node.
    /// </summary>
    /// <param name="item">The node with keyed children whose values to obtain.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>An array of all the values of the children of this node.</returns>
    public static TValue[] GetValues<TKey, TSelf, TValue>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
        where TKey : notnull
        => item.GetChildrenRecursive<TSelf, TKey, TValue, ValueSelector<TKey, TSelf, TValue>>();

    /// <summary>
    ///     Helper method to populate values recursively.
    /// </summary>
    /// <param name="item">The current node with keyed children.</param>
    /// <param name="buffer">
    ///     The span to fill with values.
    ///     Should be at least as big as the count of children.
    /// </param>
    /// <param name="index">The current index in the array.</param>
    [ExcludeFromCodeCoverage]
    public static void GetValuesUnsafe<TKey, TSelf, TValue>(TSelf item, Span<TValue> buffer, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
        where TKey : notnull
        => item.GetChildrenRecursiveUnsafe<TSelf, TKey, TValue, ValueSelector<TKey, TSelf, TValue>>(buffer, ref index);
}

/// <summary>
///    Extensions for <see cref="IHaveValue{TValue}"/>
/// </summary>
[ExcludeFromCodeCoverage] // Wrapper
// ReSharper disable once InconsistentNaming
public static class IHaveValueExtensions
{
    /// <summary>
    ///     Retrieves the value of the node.
    /// </summary>
    /// <param name="item">The keyed boxed node whose value is to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">Type of key used.</typeparam>
    /// <returns>The value of the node.</returns>
    public static TValue Value<TSelf, TKey, TValue>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveValue<TValue>
        where TValue : notnull
        where TKey : notnull
        => item.Value.Item.Value;

    /// <summary>
    ///     Retrieves the value of the node.
    /// </summary>
    /// <param name="item">The keyed boxed node whose value is to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">Type of key used.</typeparam>
    /// <returns>The value of the node.</returns>
    public static TValue Value<TSelf, TKey, TValue>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveValue<TValue>
        where TValue : notnull
        where TKey : notnull
        => item.Item.Value;

    /// <summary>
    ///     Retrieves the value of the node.
    /// </summary>
    /// <param name="item">The boxed node whose value is to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>The value of the node.</returns>
    public static TValue Value<TSelf, TValue>(this Box<TSelf> item)
        where TSelf : struct, IHaveValue<TValue>
        => item.Item.Value;
}

internal struct BoxValueSelector<TSelf, TValue> : ISelector<Box<TSelf>, TValue>
    where TSelf : struct, IHaveValue<TValue>
{
    public static TValue Select(Box<TSelf> item) => item.Item.Value;
}

internal struct KeyValueValueSelector<TSelf, TKey, TValue> : ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TValue>
    where TSelf : struct, IHaveValue<TValue>
    where TKey : notnull
{
    public static TValue Select(KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item) => item.Value.Item.Value;
}

internal struct ValueSelector<TSelf, TValue> : ISelector<TSelf, TValue>
    where TSelf : struct, IHaveValue<TValue>
{
    public static TValue Select(TSelf item) => item.Value;
}

internal struct ValueSelector<TKey, TSelf, TValue> : ISelector<TSelf, TValue>
    where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveValue<TValue>
    where TKey : notnull
{
    public static TValue Select(TSelf item) => item.Value;
}
