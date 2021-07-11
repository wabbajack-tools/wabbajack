namespace Wabbajack.Core.IO
{
    public interface IPath
    {
        /// <summary>
        /// Get the final file name, for <c>c:\bar\baz</c> this is <c>baz</c> for <c>c:\bar.zip</c> this is <c>bar.zip</c>
        /// for <c>bar.zip</c> this is <c>bar.zip</c>.
        /// </summary>
        public RelativePath FileName { get; }
    }
}
