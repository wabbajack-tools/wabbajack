using System.Collections.Generic;
using Wabbajack.Common.FileSignatures;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class SignatureTests
    {
        [Fact]
        public async void CanMatchSignatures()
        {
            await using var tempFile = new TempFile();

            var sig = new byte[] {0x00, 0x01, 0x00, 0x00, 0x00};

            await tempFile.Path.WriteAllBytesAsync(sig);

            var list = new List<Definitions.FileType>
            {
                Definitions.FileType.TTF, Definitions.FileType.ABA, Definitions.FileType.ACCDB
            };

            var checker = new SignatureChecker(list.ToArray());

            var res = await checker.MatchesAsync(tempFile.Path);
            
            Assert.NotNull(res);
            Assert.Equal(Definitions.FileType.TTF, res);
        }

        [Fact]
        public async void CanMatchCorrectSignature()
        {
            await using var tempFile = new TempFile();

            var sig = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00 };

            await tempFile.Path.WriteAllBytesAsync(sig);

            var list = new List<Definitions.FileType>
            {
                Definitions.FileType.TES3,
                Definitions.FileType.TTF,
            };

            var checker = new SignatureChecker(list.ToArray());

            var res = await checker.MatchesAsync(tempFile.Path);

            Assert.NotNull(res);
            Assert.Equal(Definitions.FileType.TTF, res);
        }
    }
}
