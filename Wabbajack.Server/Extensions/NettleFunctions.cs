using System.Text.RegularExpressions;
using System.Web;
using Nettle.Compiler;
using Nettle.Functions;

namespace Wabbajack.Server.Extensions;

public static class NettleFunctions
{
    public static INettleCompiler RegisterWJFunctions(this INettleCompiler compiler)
    {
        compiler.RegisterFunction(new Escape());
        return compiler;
    }

    private sealed class UrlEncode : FunctionBase
    {
        public UrlEncode() : base()
        {
            DefineRequiredParameter("text", "text to encode", typeof(string));
        }
        
        protected override object GenerateOutput(TemplateContext context, params object[] parameterValues)
        {
            var value = GetParameterValue<string>("text", parameterValues);
            return HttpUtility.UrlEncode(value);
        }

        public override string Description => "URL encodes a string";
    }
    
    private sealed class Escape : FunctionBase
    {
        public Escape() : base()
        {
            DefineRequiredParameter("text", "text to escape", typeof(string));
        }
        
        protected override object GenerateOutput(TemplateContext context, params object[] parameterValues)
        {
            var value = GetParameterValue<string>("text", parameterValues);
            return Regex.Escape(value).Replace("'", "\'");
        }

        public override string Description => "Escapes a string";
    }
}