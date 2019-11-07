using Wabbajack.Lib;

namespace Wabbajack
{
    public class MO2CompilerConfigVM : ViewModel
    {
        private CompilerConfigVM _compilerConfig;

        public MO2CompilerConfigVM(CompilerConfigVM compilerConfig)
        {
            _compilerConfig = compilerConfig;
        }
    }
}
