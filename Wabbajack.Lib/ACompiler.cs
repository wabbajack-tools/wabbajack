using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;

namespace Wabbajack.Lib
{
    public abstract class ACompiler
    {
        public ModManager ModManager;
        public Compiler _mo2Compiler;
        public VortexCompiler _vortexCompiler;

        public string GamePath;

        public string ModListOutputFolder;
        public string ModListOutputFile;

        public List<Archive> SelectedArchives;
        public List<Directive> InstallDirectives;
        public List<RawSourceFile> AllFiles;
        public ModList ModList;
        public VirtualFileSystem VFS;
        public List<IndexedArchive> IndexedArchives;
        public Dictionary<string, IEnumerable<VirtualFile>> IndexedFiles;

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
