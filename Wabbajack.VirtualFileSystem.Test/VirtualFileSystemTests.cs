using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.Test
{
    [TestClass]
    public class VFSTests
    {
        private static AbsolutePath VFS_TEST_DIR = ((RelativePath)"vfs_test_dir").RelativeToEntryPoint();
        private Context context;

        public TestContext TestContext { get; set; }
        public WorkQueue Queue { get; set; }

        [TestInitialize]
        public async Task Setup()
        {
            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f.ShortDescription));
            if (VFS_TEST_DIR.IsDirectory)
                await Utils.DeleteDirectory(VFS_TEST_DIR);
            VFS_TEST_DIR.CreateDirectory();
            Queue = new WorkQueue();
            context = new Context(Queue);
        }

        [TestMethod]
        public async Task FilesAreIndexed()
        {
            AddFile("test.txt", "This is a test");
            await AddTestRoot();

            var file = context.Index.ByFullPath[Path.Combine((string)VFS_TEST_DIR, "test.txt")];
            Assert.IsNotNull(file);

            Assert.AreEqual(file.Size, 14);
            Assert.AreEqual(file.Hash, "qX0GZvIaTKM=");
        }

        private async Task AddTestRoot()
        {
            await context.AddRoot((string)VFS_TEST_DIR);
            await context.WriteToFile(Path.Combine((string)VFS_TEST_DIR, "vfs_cache.bin"));
            await context.IntegrateFromFile(Path.Combine((string)VFS_TEST_DIR, "vfs_cache.bin"));
        }


        [TestMethod]
        public async Task ArchiveContentsAreIndexed()
        {
            AddFile("archive/test.txt", "This is a test");
            await ZipUpFolder("archive", "test.zip");
            await AddTestRoot();

            var abs_path = Path.Combine((string)VFS_TEST_DIR, "test.zip");
            var file = context.Index.ByFullPath[abs_path];
            Assert.IsNotNull(file);

            Assert.AreEqual(128, file.Size);
            Assert.AreEqual(abs_path.FileHash(), file.Hash);

            Assert.IsTrue(file.IsArchive);
            var inner_file = file.Children.First();
            Assert.AreEqual(14, inner_file.Size);
            Assert.AreEqual("qX0GZvIaTKM=", inner_file.Hash);
            Assert.AreSame(file, file.Children.First().Parent);
        }

        [TestMethod]
        public async Task DuplicateFileHashes()
        {
            AddFile("archive/test.txt", "This is a test");
            await ZipUpFolder("archive", "test.zip");

            AddFile("test.txt", "This is a test");
            await AddTestRoot();


            var files = context.Index.ByHash["qX0GZvIaTKM="];
            Assert.AreEqual(files.Count(), 2);
        }

        [TestMethod]
        public async Task DeletedFilesAreRemoved()
        {
            AddFile("test.txt", "This is a test");
            await AddTestRoot();

            var file = context.Index.ByFullPath[Path.Combine((string)VFS_TEST_DIR, "test.txt")];
            Assert.IsNotNull(file);

            Assert.AreEqual(file.Size, 14);
            Assert.AreEqual(file.Hash, "qX0GZvIaTKM=");

            File.Delete(Path.Combine((string)VFS_TEST_DIR, "test.txt"));

            await AddTestRoot();

            CollectionAssert.DoesNotContain(context.Index.ByFullPath, Path.Combine((string)VFS_TEST_DIR, "test.txt"));
        }

        [TestMethod]
        public async Task UnmodifiedFilesAreNotReIndexed()
        {
            AddFile("test.txt", "This is a test");
            await AddTestRoot();

            var old_file = context.Index.ByFullPath[Path.Combine((string)VFS_TEST_DIR, "test.txt")];
            var old_time = old_file.LastAnalyzed;

            await AddTestRoot();

            var new_file = context.Index.ByFullPath[Path.Combine((string)VFS_TEST_DIR, "test.txt")];

            Assert.AreEqual(old_time, new_file.LastAnalyzed);
        }

        [TestMethod]
        public async Task CanStageSimpleArchives()
        {
            AddFile("archive/test.txt", "This is a test");
            await ZipUpFolder("archive", "test.zip");
            await AddTestRoot();

            var abs_path = Path.Combine((string)VFS_TEST_DIR, "test.zip");
            var file = context.Index.ByFullPath[abs_path + "|test.txt"];

            var cleanup = await context.Stage(new List<VirtualFile> {file});
            Assert.AreEqual("This is a test", File.ReadAllText(file.StagedPath));

            cleanup();
        }

        [TestMethod]
        public async Task CanStageNestedArchives()
        {
            AddFile("archive/test.txt", "This is a test");
            await ZipUpFolder("archive", "test.zip");

            Directory.CreateDirectory(Path.Combine((string)VFS_TEST_DIR, @"archive\other\dir"));
            File.Move(Path.Combine((string)VFS_TEST_DIR, "test.zip"),
                Path.Combine((string)VFS_TEST_DIR, @"archive\other\dir\nested.zip"));
            await ZipUpFolder("archive", "test.zip");

            await AddTestRoot();

            var files = context.Index.ByHash["qX0GZvIaTKM="];

            var cleanup = await context.Stage(files);

            foreach (var file in files)
                Assert.AreEqual("This is a test", File.ReadAllText(file.StagedPath));

            cleanup();
        }

        [TestMethod]
        public async Task CanRequestPortableFileTrees()
        {
            AddFile("archive/test.txt", "This is a test");
            await ZipUpFolder("archive", "test.zip");

            Directory.CreateDirectory(Path.Combine((string)VFS_TEST_DIR, @"archive\other\dir"));
            File.Move(Path.Combine((string)VFS_TEST_DIR, "test.zip"),
                Path.Combine((string)VFS_TEST_DIR, @"archive\other\dir\nested.zip"));
            await ZipUpFolder("archive", "test.zip");

            await AddTestRoot();

            var files = context.Index.ByHash["qX0GZvIaTKM="];
            var archive = context.Index.ByRootPath[Path.Combine((string)VFS_TEST_DIR, "test.zip")];

            var state = context.GetPortableState(files);

            var new_context = new Context(Queue);

            await new_context.IntegrateFromPortable(state,
                new Dictionary<string, string> {{archive.Hash, archive.FullPath}});

            var new_files = new_context.Index.ByHash["qX0GZvIaTKM="];

            var close = await new_context.Stage(new_files);

            foreach (var file in new_files)
                Assert.AreEqual("This is a test", File.ReadAllText(file.StagedPath));

            close();
        }

        private static void AddFile(string filename, string thisIsATest)
        {
            var fullpath = Path.Combine((string)VFS_TEST_DIR, filename);
            if (!Directory.Exists(Path.GetDirectoryName(fullpath)))
                Directory.CreateDirectory(Path.GetDirectoryName(fullpath));
            File.WriteAllText(fullpath, thisIsATest);
        }

        private static async Task ZipUpFolder(string folder, string output)
        {
            var path = Path.Combine((string)VFS_TEST_DIR, folder);
            ZipFile.CreateFromDirectory(path, Path.Combine((string)VFS_TEST_DIR, output));
            await Utils.DeleteDirectory((AbsolutePath)path);
        }
    }
}
