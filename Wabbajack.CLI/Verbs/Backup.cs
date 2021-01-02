using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Common.IO;

namespace Wabbajack.CLI.Verbs
{
    
    [Verb("backup", HelpText = @"Copy all encrypted and personal info into a location for transferring to a new machine", Hidden = true)]
    public class Backup : AVerb
    {
        public static Dictionary<string, bool> Keys = new Dictionary<string, bool>()
        {
            {"bunnycdn", true},
            {"nexusapikey", true},
            {"nexus-cookies", true},
            {"x-metrics-key", true},
            {"author-api-key.txt", false}
        };
        
        [Option('o', "output", Required = true, HelpText = @"Output folder for the decrypted data")]
        public string Output { get; set; } = "";

        public AbsolutePath OutputPath => (AbsolutePath)Output;
        protected override async Task<ExitCode> Run()
        {
            foreach(var (name, encrypted) in Keys)
            {
                byte[] data;
                var src = name.RelativeTo(Consts.LocalAppDataPath);
                if (!src.Exists)
                {
                    Console.WriteLine($"{name} doesn't exist, skipping");
                    continue;
                }

                if (encrypted)
                {
                    data = await Utils.FromEncryptedData(name);
                }
                else
                {
                    data = await src.ReadAllBytesAsync();
                }

                await name.RelativeTo(OutputPath).WriteAllBytesAsync(data);
            }

            return ExitCode.Ok;
        }
    }
    
    [Verb("restore", HelpText = @"Copy all encrypted and personal info into a location for transferring to a new machine", Hidden = true)]
    public class Restore : AVerb
    {
        public static Dictionary<string, bool> Keys = new Dictionary<string, bool>()
        {
            {"bunnycdn", true},
            {"nexusapikey", true},
            {"nexus-cookies", true},
            {"x-metrics-key", true},
            {"author-api-key.txt", false}
        };
        
        [Option('i', "input", Required = true, HelpText = @"Input folder for the decrypted data")]
        public string Input { get; set; } = "";

        public AbsolutePath InputPath => (AbsolutePath)Input;
        protected override async Task<ExitCode> Run()
        {
            foreach(var (name, encrypted) in Keys)
            {
                var src = name.RelativeTo(InputPath);
                if (!src.Exists) 
                {
                    Console.WriteLine($"{name} doesn't exist, skipping");
                    continue;
                }

                var data = await src.ReadAllBytesAsync(); 

                if (encrypted)
                {
                    await data.ToEncryptedData(name);
                }
                else
                {
                    await name.RelativeTo(Consts.LocalAppDataPath).WriteAllBytesAsync(data);
                }
            }

            return ExitCode.Ok;
        }
    }
}
