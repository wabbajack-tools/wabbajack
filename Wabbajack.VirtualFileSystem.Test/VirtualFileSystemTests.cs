using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.VirtualFileSystem.Test
{
    public class VFSTests
    {
        private static readonly AbsolutePath VFS_TEST_DIR = "vfs_test_dir".ToPath().RelativeToEntryPoint();
        private static readonly AbsolutePath TEST_ZIP = "test.zip".RelativeTo(VFS_TEST_DIR);
        private static readonly AbsolutePath TEST_TXT = "test.txt".RelativeTo(VFS_TEST_DIR);
        private static readonly AbsolutePath ARCHIVE_TEST_TXT = "archive/test.txt".RelativeTo(VFS_TEST_DIR);
        private Context context;

        private readonly ITestOutputHelper _helper;
        private WorkQueue Queue { get; }

        public VFSTests(ITestOutputHelper helper)
        {
            _helper = helper;
            Utils.LogMessages.Subscribe(f => _helper.WriteLine(f.ShortDescription));
            Queue = new WorkQueue();
            context = new Context(Queue);
        }

        public static async Task<VFSTests> Factory(ITestOutputHelper helper)
        {
            await VFS_TEST_DIR.DeleteDirectory();
            VFS_TEST_DIR.CreateDirectory();
            return new VFSTests(helper);
        }

        [Fact]
        public async Task FilesAreIndexed()
        {
            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();

            var file = context.Index.ByRootPath["test.txt".ToPath().RelativeTo(VFS_TEST_DIR)];
            Assert.NotNull(file);

            Assert.Equal(14, file.Size);
            Assert.Equal(file.Hash, Hash.FromBase64("qX0GZvIaTKM="));
        }

        
        private async Task AddTestRoot()
        {
            await context.AddRoot(VFS_TEST_DIR);
            await context.WriteToFile("vfs_cache.bin".RelativeTo(VFS_TEST_DIR));
            await context.IntegrateFromFile( "vfs_cache.bin".RelativeTo(VFS_TEST_DIR));
        }


        [Fact]
        public async Task ArchiveContentsAreIndexed()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            await ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);
            await AddTestRoot();

            var absPath = "test.zip".RelativeTo(VFS_TEST_DIR);
            var file = context.Index.ByRootPath[absPath];
            Assert.NotNull(file);

            Assert.Equal(128, file.Size);
            Assert.Equal(absPath.FileHash(), file.Hash);

            Assert.True(file.IsArchive);
            var innerFile = file.Children.First();
            Assert.Equal(14, innerFile.Size);
            Assert.Equal(Hash.FromBase64("qX0GZvIaTKM="), innerFile.Hash);
            Assert.Same(file, file.Children.First().Parent);
        }
        

        [Fact]
        public async Task DuplicateFileHashes()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            await ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();


            var files = context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];
            Assert.Equal(2, files.Count());
        }

        [Fact]
        public async Task DeletedFilesAreRemoved()
        {
            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();

            var file = context.Index.ByRootPath[TEST_TXT];
            Assert.NotNull(file);

            Assert.Equal(14, file.Size);
            Assert.Equal(Hash.FromBase64("qX0GZvIaTKM="), file.Hash);

            TEST_TXT.Delete();

            await AddTestRoot();

            Assert.DoesNotContain(TEST_TXT, context.Index.AllFiles.Select(f => f.AbsoluteName));
        }

        [Fact]
        public async Task UnmodifiedFilesAreNotReIndexed()
        {
            await AddFile(TEST_TXT, "This is a test");
            await AddTestRoot();

            var old_file = context.Index.ByRootPath[TEST_TXT];
            var old_time = old_file.LastAnalyzed;

            await AddTestRoot();

            var new_file = context.Index.ByRootPath[TEST_TXT];

            Assert.Equal(old_time, new_file.LastAnalyzed);
        }

        [Fact]
        public async Task CanStageSimpleArchives()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            await ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);
            await AddTestRoot();

            var res = new FullPath(TEST_ZIP, new[] {(RelativePath)"test.txt"});
            var file = context.Index.ByFullPath[res];

            var cleanup = await context.Stage(new List<VirtualFile> {file});
            Assert.Equal("This is a test", await file.StagedPath.ReadAllTextAsync());

            await cleanup();
        }

        [Fact]
        public async Task CanStageNestedArchives()
        {
            await AddFile(ARCHIVE_TEST_TXT, "This is a test");
            await ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            var inner_dir = @"archive\other\dir".RelativeTo(VFS_TEST_DIR);
            inner_dir.CreateDirectory();
            TEST_ZIP.MoveTo( @"archive\other\dir\nested.zip".RelativeTo(VFS_TEST_DIR));
            await ZipUpFolder(ARCHIVE_TEST_TXT.Parent, TEST_ZIP);

            await AddTestRoot();

            var files = context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];

            var cleanup = await context.Stage(files);

            foreach (var file in files)
                Assert.Equal("This is a test", await file.StagedPath.ReadAllTextAsync());

            await cleanup();
        }

        private static async Task AddFile(AbsolutePath filename, string text)
        {
            filename.Parent.CreateDirectory();
            await filename.WriteAllTextAsync(text);
        }

        private static async Task ZipUpFolder(AbsolutePath folder, AbsolutePath output)
        {
            ZipFile.CreateFromDirectory((string)folder, (string)output);
            await folder.DeleteDirectory();
        }
    }
}
