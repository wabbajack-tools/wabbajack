using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using Wabbajack.BuildServer.Models;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.GraphQL
{
    public class ModListStatusType : ObjectGraphType<ModListStatus>
    {
        public ModListStatusType()
        {
            Name = "ModlistSummary";
            Description = "Short summary of a modlist status";
            Field(x => x.Id).Description("Name of the modlist");
            Field(x => x.Metadata.Title).Description("Human-friendly name of the modlist");
            Field<ListGraphType<ModListArchiveType>>("Archives",
                    arguments: new QueryArguments(new QueryArgument<ArchiveEnumFilterType>
                    {
                        Name = "filter", Description = "Type of archives to return"
                    }),
                    resolve: context =>
                    {
                        var arg = context.GetArgument<string>("filter");
                        var archives = (IEnumerable<DetailedStatusItem>)context.Source.DetailedStatus.Archives;
                        switch (arg)
                        {
                            case "FAILED":
                                archives = archives.Where(a => a.IsFailing);
                                break;
                            case "PASSED":
                                archives = archives.Where(a => !a.IsFailing);
                                break;
                            default:
                                break;
                        }

                        return archives;
                    });
        }
       
    }

    public class ModListArchiveType : ObjectGraphType<DetailedStatusItem>
    {
        public ModListArchiveType()
        {
            Field(x => x.IsFailing).Description("Is this archive failing validation?");
            Field(x => x.Archive.Name).Description("Name of the archive");
            Field(x => x.Archive.Hash).Description("Hash of the archive");
            Field(x => x.Archive.Size).Description("Size of the archive");
        }
    }

    public class ArchiveEnumFilterType : EnumerationGraphType
    {
        public ArchiveEnumFilterType()
        {
            Name = "ArchiveFilterEnum";
            Description = "What archives should be returned from a sublist";
            AddValue("ALL", "All archives are returned", "ALL");
            AddValue("FAILED", "All archives are returned", "FAILED");
            AddValue("PASSED", "All archives are returned", "PASSED");
            
        }
    }
}
