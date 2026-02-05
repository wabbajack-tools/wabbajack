using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Reloaded.Memory.Extensions;

namespace Wabbajack.GameFinder.Paths.Trees.Traits;

/// <summary>
///     An interface used by FileTree implementations to indicate that they have a parent.
/// </summary>
/// <typeparam name="TSelf">The concrete type stored inside this interface.</typeparam>
public interface IHaveParent<TSelf> where TSelf : struct, IHaveParent<TSelf>
{
    /// <summary>
    ///     Returns the parent node if it exists. If not, the node is considered the root node.
    /// </summary>
    /// <remarks>
    ///     This can be cast parent boxes like <see cref="KeyedBox{TKey,TSelf}"/> if using the appropriate type.
    /// </remarks>
    public Box<TSelf>? Parent { get; }

    /// <summary>
    ///     Returns true if the tree has a parent.
    /// </summary>
    bool HasParent => Parent != null;

    /// <summary>
    ///     Returns true if this is the root of the tree.
    /// </summary>
    bool IsTreeRoot => !HasParent;
}

/// <summary>
///     Trait methods for <see cref="IHaveBoxedChildren{TSelf}" />.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveParentExtensionsForIHaveBoxedChildren
{
    /// <summary>
    ///      Returns the total number of siblings in this node.
    /// </summary>
    /// <param name="item">The 'this' item.</param>
    /// <returns>The total amount of siblings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingCount<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>
        => item.Item.GetSiblingCount();

    /// <summary>
    ///      Returns the total number of siblings in this node.
    /// </summary>
    /// <param name="item">The 'this' item.</param>
    /// <returns>The total amount of siblings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingCount<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>
    {
        var parent = item.Parent;
        if (parent != null) // <= do not invert branch, hot path
            return parent.Item.Children.Length - 1; // -1 to exclude self.

        return 0;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Box<TSelf>[] GetSiblings<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var result = GC.AllocateUninitializedArray<Box<TSelf>>(item.GetSiblingCount());
        GetSiblingsUnsafe(item, result);
        return result;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Box<TSelf>[] GetSiblings<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var result = GC.AllocateUninitializedArray<Box<TSelf>>(item.GetSiblingCount());
        GetSiblingsUnsafe(item, result);
        return result;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <param name="resultsBuf">
    ///     The buffer which holds the results.
    ///     Please use <see cref="GetSiblingCount{TSelf}(Box{TSelf})"/> to obtain the required size.
    /// </param>
    /// <returns>The amount of siblings inserted into the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingsUnsafe<TSelf>(this Box<TSelf> item, Span<Box<TSelf>> resultsBuf)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        // Note: While this code is mostly duplicated from other overload, it is not the same.
        //       This compares reference equality, other compares value equality.
        var parent = item.Item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            var writeIndex = 0;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Equals(item))
                    resultsBuf.DangerousGetReferenceAt(writeIndex++) = child;
            }

            return item.GetSiblingCount();
        }

        return 0;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <param name="resultsBuf">
    ///     The buffer which holds the results.
    ///     Please use <see cref="GetSiblingCount{TSelf}(Box{TSelf})"/> to obtain the required size.
    /// </param>
    /// <returns>The amount of siblings inserted into the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingsUnsafe<TSelf>(this TSelf item, Span<Box<TSelf>> resultsBuf)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var parent = item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            var writeIndex = 0;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Item.Equals(item))
                    resultsBuf.DangerousGetReferenceAt(writeIndex++) = child;
            }

            return item.GetSiblingCount();
        }

        return 0;
    }

    /// <summary>
    ///      Enumerates all of the siblings of this node.
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<Box<TSelf>> EnumerateSiblings<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        // Note: While this code is mostly duplicated from other overload, it is not the same.
        //       This compares reference equality, other compares value equality.
        var parent = item.Item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Equals(item))
                    yield return child;
            }
        }
    }

    /// <summary>
    ///      Enumerates all of the siblings of this node.
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<Box<TSelf>> EnumerateSiblings<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var parent = item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Item.Equals(item))
                    yield return child;
            }
        }
    }
}

