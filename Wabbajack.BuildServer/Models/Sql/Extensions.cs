using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

namespace Wabbajack.BuildServer.Model.Models
{
    public static class Extensions
    {
        
        public static DataTable ToDataTable(this IEnumerable<IndexedFile> coll)
        {
            var ut = new DataTable("dbo.IndexedFileType");
            ut.Columns.Add("Hash", typeof(long));
            ut.Columns.Add("Sha256", typeof(byte[]));
            ut.Columns.Add("Sha1", typeof(byte[]));
            ut.Columns.Add("Md5", typeof(byte[]));
            ut.Columns.Add("Crc32", typeof(int));
            ut.Columns.Add("Size", typeof(long));

            foreach (var itm in coll) 
                ut.Rows.Add(itm.Hash, itm.Sha256, itm.Sha1, itm.Md5, itm.Crc32, itm.Size);

            return ut;
        }
        
        public static DataTable ToDataTable(this IEnumerable<ArchiveContent> coll)
        {
            var ut = new DataTable("dbo.ArchiveContentType");
            ut.Columns.Add("Parent", typeof(long));
            ut.Columns.Add("Child", typeof(long));
            ut.Columns.Add("Path", typeof(string));

            foreach (var itm in coll) 
                ut.Rows.Add(itm.Parent, itm.Child, itm.Path);

            return ut;
        }
    }
}
