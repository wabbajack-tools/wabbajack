namespace Wabbajack.Translation;

public enum FieldDecision
{
    Apply,
    OverriddenLater,
    NoTranslation,
    Untranslated
}

public static class TranslationRules
{
    public static FieldDecision Decide(string winningText, string ownerText, string? translatedText)
    {
        //  apply when the winning text is still the owner text, so a later override is kept.
        if (winningText != ownerText) return FieldDecision.OverriddenLater;
        if (string.IsNullOrWhiteSpace(translatedText)) return FieldDecision.NoTranslation;
        if (translatedText == ownerText) return FieldDecision.Untranslated;
        return FieldDecision.Apply;
    }
    //  file that leaves most matched fields unchanged is prob not a translation.
    public static bool IsTranslationOf(int matchedFields, int differingFields) =>
        matchedFields > 0 && differingFields * 2 > matchedFields;

    //$ strings are UI keys, not text.
    public static bool IsTranslatable(string? text) =>
        !string.IsNullOrWhiteSpace(text) && !text.StartsWith('$');
}