/// <summary>
///     Trait methods for <see cref="IHaveObservableChildren{TSelf}" />.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveParentExtensionsForIHaveObservableChildren
{
    /// <summary>
    ///      Returns the total number of siblings in this node.
    /// </summary>
    /// <param name="item">The 'this' item.</param>
    /// <returns>The total amount of siblings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingCount<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>
        => item.Item.GetSiblingCount();

    /// <summary>
    ///      Returns the total number of siblings in this node.
    /// </summary>
    /// <param name="item">The 'this' item.</param>
    /// <returns>The total amount of siblings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingCount<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>
    {
        var parent = item.Parent;
        if (parent != null) // <= do not invert branch, hot path
            return parent.Item.Children.Count - 1; // -1 to exclude self.

        return 0;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Box<TSelf>[] GetSiblings<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var result = GC.AllocateUninitializedArray<Box<TSelf>>(item.GetSiblingCount());
        GetSiblingsUnsafe(item, result);
        return result;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Box<TSelf>[] GetSiblings<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var result = GC.AllocateUninitializedArray<Box<TSelf>>(item.GetSiblingCount());
        GetSiblingsUnsafe(item, result);
        return result;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <param name="resultsBuf">
    ///     The buffer which holds the results.
    ///     Please use <see cref="GetSiblingCount{TSelf}(Box{TSelf})"/> to obtain the required size.
    /// </param>
    /// <returns>The amount of siblings inserted into the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingsUnsafe<TSelf>(this Box<TSelf> item, Span<Box<TSelf>> resultsBuf)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        // Note: While this code is mostly duplicated from other overload, it is not the same.
        //       This compares reference equality, other compares value equality.
        var parent = item.Item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            var writeIndex = 0;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Equals(item))
                    resultsBuf.DangerousGetReferenceAt(writeIndex++) = child;
            }

            return item.GetSiblingCount();
        }

        return 0;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <param name="resultsBuf">
    ///     The buffer which holds the results.
    ///     Please use <see cref="GetSiblingCount{TSelf}(Box{TSelf})"/> to obtain the required size.
    /// </param>
    /// <returns>The amount of siblings inserted into the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingsUnsafe<TSelf>(this TSelf item, Span<Box<TSelf>> resultsBuf)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var parent = item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            var writeIndex = 0;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Item.Equals(item))
                    resultsBuf.DangerousGetReferenceAt(writeIndex++) = child;
            }

            return item.GetSiblingCount();
        }

        return 0;
    }

    /// <summary>
    ///      Enumerates all of the siblings of this node.
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<Box<TSelf>> EnumerateSiblings<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        // Note: While this code is mostly duplicated from other overload, it is not the same.
        //       This compares reference equality, other compares value equality.
        var parent = item.Item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Equals(item))
                    yield return child;
            }
        }
    }

    /// <summary>
    ///      Enumerates all of the siblings of this node.
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<Box<TSelf>> EnumerateSiblings<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveParent<TSelf>, IEquatable<TSelf>
    {
        var parent = item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Item.Equals(item))
                    yield return child;
            }
        }
    }
}

