namespace Wabbajack.Common
{
    public interface IPath
    {
        /// <summary>
        ///     Get the final file name, for c:\bar\baz this is `baz` for c:\bar.zip this is `bar.zip`
        ///     for `bar.zip` this is `bar.zip`
        /// </summary>
        public RelativePath FileName { get; }
    }

}
