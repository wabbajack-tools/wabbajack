using System.IO;
using System.Linq;
using Wabbajack.DTOs.Directives;

namespace Wabbajack.Compiler.CompilationSteps;

public class zEditIntegration
{
    public static void VerifyMerges(MO2Compiler compiler)
    {
        var byName = compiler.InstallDirectives.ToDictionary(f => f.To);

        foreach (var directive in compiler.InstallDirectives.OfType<MergedPatch>())
        foreach (var source in directive.Sources)
        {
            if (!byName.TryGetValue(source.RelativePath, out var result))
                throw new InvalidDataException(
                    $"{source.RelativePath} is needed for merged patch {directive.To} but is not included in the install.");

            if (result.Hash != source.Hash)
                throw new InvalidDataException(
                    $"Hashes for {result.To} needed for zEdit merge sources don't match, this shouldn't happen");
        }
    }
}