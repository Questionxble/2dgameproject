public static class PlayerSessionSettings
{
    public const string DefaultPlayerName = "default";
    private const int MaxPlayerNameLength = 24;

    private static string localPlayerName = DefaultPlayerName;

    public static string LocalPlayerName
    {
        get => localPlayerName;
        set => localPlayerName = SanitizePlayerName(value);
    }

    public static string SanitizePlayerName(string candidate)
    {
        string sanitized = string.IsNullOrWhiteSpace(candidate) ? DefaultPlayerName : candidate.Trim();

        if (sanitized.Length > MaxPlayerNameLength)
        {
            sanitized = sanitized.Substring(0, MaxPlayerNameLength);
        }

        return sanitized;
    }
}