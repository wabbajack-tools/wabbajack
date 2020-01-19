using GraphQL.Types;
using Wabbajack.BuildServer.Models;

namespace Wabbajack.BuildServer.GraphQL
{
    public class UploadedFileType : ObjectGraphType<UploadedFile>
    {
        public UploadedFileType()
        {
            Name = "UploadedFile";
            Description = "A file uploaded for hosting on Wabbajack's static file hosting";
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Unique Id of the Job");
            Field(x => x.Name).Description("Non-unique name of the file");
            Field(x => x.MungedName, type: typeof(IdGraphType)).Description("Unique file name");
            Field(x => x.UploadDate, type: typeof(DateGraphType)).Description("Date of the file upload");
            Field(x => x.Uploader, type: typeof(IdGraphType)).Description("Uploader of the file");
            Field(x => x.Uri, type: typeof(UriGraphType)).Description("URI of the file");
            Field(x => x.Hash).Description("xxHash64 of the file");
            Field(x => x.Size).Description("Size of the file");
        }
    }
}
