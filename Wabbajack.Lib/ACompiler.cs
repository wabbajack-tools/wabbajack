using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public abstract class ACompiler
    {
        public Context VFS { get; internal set; } = new Context();
        public ModManager ModManager;

        public string GamePath;

        public string ModListOutputFolder;
        public string ModListOutputFile;

        public List<Archive> SelectedArchives;
        public List<Directive> InstallDirectives;
        public List<RawSourceFile> AllFiles = new List<RawSourceFile>();
        public ModList ModList;
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
    }
}
