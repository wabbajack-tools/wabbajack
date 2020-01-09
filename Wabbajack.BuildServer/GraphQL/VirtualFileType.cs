using GraphQL.Types;
using Wabbajack.BuildServer.Models;

namespace Wabbajack.BuildServer.GraphQL
{
    public class VirtualFileType : ObjectGraphType<IndexedFileWithChildren>
    {
        public VirtualFileType()
        {
            Name = "VirtualFile";
            Field(x => x.Hash, type: typeof(IdGraphType)).Description("xxHash64 of the file, in Base64 encoding");
            Field(x => x.Size, type: typeof(LongGraphType)).Description("Size of the file");
            Field(x => x.IsArchive).Description("True if this file is an archive (BSA, zip, 7z, etc.)");
            Field(x => x.SHA256).Description("SHA256 hash of the file, in hexidecimal encoding");
            Field(x => x.SHA1).Description("SHA1 hash of the file, in hexidecimal encoding");
            Field(x => x.MD5).Description("MD5 hash of the file, in hexidecimal encoding");
            Field(x => x.CRC).Description("CRC32 hash of the file, in hexidecimal encoding");
            Field(x => x.Children, type: typeof(ChildFileType)).Description("Metadata for the files in this archive (if any)");
        }
    }

    public class ChildFileType : ObjectGraphType<ChildFile>
    {
        public ChildFileType()
        {
            Name = "ChildFile";
            Field(x => x.Name).Description("The relative path to the file inside the parent archive");
            Field(x => x.Hash).Description("The hash (xxHash64, Base64 ecoded) of the child file");
            Field(x => x.Extension).Description("File extension of the child file");
        }
    }
}
