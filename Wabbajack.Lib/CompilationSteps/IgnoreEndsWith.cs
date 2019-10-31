using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreEndsWith : ACompilationStep
    {
        private readonly string _postfix;
        private readonly string _reason;

        public IgnoreEndsWith(Compiler compiler, string postfix) : base(compiler)
        {
            _postfix = postfix;
            _reason = $"Ignored because path ends with {postfix}";
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.EndsWith(_postfix)) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;
        }

        public override IState GetState()
        {
            return new State(_postfix);
        }

        [JsonObject("IgnoreEndsWith")]
        public class State : IState
        {
            public State(string postfix)
            {
                Postfix = postfix;
            }

            public State()
            {
            }

            public string Postfix { get; set; }

            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new IgnoreEndsWith(compiler, Postfix);
            }
        }
    }
}