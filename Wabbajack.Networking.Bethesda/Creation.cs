namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     One of the Anniversary Edition Creations, as the game's own table describes it.
/// </summary>
/// <param name="ContentId">
///     The id Bethesda's content endpoint is keyed on. The extracted table carries three ids per Creation
///     and this is column 0; the other two are older ones, one of which is confusingly what the response
///     calls <c>legacy_content_id</c>.
/// </param>
/// <param name="Plugin">
///     The plugin file name, <c>.esl</c> or <c>.esm</c>. The Creation's <c>.bsa</c> shares its stem, which
///     is how <see cref="CreationIndex" /> resolves either of them to the same Creation.
/// </param>
/// <param name="DisplayName">What Bethesda calls it. For messages only; nothing matches on it.</param>
public sealed record Creation(long ContentId, string Plugin, string DisplayName);
