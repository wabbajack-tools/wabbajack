using System.Collections.Generic;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class RunIfGame : ACompilationStep
    {
        private readonly Game _game;
        private readonly List<ICompilationStep> _microStack;

        public RunIfGame(ACompiler compiler, Game game, List<ICompilationStep> microStack) : base(compiler)
        {
            _game = game;
            _microStack = microStack;
        }

        public override Directive Run(RawSourceFile source)
        {
            return _compiler._vortexCompiler?.Game != _game ? null : _compiler._vortexCompiler.RunStack(_microStack, source);
        }

        public override IState GetState()
        {
            return new State(_game, _microStack);
        }

        [JsonArray("RunIfGame")]
        public class State : IState
        {
            public State(Game game, List<ICompilationStep> microStack)
            {
                Game = game;
                MicroStack = microStack;
            }

            public Game Game { get; set; }
            public List<ICompilationStep> MicroStack { get; set; }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new RunIfGame(compiler, Game, MicroStack);
            }
        }
    }
}
