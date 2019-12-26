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

using System;
using System.IO;

namespace OMODExtraction
{
    public static class Framework
    {
        /// <summary>
        /// Temp folder used for extraction. Default is %temp%\\OMODFramework\\
        /// </summary>
        public static string TempDir { get; set; } = Path.Combine(Path.GetTempPath(), "OMODFramework");

        /// <summary>
        /// DO NOT TOUCH UNLESS TOLD TO
        /// </summary>
        public static int MaxMemoryStreamSize => 67108864;

        public class OMODFrameworkException : ApplicationException
        {
            public OMODFrameworkException(string s) : base(s) { }
        }

        /// <summary>
        /// Convenience function that will clean the entire temp folder for you 
        /// </summary>
        /// <param name="deleteRoot">Whether to delete the folder itself</param>
        public static void CleanTempDir(bool deleteRoot = false)
        {
            if (!Directory.Exists(TempDir))
                return;

            var dInfo = new DirectoryInfo(TempDir);
            dInfo.GetFiles().Do(f =>
            {
                if (!f.Exists || f.IsReadOnly)
                    return;

                try
                {
                    f.Delete();
                }
                catch
                {
                    // ignored
                }

            });
            dInfo.GetDirectories().Do(d => { if (d.Exists && !d.Attributes.HasFlag(FileAttributes.ReadOnly)) d.Delete(true); });

            if (deleteRoot)
                Directory.Delete(TempDir);
        }
    }
}
