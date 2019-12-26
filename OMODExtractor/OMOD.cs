/*
    Copyright (C) 2019  erri120

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

/*
 * This file contains parts of the Oblivion Mod Manager licensed under GPLv2
 * and has been modified for use in this OMODFramework
 * Original source: https://www.nexusmods.com/oblivion/mods/2097
 * GPLv2: https://opensource.org/licenses/gpl-2.0.php
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;

namespace OMODExtraction
{
    public class OMOD
    {
        protected class PrivateData
        {
            internal ZipFile ModFile;
        }

        private PrivateData _pD = new PrivateData();

        public readonly string FilePath;
        public readonly string FileName;
        public readonly string LowerFileName;
        public string FullFilePath => Path.Combine(FilePath, FileName);
        public readonly CompressionType Compression;

        private ZipFile ModFile
        {
            get
            {
                if (_pD.ModFile != null) return _pD.ModFile;
                _pD.ModFile = new ZipFile(FullFilePath);
                return _pD.ModFile;
            }
        }


        public OMOD(string path)
        {
            if (!File.Exists(path))
                throw new Framework.OMODFrameworkException($"The provided file at {path} does not exists!");

            FilePath = Path.GetDirectoryName(path);
            FileName = Path.GetFileName(path);
            LowerFileName = FileName?.ToLower();

            using (var configStream = ExtractWholeFile("config"))
            {
                if (configStream == null)
                    throw new Framework.OMODFrameworkException($"Could not find the configuration data for {FileName} !");
                using (var br = new BinaryReader(configStream))
                {
                    var fileVersion = br.ReadByte();
                    br.ReadString(); //mod name
                    br.ReadInt32(); // major version
                    br.ReadInt32(); // minor version
                    br.ReadString(); // author
                    br.ReadString(); // email
                    br.ReadString(); // website
                    br.ReadString(); // description
                    if (fileVersion >= 2)
                        br.ReadInt64();
                    else
                        br.ReadString();

                    Compression = (CompressionType)br.ReadByte();
                }
            }

            Close();
        }

        public void Close()
        {
            _pD.ModFile?.Close();
            _pD.ModFile = null;
        }

        public string ExtractPlugins()
        {
            return ParseCompressedStream("plugins.crc", "plugins");
        }

        public string ExtractDataFiles()
        {
            return ParseCompressedStream("data.crc", "data");
        }

        private string ParseCompressedStream(string dataInfo, string dataCompressed)
        {
            var infoStream = ExtractWholeFile(dataInfo);
            if (infoStream == null) return null;

            var compressedStream = ExtractWholeFile(dataCompressed);
            var path = CompressionHandler.DecompressFiles(infoStream, compressedStream, Compression);

            infoStream.Close();
            compressedStream.Close();

            return path;
        }

        private Stream ExtractWholeFile(string s)
        {
            string s2 = null;
            return ExtractWholeFile(s, ref s2);
        }

        private Stream ExtractWholeFile(string s, ref string path)
        {
            var ze = ModFile.GetEntry(s);
            return ze == null ? null : ExtractWholeFile(ze, ref path);
        }

        private Stream ExtractWholeFile(ZipEntry ze, ref string path)
        {
            var file = ModFile.GetInputStream(ze);
            Stream tempStream;

            if (path != null || ze.Size > Framework.MaxMemoryStreamSize)
                tempStream = Utils.CreateTempFile(out path);
            else
                tempStream = new MemoryStream((int)ze.Size);

            byte[] buffer = new byte[4096];
            int i;

            while ((i = file.Read(buffer, 0, 4096)) > 0)
                tempStream.Write(buffer, 0, i);

            tempStream.Position = 0;
            return tempStream;
        }
    }
}
