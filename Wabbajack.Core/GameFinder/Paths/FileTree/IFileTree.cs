using System.Collections.Generic;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths.FileTree;

/// <summary>
/// Represents a generic tree of files.
/// </summary>
/// <typeparam name="TFileTree">Allows implementations to return concrete types</typeparam>
[PublicAPI]
public interface IFileTree<TFileTree> where TFileTree : IFileTree<TFileTree>
{
    /// <summary>
    /// The file name for this node.
    /// </summary>
    public RelativePath Name { get; }

    /// <summary>
    /// Returns true if node is assumed to be a file.
    /// </summary>
    public bool IsFile { get; }

    /// <summary>
    /// Returns true if node is assumed to be a directory.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Returns true if node is the root of the tree.
    /// </summary>
    public bool IsTreeRoot { get; }

    /// <summary>
    /// Returns tre if node has a parent.
    /// </summary>
    public bool HasParent { get; }

    /// <summary>
    /// A Dictionary containing all the children of this node, both files and directories.
    /// </summary>
    /// <remarks>
    /// The key is the <see cref="Name"/> of the child.
    /// </remarks>
    public IDictionary<RelativePath, TFileTree> Children { get; }

    /// <summary>
    /// Returns the parent node if it exists, throws InvalidOperationException otherwise.
    /// </summary>
    public TFileTree Parent { get; }

    /// <summary>
    /// Returns the root node of the tree.
    /// </summary>
    public TFileTree Root { get; }

    /// <summary>
    /// A collection of all sibling nodes, excluding this one.
    /// </summary>
    /// <remarks>
    /// Returns an empty collection if this node is the root.
    /// </remarks>
    public IEnumerable<TFileTree> GetSiblings();

    /// <summary>
    /// A collection of all File nodes that descend from this one.
    /// </summary>
    /// <remarks>
    /// Returns an empty collection if this node is a file.
    /// </remarks>
    public List<TFileTree> GetAllDescendentFiles();
}