/// <summary>
///     Trait methods for <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" />.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveParentExtensionsForIHaveBoxedChildrenWithKey
{
    /// <summary>
    ///      Returns the total number of siblings in this node.
    /// </summary>
    /// <param name="item">The 'this' item.</param>
    /// <returns>The total amount of siblings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingCount<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf> where TKey : notnull
        => item.Item.GetSiblingCount<TSelf, TKey>();

    /// <summary>
    ///      Returns the total number of siblings in this node.
    /// </summary>
    /// <param name="item">The 'this' item.</param>
    /// <returns>The total amount of siblings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingCount<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf> where TKey : notnull
    {
        var parent = item.Parent;
        if (parent != null) // <= do not invert branch, hot path
            return parent.Item.Children.Count - 1; // -1 to exclude self.

        return 0;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeyedBox<TKey, TSelf>[] GetSiblings<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf>, IEquatable<TSelf> where TKey : notnull
    {
        var count = item.GetSiblingCount<TSelf, TKey>();
        var result = GC.AllocateUninitializedArray<KeyedBox<TKey, TSelf>>(count);
        GetSiblingsUnsafe<TSelf, TKey>(item, result);
        return result;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <param name="resultsBuf">
    ///     The buffer which holds the results.
    ///     Please use <see cref="GetSiblingCount{TSelf,TKey}(TSelf)"/> to obtain the required size.
    /// </param>
    /// <returns>The amount of siblings inserted into the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingsUnsafe<TSelf, TKey>(this TSelf item, Span<KeyedBox<TKey, TSelf>> resultsBuf)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf>, IEquatable<TSelf> where TKey : notnull
    {
        var parent = item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            var writeIndex = 0;
            foreach (var child in parentChildren)
            {
                if (!child.Value.Item.Equals(item))
                    resultsBuf.DangerousGetReferenceAt(writeIndex++) = child.Value;
            }

            return item.GetSiblingCount<TSelf, TKey>();
        }

        return 0;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeyedBox<TKey, TSelf>[] GetSiblings<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf>, IEquatable<TSelf> where TKey : notnull
    {
        var count = item.GetSiblingCount();
        var result = GC.AllocateUninitializedArray<KeyedBox<TKey, TSelf>>(count);
        GetSiblingsUnsafe(item, result);
        return result;
    }

    /// <summary>
    ///      Returns all of the siblings of this node (excluding itself).
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <param name="resultsBuf">
    ///     The buffer which holds the results.
    ///     Please use <see cref="GetSiblingCount{TSelf,TKey}(TSelf)"/> to obtain the required size.
    /// </param>
    /// <returns>The amount of siblings inserted into the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSiblingsUnsafe<TSelf, TKey>(this KeyedBox<TKey, TSelf> item, Span<KeyedBox<TKey, TSelf>> resultsBuf)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf>, IEquatable<TSelf> where TKey : notnull
    {
        var parent = item.Item.Parent;

        // Note: While this code is mostly duplicated from other overload, it is not the same.
        //       This compares reference equality, other compares value equality.
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            var writeIndex = 0;
            foreach (var child in parentChildren)
            {
                if (!child.Value.Equals(item))
                    resultsBuf.DangerousGetReferenceAt(writeIndex++) = child.Value;
            }

            return item.GetSiblingCount();
        }

        return 0;
    }

    /// <summary>
    ///      Enumerates all of the siblings of this node.
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<KeyedBox<TKey, TSelf>> EnumerateSiblings<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf>, IEquatable<TSelf> where TKey : notnull
    {
        var parent = item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Value.Item.Equals(item))
                    yield return child.Value;
            }
        }
    }

    /// <summary>
    ///      Enumerates all of the siblings of this node.
    /// </summary>
    /// <param name="item">The item whose siblings to obtain.</param>
    /// <returns>All of the siblings of this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<KeyedBox<TKey, TSelf>> EnumerateSiblings<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveParent<TSelf>, IEquatable<TSelf> where TKey : notnull
    {
        var parent = item.Item.Parent;
        // ReSharper disable once InvertIf
        if (parent != null) // <= do not invert, hot path.
        {
            var parentChildren = parent.Item.Children;
            foreach (var child in parentChildren) // <= lowered to 'for'
            {
                if (!child.Value.Equals(item))
                    yield return child.Value;
            }
        }
    }
}

