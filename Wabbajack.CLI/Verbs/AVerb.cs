using System.Threading.Tasks;

namespace Wabbajack.CLI.Verbs
{
    public abstract class AVerb
    {
        public int Execute()
        {
            return Run().Result;
        }

        protected abstract Task<int> Run();

    }
}
