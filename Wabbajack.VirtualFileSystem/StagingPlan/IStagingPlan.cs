using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.VirtualFileSystem.SevenZipExtractor;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public interface IStagingPlan : IAsyncDisposable
    {
         AbsolutePath Destination { get; }
    }

    public static class StagingPlan
    {

        public static IEnumerable<IStagingPlan> CreatePlan(IEnumerable<(VirtualFile src, AbsolutePath dest)> directives)
        {
            var top = new VirtualFile();
            var original = directives.ToHashSet();
            var files = directives.GroupBy(f => f.src).ToDictionary(f => f.Key);

            var childrenForParent = files.Keys.GroupBy(f => f.Parent ?? top).ToDictionary(f => f.Key);

            var allFilesForParent = files.Select(f => f.Key)
                .SelectMany(f => f.FilesInFullPath)
                .Distinct()
                .GroupBy(f => f.Parent ?? top)
                .ToDictionary(f => f.Key, f => f.ToArray());

            var baseDirectives = new List<IStagingPlan>();

            // Recursive internal function to get the plans for a given parent, we'll then start at the 
            // null parent (the top) and recurse our way down into all the children files.
            IEnumerable<IStagingPlan> GetPlans(VirtualFile parent)
            {
                foreach (var forParent in allFilesForParent[parent])
                {
                    // Do we need files inside this file?
                    if (childrenForParent.TryGetValue(forParent, out var children))
                    {
                        if (files.TryGetValue(forParent, out var copies))
                        {
                            ASubStage subStage;
                            if (parent == top)
                            {
                                subStage = new NativeArchive(forParent.AbsoluteName, GetPlans(forParent));
                            }
                            else
                            {
                                subStage = new SubStage(forParent.RelativeName, copies.First().dest, GetPlans(forParent));
                            }
                            yield return subStage;
                            foreach (var copy in copies)
                            {
                                yield return new DuplicateTo(subStage, copy.dest);
                            }
                        }
                        else
                        {
                            if (parent == top)
                            {
                                yield return new NativeArchive(forParent.AbsoluteName, GetPlans(forParent));
                            }
                            else
                            {
                                yield return new TempSubStage(forParent.RelativeName, GetPlans(forParent));
                            }
                        }
                    }
                    else
                    {
                        // If not then we need to copy this file around
                        var copies = files[forParent];
                        var firstCopy = new CopyTo(copies.Key.Name, copies.First().dest);
                        yield return firstCopy;
                        
                        foreach (var duplicate in copies.Skip(1))
                        {
                            yield return new DuplicateTo(firstCopy, duplicate.dest);
                        }
                    }
                }
            }

            return GetPlans(top);
        }
        public static async ValueTask ExecutePlan(WorkQueue queue, Func<ValueTask<Stream>> src, IEnumerable<IStagingPlan> plans)
        {
            // Extract these files

            await using var stream = await src();
            var sig = await FileExtractor.ArchiveSigs.MatchesAsync(stream);
            stream.Position = 0;

            switch (sig)
            {
                case Definitions.FileType.ZIP:
                    await ExtractWith7Zip((Definitions.FileType)sig, stream, plans);

                    
                    break;
                
                default:
                    throw new Exception($"Invalid archive for extraction");
            }
            
            
            // Copy around the duplicates
            foreach (var file in plans.OfType<DuplicateTo>())
            {
                await file.Execute();
            }
            
            // Execute the sub-stages
            foreach (var subStage in plans.OfType<ISubStage>())
            {
                await subStage.Execute(queue);
            }

            // Dispose of all plans
            foreach (var file in plans)
            {
                await file.DisposeAsync();
            }

        }

        private static async ValueTask ExtractWith7Zip(Definitions.FileType sig, Stream stream, IEnumerable<IStagingPlan> plans)
        {
            using var archive = await ArchiveFile.Open(stream, sig);

            void HandleFile(RelativePath path, IStagingPlan src, Func<Stream> sf)
            {
                
            }
            
            var extractor = new TypedExtractor<IStagingPlan>(plans.OfType<IStagingSrc>().ToDictionary(s => s.Source.FileName, s => (IStagingPlan)s),
                HandleFile);
            extractor.Extract(archive);

        }


        public static async Task ExecutePlans(WorkQueue queue, IEnumerable<IStagingPlan> plans)
        {
            foreach (var file in plans.OfType<NativeArchive>())
            {
                await file.Execute(queue);
            }
        }
    }
}
