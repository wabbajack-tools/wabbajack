using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VFS;
using Wabbajack.Lib.CompilationSteps;

namespace Wabbajack.Lib
{
    public abstract class ACompiler
    {
        protected string GamePath;

        protected string ModListOutputFolder;
        protected string ModListOutputFile;

        protected List<Directive> InstallDirectives;
        protected List<RawSourceFile> AllFiles;
        protected ModList ModList;
        protected VirtualFileSystem VFS;
        protected List<IndexedArchive> IndexedArchives;
        protected Dictionary<string, IEnumerable<VirtualFile>> IndexedFiles;

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
