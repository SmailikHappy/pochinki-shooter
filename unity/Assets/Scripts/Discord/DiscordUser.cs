// DiscordUser.cs
// DiscordUser.cs
public sealed class DiscordUser : User
{
    public string Username;
    public string AvatarUrl;
    public float MouseX;
    public float MouseY;
    public bool IsSelf;

    public DiscordUser(string discordUserId, string username, bool isSelf)
        : base(discordUserId)
    {
        Username = username;
        IsSelf = isSelf;
    }
}
