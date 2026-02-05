using System;
using System.IO;
using System.Threading;
using Wabbajack.Common;

namespace Wabbajack.Compression.BSA.TES5Archive;

internal class FileNameBlock
{
    public readonly Lazy<ReadOnlyMemorySlice<byte>[]> Names;

    public FileNameBlock(Reader bsa, long position)
    {
        Names = new Lazy<ReadOnlyMemorySlice<byte>[]>(
            mode: LazyThreadSafetyMode.ExecutionAndPublication,
            valueFactory: () =>
            {
                using var stream = bsa.GetStream();
                stream.BaseStream.Position = position;
                ReadOnlyMemorySlice<byte> data = stream.ReadBytes(checked((int) bsa._totalFileNameLength));
                var names = new ReadOnlyMemorySlice<byte>[bsa._fileCount];
                for (var i = 0; i < bsa._fileCount; i++)
                {
                    var index = data.Span.IndexOf(default(byte));
                    if (index == -1) throw new InvalidDataException("Did not end all of its strings in null bytes");
                    names[i] = data.Slice(0, index + 1);
                    var str = names[i].ReadStringTerm(bsa.HeaderType);
                    data = data.Slice(index + 1);
                }

                // Data doesn't seem to need to be fully consumed.
                // Official BSAs have overflow of zeros
                return names;
            });
    }
}