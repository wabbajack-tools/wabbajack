using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.Directives;

/// <summary>
///     A file that has the game and MO2 folders remapped on installation
/// </summary>
[JsonName("RemappedInlineFile")]
[JsonAlias("RemappedInlineFile, Wabbajack.Lib")]
public class RemappedInlineFile : InlineFile
{
    public override bool IsDeterministic => false;
}