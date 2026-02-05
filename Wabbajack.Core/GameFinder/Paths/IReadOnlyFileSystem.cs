namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// Marker interface to indicate a file system or provider is read-only.
/// Implementors should throw on any write operation.
/// </summary>
public interface IReadOnlyFileSystem { }
