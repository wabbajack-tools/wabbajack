using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wabbajack.GameFinder.Paths.Trees.Traits.Interfaces;

namespace Wabbajack.GameFinder.Paths.Trees.Traits;

/// <summary>
///     An interface used by Tree implementations to indicate whether an item is a file/directory.
/// </summary>
public interface IHaveAFileOrDirectory
{
    /// <summary>
    ///     Returns true if this item represents a file.
    /// </summary>
    public bool IsFile { get; }

    /// <summary>
    ///     Returns true if this item represents a directory.
    /// </summary>
    public bool IsDirectory => !IsFile;
}

/// <summary>
///     Trait methods for <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" />.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveAFileOrDirectoryExtensionsForIHaveBoxedChildrenWithKey
{
    /// <summary>
    ///     Counts the number of files present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior file count is to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total file count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFiles<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.CountFiles<TSelf, TKey>();

    /// <summary>
    ///     Counts the number of files present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior file count is to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total file count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFiles<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.CountChildren<TSelf, TKey, FileFilter<TSelf>>();

    /// <summary>
    ///     Counts the number of directories present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior directory count is to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total directory count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDirectories<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.CountDirectories<TSelf, TKey>();

    /// <summary>
    ///     Counts the number of directories present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior directory count is to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total directory count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDirectories<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.CountChildren<TSelf, TKey, DirectoryFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateFilesBfs<TSelf, TKey>(this TSelf item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenBfs<TSelf, TKey, KeyedBoxFileFilter<TSelf, TKey>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateFilesBfs<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateFilesBfs<TSelf, TKey>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose directory children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateDirectoriesBfs<TSelf, TKey>(this TSelf item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenBfs<TSelf, TKey, KeyedBoxDirectoryFilter<TSelf, TKey>>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose directory children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateDirectoriesBfs<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateDirectoriesBfs<TSelf, TKey>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a depth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateFilesDfs<TSelf, TKey>(this TSelf item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenDfs<TSelf, TKey, KeyedBoxFileFilter<TSelf, TKey>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a depth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateFilesDfs<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateFilesDfs<TSelf, TKey>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose directory children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateDirectoriesDfs<TSelf, TKey>(this TSelf item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenDfs<TSelf, TKey, KeyedBoxDirectoryFilter<TSelf, TKey>>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose directory children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateDirectoriesDfs<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TKey : notnull
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateDirectoriesDfs<TSelf, TKey>();

    /// <summary>
    ///     Retrieves all directory-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose directory-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are directories.</returns>
    public static Box<TSelf>[] GetDirectories<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetDirectories();

    /// <summary>
    ///     Retrieves all directory-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose directory-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are directories.</returns>
    public static Box<TSelf>[] GetDirectories<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursive<TSelf, DirectoryFilter<TSelf>>();

    /// <summary>
    ///     Recursively retrieves all directory-type children of this boxed node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The boxed node whose directory-type children are to be retrieved.</param>
    /// <param name="directoriesSpan">The span to be populated with the directory-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetDirectoriesUnsafe<TSelf>(this Box<TSelf> item, Span<Box<TSelf>> directoriesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetDirectoriesUnsafe(directoriesSpan, ref index);

    /// <summary>
    ///     Recursively retrieves all directory-type children of this node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node whose directory-type children are to be retrieved.</param>
    /// <param name="directoriesSpan">The span to be populated with the directory-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetDirectoriesUnsafe<TSelf>(this TSelf item, Span<Box<TSelf>> directoriesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursiveUnsafe<TSelf, DirectoryFilter<TSelf>>(directoriesSpan, ref index);

    /// <summary>
    ///     Retrieves all file-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose file-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are files.</returns>
    public static Box<TSelf>[] GetFiles<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetFiles();

    /// <summary>
    ///     Retrieves all file-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose file-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are files.</returns>
    public static Box<TSelf>[] GetFiles<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursive<TSelf, FileFilter<TSelf>>();

    /// <summary>
    ///     Recursively retrieves all file-type children of this boxed node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The boxed node whose file-type children are to be retrieved.</param>
    /// <param name="filesSpan">The span to be populated with the file-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetFilesUnsafe<TSelf>(this Box<TSelf> item, Span<Box<TSelf>> filesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetFilesUnsafe(filesSpan, ref index);

    /// <summary>
    ///     Recursively retrieves all file-type children of this node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node whose file-type children are to be retrieved.</param>
    /// <param name="filesSpan">The span to be populated with the file-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetFilesUnsafe<TSelf>(this TSelf item, Span<Box<TSelf>> filesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursiveUnsafe<TSelf, FileFilter<TSelf>>(filesSpan, ref index);
}