/// <summary>
///     Trait methods for <see cref="IHaveParent{TSelf}"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveParentExtensions
{
    /// <summary>
    ///     Gets the parent of the given boxed item.
    /// </summary>
    /// <param name="item">The boxed item whose parent is to be retrieved.</param>
    /// <returns>The parent of the item, or null if it is the root.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static Box<TSelf>? Parent<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveParent<TSelf>
        => item.Item.Parent;

    /// <summary>
    ///     Checks if the given boxed item has a parent.
    /// </summary>
    /// <param name="item">The boxed item to check.</param>
    /// <returns>True if the item has a parent; otherwise, false.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static bool HasParent<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveParent<TSelf>
        => item.Item.HasParent;

    /// <summary>
    ///     Checks if the given boxed item is the root of the tree.
    /// </summary>
    /// <param name="item">The boxed item to check.</param>
    /// <returns>True if the item is the root of the tree; otherwise, false.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static bool IsTreeRoot<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveParent<TSelf>
        => item.Item.IsTreeRoot;

    /// <summary>
    ///     Gets the parent of the given keyed boxed item.
    /// </summary>
    /// <param name="item">The keyed boxed item whose parent is to be retrieved.</param>
    /// <returns>The parent of the item, or null if it is the root.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static KeyedBox<TKey, TSelf>? Parent<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveParent<TSelf>
        where TKey : notnull
        => Unsafe.As<KeyedBox<TKey, TSelf>>(item.Value.Item.Parent);

    /// <summary>
    ///     Gets the parent of the given keyed boxed item.
    /// </summary>
    /// <param name="item">The keyed boxed item whose parent is to be retrieved.</param>
    /// <returns>The parent of the item, or null if it is the root.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static KeyedBox<TKey, TSelf>? Parent<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveParent<TSelf>
        where TKey : notnull
        => Unsafe.As<KeyedBox<TKey, TSelf>>(item.Item.Parent);

    /// <summary>
    ///     Checks if the given keyed boxed item has a parent.
    /// </summary>
    /// <param name="item">The keyed boxed item to check.</param>
    /// <returns>True if the item has a parent; otherwise, false.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static bool HasParent<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveParent<TSelf>
        where TKey : notnull
        => item.Value.Item.HasParent;

    /// <summary>
    ///     Checks if the given keyed boxed item has a parent.
    /// </summary>
    /// <param name="item">The keyed boxed item to check.</param>
    /// <returns>True if the item has a parent; otherwise, false.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static bool HasParent<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveParent<TSelf>
        where TKey : notnull
        => item.Item.HasParent;

    /// <summary>
    ///     Checks if the given keyed boxed item is the root of the tree.
    /// </summary>
    /// <param name="item">The keyed boxed item to check.</param>
    /// <returns>True if the item is the root of the tree; otherwise, false.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static bool IsTreeRoot<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveParent<TSelf>
        where TKey : notnull
        => item.Value.Item.IsTreeRoot;

    /// <summary>
    ///     Checks if the given keyed boxed item is the root of the tree.
    /// </summary>
    /// <param name="item">The keyed boxed item to check.</param>
    /// <returns>True if the item is the root of the tree; otherwise, false.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static bool IsTreeRoot<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveParent<TSelf>
        where TKey : notnull
        => item.Item.IsTreeRoot;

    /// <summary>
    ///     Finds a node by traversing up the parent nodes, matching keys in reverse order.
    ///     Returns the node corresponding to last key in the span.
    /// </summary>
    /// <param name="node">The starting node for the search.</param>
    /// <param name="keys">The sequence of keys to match, in reverse order.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <typeparam name="TKey">The type of the key used in the tree.</typeparam>
    /// <returns>The node that matches the sequence of keys from end to start, or null if no match is found.</returns>
    public static Box<TSelf>? FindByKeyUpward<TSelf, TKey>(this Box<TSelf> node, Span<TKey> keys)
        where TSelf : struct, IHaveParent<TSelf>, IHaveKey<TKey>
        where TKey : notnull
        => keys.Length == 0 ? null : node.FindByKeysUpwardWithNonZeroKey(keys);

    /// <summary>
    ///     Finds a node by traversing up the parent nodes, matching keys in reverse order.
    ///     Returns the node corresponding to last key in the span.
    /// </summary>
    /// <param name="node">The starting node for the search.</param>
    /// <param name="keys">The sequence of keys to match, in reverse order.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <typeparam name="TKey">The type of the key used in the tree.</typeparam>
    /// <returns>The node that matches the sequence of keys from end to start, or null if no match is found.</returns>
    [ExcludeFromCodeCoverage]
    public static Box<TSelf>? FindByKeyUpward<TSelf, TKey>(this KeyedBox<TKey, TSelf> node, Span<TKey> keys)
        where TSelf : struct, IHaveParent<TSelf>, IHaveKey<TKey>
        where TKey : notnull
        => keys.Length == 0 ? null : node.FindByKeysUpwardWithNonZeroKey(keys);

    /// <summary>
    ///     Verifies the path of this node against a Span of keys (inverse FindByKey).
    ///     Returns the node corresponding to last key in the span.
    /// </summary>
    /// <param name="node">The starting node for the search.</param>
    /// <param name="keys">The sequence of keys to match, in reverse order.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <typeparam name="TKey">The type of the key used in the tree.</typeparam>
    /// <returns>The node that matches the sequence of keys from end to start, or null if no match is found.</returns>
    [ExcludeFromCodeCoverage]
    [Obsolete("Do not use this overload with key length <= 1. This overload also boxes. Use the overload with boxed TSelf instead.")]
    public static Box<TSelf>? FindByKeyUpward<TSelf, TKey>(this TSelf node, Span<TKey> keys)
        where TSelf : struct, IHaveParent<TSelf>, IHaveKey<TKey>
        where TKey : notnull
        => keys.Length <= 1 ? null : ((Box<TSelf>) node).FindByKeysUpwardWithNonZeroKey(keys); // for your own good, this returns null if key has 1 item.

    internal static Box<TSelf>? FindByKeysUpwardWithNonZeroKey<TSelf, TKey>(this Box<TSelf> node, Span<TKey> keys)
        where TSelf : struct, IHaveParent<TSelf>, IHaveKey<TKey>
        where TKey : notnull
    {
        var keyIndex = keys.Length - 1;
        var currentNode = node;
        while (keyIndex >= 0)
        {
            if (!EqualityComparer<TKey>.Default.Equals(currentNode.Item.Key, keys.DangerousGetReferenceAt(keyIndex)))
                return null;

            keyIndex--;
            if (currentNode.HasParent())
                currentNode = currentNode.Parent()!;
            else
                break;
        }

        return keyIndex < 0 ? node : null;
    }

    /// <summary>
    ///     Verifies the path of this node against a Span of keys (inverse FindByKey).
    ///     Returns the node corresponding to first key in the span.
    /// </summary>
    /// <param name="node">The starting node for the search.</param>
    /// <param name="keys">The sequence of keys to match, in reverse order.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <typeparam name="TKey">The type of the key used in the tree.</typeparam>
    /// <returns>The node that matches the sequence of keys from start to end, or null if no match is found.</returns>
    [ExcludeFromCodeCoverage]
    [Obsolete("Do not use this overload with key length <= 1. This overload also boxes. Use the overload with boxed TSelf instead.")]
    public static Box<TSelf>? FindRootByKeyUpward<TSelf, TKey>(this TSelf node, Span<TKey> keys)
        where TSelf : struct, IHaveParent<TSelf>, IHaveKey<TKey>
        where TKey : notnull
        => keys.Length <= 1 ? null : ((Box<TSelf>) node).FindRootByKeyUpward(keys); // for your own good, this returns null if key has 1 item.

    /// <summary>
    ///     Verifies the path of this node against a Span of keys (inverse FindByKey).
    ///     Returns the node corresponding to first key in the span.
    /// </summary>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <typeparam name="TKey">The type of the key used in the tree.</typeparam>
    /// <param name="node">The starting keyed boxed node for the search.</param>
    /// <param name="keys">The sequence of keys to match, in reverse order.</param>
    /// <returns>The node that matches the sequence of keys from start to end, or null if no match is found.</returns>
    [ExcludeFromCodeCoverage]
    public static Box<TSelf>? FindRootByKeyUpward<TSelf, TKey>(this KeyedBox<TKey, TSelf> node, Span<TKey> keys)
        where TSelf : struct, IHaveParent<TSelf>, IHaveKey<TKey>
        where TKey : notnull
        => ((Box<TSelf>)node).FindRootByKeyUpward(keys);

    /// <summary>
    ///     Verifies the path of this node against a Span of keys (inverse FindByKey).
    ///     Returns the node corresponding to first key in the span.
    /// </summary>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <typeparam name="TKey">The type of the key used in the tree.</typeparam>
    /// <param name="node">The starting keyed boxed node for the search.</param>
    /// <param name="keys">The sequence of keys to match, in reverse order.</param>
    /// <returns>The node that matches the sequence of keys from start to end, or null if no match is found.</returns>
    public static Box<TSelf>? FindRootByKeyUpward<TSelf, TKey>(this Box<TSelf> node, Span<TKey> keys)
        where TSelf : struct, IHaveParent<TSelf>, IHaveKey<TKey>
        where TKey : notnull
    {
        if (keys.IsEmpty)
            return null;

        var currentNode = node;
        for (var x = keys.Length - 1; x >= 0; x--)
        {
            if (!EqualityComparer<TKey>.Default.Equals(currentNode!.Item.Key, keys[x]))
                return null;

            // If we've hit the root, return it.
            if (x == 0)
                return currentNode;

            // Else navigate up.
            if (currentNode.HasParent())
                currentNode = currentNode.Parent();
            else
                return null;
        }

        // Unreachable.
        return null;
    }
}
