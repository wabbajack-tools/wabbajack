using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

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

        public static async Task<Result> ScanStream(Stream stream)
        {
            await using var file = new TempFile();
            await file.Path.WriteAllAsync(stream);

            var process = new ProcessHelper()
            {
                Path =
                    (AbsolutePath)@"C:\ProgramData\Microsoft\Windows Defender\Platform\4.18.2006.10-0\X86\MpCmdRun.exe",
                Arguments = new object[] {"-Scan", "-ScanType", "3", "-DisableRemediation", "-File", file.Path},
            };
            
            return (Result)await process.Start();
        }

        public static Task<bool> ShouldScan(AbsolutePath path)
        {
            
        }
    }
}