/// <summary>
///     Trait methods for <see cref="IHaveBoxedChildren{TSelf}" />.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveAFileOrDirectoryExtensionsForIHaveBoxedChildren
{
    /// <summary>
    ///      Counts the number of files present under this node.
    /// </summary>
    /// <param name="item">The node (directory) whose interior file count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total file count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFiles<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.CountFiles();

    /// <summary>
    ///      Counts the number of files present under this node.
    /// </summary>
    /// <param name="item">The node (directory) whose interior file count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total file count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFiles<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.CountChildren<TSelf, FileFilter<TSelf>>();

    /// <summary>
    ///      Counts the number of directories present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior directory count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total directory count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDirectories<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.CountDirectories();

    /// <summary>
    ///      Counts the number of directories present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior directory count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total directory count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDirectories<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.CountChildren<TSelf, DirectoryFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesBfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenBfs<TSelf, BoxFileFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesBfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateFilesBfs();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose directory children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesBfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenBfs<TSelf, BoxDirectoryFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesBfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateDirectoriesBfs();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesDfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenDfs<TSelf, BoxFileFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesDfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateFilesDfs();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesDfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenDfs<TSelf, BoxDirectoryFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesDfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateDirectoriesDfs();

    /// <summary>
    /// Retrieves all directory-type children of this node with keys, recursively.
    /// </summary>
    /// <param name="item">The keyed boxed node whose directory-type children are to be retrieved.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of keyed boxed children that are directories.</returns>
    public static KeyedBox<TKey, TSelf>[] GetDirectories<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.GetDirectories<TSelf, TKey>();

    /// <summary>
    /// Retrieves all directory-type children of this node with keys, recursively.
    /// </summary>
    /// <param name="item">The keyed boxed node whose directory-type children are to be retrieved.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of keyed boxed children that are directories.</returns>
    public static KeyedBox<TKey, TSelf>[] GetDirectories<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.GetChildrenRecursive<TSelf, TKey, DirectoryFilter<TSelf>>();

    /// <summary>
    ///     Recursively retrieves all directory-type children of this node with keys and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node with keys whose directory-type children are to be retrieved.</param>
    /// <param name="directoriesSpan">The span to be populated with the directory-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetDirectoriesUnsafe<TSelf, TKey>(this KeyedBox<TKey, TSelf> item, Span<KeyedBox<TKey, TSelf>> directoriesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.GetDirectoriesUnsafe(directoriesSpan, ref index);

    /// <summary>
    ///     Recursively retrieves all directory-type children of this node with keys and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node with keys whose directory-type children are to be retrieved.</param>
    /// <param name="directoriesSpan">The span to be populated with the directory-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetDirectoriesUnsafe<TSelf, TKey>(this TSelf item, Span<KeyedBox<TKey, TSelf>> directoriesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.GetChildrenRecursiveUnsafe<TSelf, TKey, DirectoryFilter<TSelf>>(directoriesSpan, ref index);

    /// <summary>
    /// Retrieves all file-type children of this node with keys, recursively.
    /// </summary>
    /// <param name="item">The keyed boxed node whose file-type children are to be retrieved.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of keyed boxed children that are files.</returns>
    public static KeyedBox<TKey, TSelf>[] GetFiles<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.GetFiles<TSelf, TKey>();

    /// <summary>
    /// Retrieves all file-type children of this node with keys, recursively.
    /// </summary>
    /// <param name="item">The keyed boxed node whose file-type children are to be retrieved.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of keyed boxed children that are files.</returns>
    public static KeyedBox<TKey, TSelf>[] GetFiles<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.GetChildrenRecursive<TSelf, TKey, FileFilter<TSelf>>();

    /// <summary>
    ///     Recursively retrieves all file-type children of this node with keys and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node with keys whose file-type children are to be retrieved.</param>
    /// <param name="filesSpan">The span to be populated with the file-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetFilesUnsafe<TSelf, TKey>(this KeyedBox<TKey, TSelf> item, Span<KeyedBox<TKey, TSelf>> filesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.GetFilesUnsafe(filesSpan, ref index);

    /// <summary>
    ///     Recursively retrieves all file-type children of this node with keys and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node with keys whose file-type children are to be retrieved.</param>
    /// <param name="filesSpan">The span to be populated with the file-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetFilesUnsafe<TSelf, TKey>(this TSelf item, Span<KeyedBox<TKey, TSelf>> filesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>, IHaveAFileOrDirectory
        where TKey : notnull
        => item.GetChildrenRecursiveUnsafe<TSelf, TKey, FileFilter<TSelf>>(filesSpan, ref index);
}

