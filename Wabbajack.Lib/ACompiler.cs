using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public abstract class ACompiler
    {
        public string ModListName, ModListAuthor, ModListDescription, ModListImage, ModListWebsite, ModListReadme;
        public string WabbajackVersion;

        public StatusUpdateTracker UpdateTracker { get; protected set; }

        public WorkQueue Queue { get; protected set; }

        protected static string _vfsCacheName = "vfs_compile_cache.bin";
        /// <summary>
        /// A stream of tuples of ("Update Title", 0.25) which represent the name of the current task
        /// and the current progress.
        /// </summary>
        public IObservable<(string, float)> ProgressUpdates => _progressUpdates;
        protected readonly Subject<(string, float)> _progressUpdates = new Subject<(string, float)>();

        public Context VFS { get; internal set; }

        public ModManager ModManager;

        public string GamePath;

        public string ModListOutputFolder;
        public string ModListOutputFile;

        public List<Archive> SelectedArchives = new List<Archive>();
        public List<Directive> InstallDirectives = new List<Directive>();
        public List<RawSourceFile> AllFiles = new List<RawSourceFile>();
        public ModList ModList = new ModList();

        public List<IndexedArchive> IndexedArchives = new List<IndexedArchive>();
        public Dictionary<string, IEnumerable<VirtualFile>> IndexedFiles = new Dictionary<string, IEnumerable<VirtualFile>>();

        public abstract void Info(string msg);
        public abstract void Status(string msg);
        public abstract void Error(string msg);

        internal abstract string IncludeFile(byte[] data);
        internal abstract string IncludeFile(string data);

        public abstract bool Compile();

        public abstract Directive RunStack(IEnumerable<ICompilationStep> stack, RawSourceFile source);
        public abstract IEnumerable<ICompilationStep> GetStack();
        public abstract IEnumerable<ICompilationStep> MakeStack();

        protected ACompiler()
        {
            ProgressUpdates.Subscribe(itm => Utils.Log($"{itm.Item2} - {itm.Item1}"));
            Queue = new WorkQueue();
        }
    }
}
