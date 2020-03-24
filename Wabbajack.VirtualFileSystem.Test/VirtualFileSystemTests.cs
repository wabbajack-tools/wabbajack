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
        private static readonly AbsolutePath VFS_TEST_DIR = "vfs_test_dir".ToPath().RelativeToEntryPoint();
        private static readonly AbsolutePath TEST_ZIP = "test.zip".RelativeTo(VFS_TEST_DIR);
        private static readonly AbsolutePath TEST_TXT = "test.txt".RelativeTo(VFS_TEST_DIR);
        private static readonly AbsolutePath ARCHIVE_TEST_TXT = "archive/text.txt".RelativeTo(VFS_TEST_DIR);
        private Context context;

        public TestContext TestContext { get; set; }
        public WorkQueue Queue { get; set; }

        [TestInitialize]
        public void Setup()
        {
            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f.ShortDescription));
            VFS_TEST_DIR.DeleteDirectory();
            VFS_TEST_DIR.CreateDirectory();
            Queue = new WorkQueue();
            context = new Context(Queue);
        }

        [TestMethod]
        public async Task FilesAreIndexed()
        {
            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();

            var file = context.Index.ByRootPath["test.txt".ToPath().RelativeTo(VFS_TEST_DIR)];
            Assert.IsNotNull(file);

            Assert.AreEqual(file.Size, 14);
            Assert.AreEqual(file.Hash, "qX0GZvIaTKM=");
        }

        private async Task AddTestRoot()
        {
            await context.AddRoot(VFS_TEST_DIR);
            await context.WriteToFile("vfs_cache.bin".RelativeTo(VFS_TEST_DIR));
            await context.IntegrateFromFile( "vfs_cache.bin".RelativeTo(VFS_TEST_DIR));
        }


        [TestMethod]
        public async Task ArchiveContentsAreIndexed()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);
            await AddTestRoot();

            var absPath = "test.zip".RelativeTo(VFS_TEST_DIR);
            var file = context.Index.ByRootPath[absPath];
            Assert.IsNotNull(file);

            Assert.AreEqual(128, file.Size);
            Assert.AreEqual(absPath.FileHash(), file.Hash);

            Assert.IsTrue(file.IsArchive);
            var innerFile = file.Children.First();
            Assert.AreEqual(14, innerFile.Size);
            Assert.AreEqual("qX0GZvIaTKM=", innerFile.Hash);
            Assert.AreSame(file, file.Children.First().Parent);
        }

        [TestMethod]
        public async Task DuplicateFileHashes()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();


            var files = context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];
            Assert.AreEqual(files.Count(), 2);
        }

        [TestMethod]
        public async Task DeletedFilesAreRemoved()
        {
            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();

            var file = context.Index.ByRootPath[TEST_TXT];
            Assert.IsNotNull(file);

            Assert.AreEqual(file.Size, 14);
            Assert.AreEqual(file.Hash, "qX0GZvIaTKM=");

            TEST_TXT.Delete();

            await AddTestRoot();

            CollectionAssert.DoesNotContain(context.Index.ByFullPath, TEST_TXT);
        }

        [TestMethod]
        public async Task UnmodifiedFilesAreNotReIndexed()
        {
            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();

            var old_file = context.Index.ByRootPath[TEST_TXT];
            var old_time = old_file.LastAnalyzed;

            await AddTestRoot();

            var new_file = context.Index.ByRootPath[TEST_TXT];

            Assert.AreEqual(old_time, new_file.LastAnalyzed);
        }

        [TestMethod]
        public async Task CanStageSimpleArchives()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);
            await AddTestRoot();

            var file = context.Index.ByFullPath[new FullPath(TEST_ZIP, new []{(RelativePath)"test.txt"})];

            var cleanup = await context.Stage(new List<VirtualFile> {file});
            Assert.AreEqual("This is a test", await file.StagedPath.ReadAllTextAsync());

            cleanup();
        }

        [TestMethod]
        public async Task CanStageNestedArchives()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            var inner_dir = @"archive\other\dir".RelativeTo(VFS_TEST_DIR);
            inner_dir.CreateDirectory();
            TEST_ZIP.MoveTo( @"archive\other\dir\nested.zip".RelativeTo(VFS_TEST_DIR));
            ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            await AddTestRoot();

            var files = context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];

            var cleanup = await context.Stage(files);

            foreach (var file in files)
                Assert.AreEqual("This is a test", await file.StagedPath.ReadAllTextAsync());

            cleanup();
        }

        [TestMethod]
        public async Task CanRequestPortableFileTrees()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            @"archive\other\dir".RelativeTo(VFS_TEST_DIR).CreateDirectory();
            TEST_ZIP.MoveTo(@"archive\other\dir\nested.zip".RelativeTo(VFS_TEST_DIR));
            ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            await AddTestRoot();

            var files = context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];
            var archive = context.Index.ByRootPath[TEST_ZIP];

            var state = context.GetPortableState(files);

            var newContext = new Context(Queue);

            await newContext.IntegrateFromPortable(state,
                new Dictionary<Hash, AbsolutePath> {{archive.Hash, archive.FullPath.Base}});

            var newFiles = newContext.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];

            var close = await newContext.Stage(newFiles);

            foreach (var file in newFiles)
                Assert.AreEqual("This is a test", await file.StagedPath.ReadAllTextAsync());

            close();
        }

        private static async Task AddFile(AbsolutePath filename, string text)
        {
            filename.Parent.CreateDirectory();
            await filename.WriteAllTextAsync(text);
        }

        private static void ZipUpFolder(AbsolutePath folder, AbsolutePath output)
        {
            ZipFile.CreateFromDirectory((string)folder, (string)output);
            folder.DeleteDirectory();
        }
    }
}
