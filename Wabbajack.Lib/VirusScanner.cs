using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Bcpg;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;

namespace Wabbajack.Lib
{
    /// <summary>
    /// Wrapper around Windows Defender's commandline tool
    /// </summary>
    public class VirusScanner
    {
        public enum Result : int
        {
            NotMalware = 0,
            Malware = 2
        }

        public static async Task<(Hash, Result)> ScanStream(Stream stream)
        {
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            var hash = await ms.xxHashAsync();
            ms.Position = 0;
            
            await using var file = new TempFile();
            try
            {
                await file.Path.WriteAllAsync(ms);
            }
            catch (IOException ex)
            {
                // Was caught before we could fully scan the file due to real-time virus scans
                if (ex.Message.ToLowerInvariant().Contains("malware"))
                {
                    return (hash, Result.Malware);
                }
            }

            var process = new ProcessHelper
            {
                Path =
                    (AbsolutePath)@"C:\ProgramData\Microsoft\Windows Defender\Platform\4.18.2006.10-0\X86\MpCmdRun.exe",
                Arguments = new object[] {"-Scan", "-ScanType", "3", "-DisableRemediation", "-File", file.Path},
            };
            
            return (hash, (Result)await process.Start());
        }

        private static SignatureChecker ExecutableChecker = new SignatureChecker(Definitions.FileType.DLL, 
            Definitions.FileType.EXE, 
            Definitions.FileType.PIF, 
            Definitions.FileType.QXD, 
            Definitions.FileType.QTX,
            Definitions.FileType.DRV,
            Definitions.FileType.SYS, 
            Definitions.FileType.COM);

        public static async Task<bool> ShouldScan(AbsolutePath path)
        {
            return await ExecutableChecker.MatchesAsync(path) != null;
        }
    }
}