/// <summary>
///     Trait methods for <see cref="IHaveObservableChildren{TSelf}" />.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveAFileOrDirectoryExtensionsForIHaveObservableChildren
{
    /// <summary>
    ///      Counts the number of files present under this node.
    /// </summary>
    /// <param name="item">The node (directory) whose interior file count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total file count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFiles<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.CountFiles();

    /// <summary>
    ///      Counts the number of files present under this node.
    /// </summary>
    /// <param name="item">The node (directory) whose interior file count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total file count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFiles<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.CountChildren<TSelf, FileFilter<TSelf>>();

    /// <summary>
    ///      Counts the number of directories present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior directory count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total directory count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDirectories<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.CountDirectories();

    /// <summary>
    ///      Counts the number of directories present under this node (directory).
    /// </summary>
    /// <param name="item">The node (directory) whose interior directory count is to be counted.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The total directory count under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDirectories<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.CountChildren<TSelf, DirectoryFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesBfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenBfs<TSelf, BoxFileFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesBfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateFilesBfs();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose directory children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a breadth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesBfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenBfs<TSelf, BoxDirectoryFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesBfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateDirectoriesBfs();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesDfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenDfs<TSelf, BoxFileFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all file child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are files, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateFilesDfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateFilesDfs();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesDfs<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.EnumerateChildrenDfs<TSelf, BoxDirectoryFilter<TSelf>>();

    /// <summary>
    ///     Enumerates all directory child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose file children are to be enumerated.</param>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node that are directories, enumerated in a depth-first manner.</returns>
    public static IEnumerable<Box<TSelf>> EnumerateDirectoriesDfs<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.EnumerateDirectoriesDfs();

    /// <summary>
    ///     Retrieves all directory-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose directory-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are directories.</returns>
    public static Box<TSelf>[] GetDirectories<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetDirectories();

    /// <summary>
    ///     Retrieves all directory-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose directory-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are directories.</returns>
    public static Box<TSelf>[] GetDirectories<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursive<TSelf, DirectoryFilter<TSelf>>();

    /// <summary>
    ///     Recursively retrieves all directory-type children of this boxed node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The boxed node whose directory-type children are to be retrieved.</param>
    /// <param name="directoriesSpan">The span to be populated with the directory-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetDirectoriesUnsafe<TSelf>(this Box<TSelf> item, Span<Box<TSelf>> directoriesSpan, ref int index)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetDirectoriesUnsafe(directoriesSpan, ref index);

    /// <summary>
    ///     Recursively retrieves all directory-type children of this node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node whose directory-type children are to be retrieved.</param>
    /// <param name="directoriesSpan">The span to be populated with the directory-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetDirectoriesUnsafe<TSelf>(this TSelf item, Span<Box<TSelf>> directoriesSpan, ref int index)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursiveUnsafe<TSelf, DirectoryFilter<TSelf>>(directoriesSpan, ref index);

    /// <summary>
    ///     Retrieves all file-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose file-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are files.</returns>
    public static Box<TSelf>[] GetFiles<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetFiles();

    /// <summary>
    ///     Retrieves all file-type children of this node, recursively.
    /// </summary>
    /// <param name="item">The boxed node whose file-type children are to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>An array of boxed children that are files.</returns>
    public static Box<TSelf>[] GetFiles<TSelf>(this TSelf item)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursive<TSelf, FileFilter<TSelf>>();

    /// <summary>
    ///     Recursively retrieves all file-type children of this boxed node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The boxed node whose file-type children are to be retrieved.</param>
    /// <param name="filesSpan">The span to be populated with the file-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetFilesUnsafe<TSelf>(this Box<TSelf> item, Span<Box<TSelf>> filesSpan, ref int index)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.Item.GetFilesUnsafe(filesSpan, ref index);

    /// <summary>
    ///     Recursively retrieves all file-type children of this node and populates them into the provided span.
    ///     This is an unsafe method and does not perform bounds checks on the provided span.
    /// </summary>
    /// <param name="item">The node whose file-type children are to be retrieved.</param>
    /// <param name="filesSpan">The span to be populated with the file-type children.</param>
    /// <param name="index">The current index in the span where the next child should be placed.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    public static void GetFilesUnsafe<TSelf>(this TSelf item, Span<Box<TSelf>> filesSpan, ref int index)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHaveAFileOrDirectory
        => item.GetChildrenRecursiveUnsafe<TSelf, FileFilter<TSelf>>(filesSpan, ref index);
}

