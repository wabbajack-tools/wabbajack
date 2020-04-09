using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using F23.StringSimilarity;
using Wabbajack.Common;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.CLI.Verbs
{
    [Verb("find-similar", HelpText = "Finds duplicate downloads")]
    public class FindSimilar : AVerb
    {
        [IsDirectory(CustomMessage = "Downloads folder at %1 does not exist!")]
        [Option('i', "input", HelpText = "Downloads folder", Required = true)]
        public string? DownloadsFolder { get; set; }

        [Option('t', "threshold", HelpText = "Set the threshold for the maximum distance", Default = 0.2, Required = false)]
        public double Threshold { get; set; }

        protected override async Task<ExitCode> Run()
        {
            var downloads = Directory.EnumerateFiles(DownloadsFolder, "*", SearchOption.TopDirectoryOnly)
                .Where(x => Consts.SupportedArchives.Contains(Path.GetExtension(x)))
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();

            var similar = downloads
                .Select(x =>
            {
                var pair = new KeyValuePair<string, CompareStruct>(x, downloads
                    .Where(y => y != x)
                    .Select(y =>
                    {
                        var lcs = new MetricLCS();
                        var distance = lcs.Distance(x, y);
                        return new CompareStruct(y, distance);
                    })
                    .Aggregate((smallest, next) => smallest.Distance < next.Distance ? smallest : next));
                return pair;
            })
                .DistinctBy(x => x.Key)
                .DistinctBy(x => x.Value.Distance)
                .Where(x => x.Value.Distance <= Threshold)
                .ToList();

            CLIUtils.Log($"Found {similar.Count} similar files:");

            similar.Do(f =>
            {
                var (key, value) = f;
                CLIUtils.Log($"{key} similar to {value.Name} by {Math.Round(value.Distance, 3)}");
            });

            return ExitCode.Ok;
        }

        internal struct CompareStruct
        {
            public string Name;
            public double Distance;

            public CompareStruct(string name, double distance)
            {
                Name = name;
                Distance = distance;
            }
        } 
    }
}