/// <summary>
///     Trait methods for <see cref="IHaveAFileOrDirectory"/>.
/// </summary>
[ExcludeFromCodeCoverage] // Wrapper
// ReSharper disable once InconsistentNaming
public static class IHaveAFileOrDirectoryExtensions
{
    /// <summary>
    ///     Checks if the item in the keyed box is a file.
    /// </summary>
    /// <param name="item">The keyed box containing the item to check.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <returns>True if the item is a file; otherwise, false.</returns>
    public static bool IsFile<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Value.Item.IsFile;

    /// <summary>
    ///     Checks if the item in the keyed box is a file.
    /// </summary>
    /// <param name="item">The keyed box containing the item to check.</param>
    /// <returns>True if the item is a file; otherwise, false.</returns>
    public static bool IsFile<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.IsFile;

    /// <summary>
    ///     Checks if the item in the box is a file.
    /// </summary>
    /// <param name="item">The box containing the item to check.</param>
    /// <returns>True if the item is a file; otherwise, false.</returns>
    public static bool IsFile<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveAFileOrDirectory
        => item.Item.IsFile;

    /// <summary>
    ///     Checks if the item in the keyed box is a directory.
    /// </summary>
    /// <param name="item">The keyed box containing the item to check.</param>
    /// <returns>True if the item is a directory; otherwise, false.</returns>
    public static bool IsDirectory<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Value.Item.IsDirectory;

    /// <summary>
    ///     Checks if the item in the keyed box is a directory.
    /// </summary>
    /// <param name="item">The keyed box containing the item to check.</param>
    /// <returns>True if the item is a directory; otherwise, false.</returns>
    public static bool IsDirectory<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveAFileOrDirectory
        where TKey : notnull
        => item.Item.IsDirectory;

    /// <summary>
    ///     Checks if the item in the box is a directory.
    /// </summary>
    /// <param name="item">The box containing the item to check.</param>
    /// <returns>True if the item is a directory; otherwise, false.</returns>
    public static bool IsDirectory<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveAFileOrDirectory
        => item.Item.IsDirectory;
}

internal struct BoxFileFilter<TSelf> : IFilter<Box<TSelf>> where TSelf : struct, IHaveAFileOrDirectory
{
    public static bool Match(Box<TSelf> item) => item.Item.IsFile;
}

internal struct BoxDirectoryFilter<TSelf> : IFilter<Box<TSelf>> where TSelf : struct, IHaveAFileOrDirectory
{
    public static bool Match(Box<TSelf> item) => item.Item.IsDirectory;
}

internal struct KeyedBoxFileFilter<TSelf, TKey> : IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
    where TSelf : struct, IHaveAFileOrDirectory
    where TKey : notnull
{
    public static bool Match(KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item) => item.Value.Item.IsFile;
}

internal struct KeyedBoxDirectoryFilter<TSelf, TKey> : IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
    where TSelf : struct, IHaveAFileOrDirectory
    where TKey : notnull
{
    public static bool Match(KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item) => item.Value.Item.IsDirectory;
}

internal struct FileFilter<TSelf> : IFilter<TSelf>
    where TSelf : struct, IHaveAFileOrDirectory
{
    public static bool Match(TSelf item) => item.IsFile;
}

internal struct DirectoryFilter<TSelf> : IFilter<TSelf>
    where TSelf : struct, IHaveAFileOrDirectory
{
    public static bool Match(TSelf item) => item.IsDirectory;
}
