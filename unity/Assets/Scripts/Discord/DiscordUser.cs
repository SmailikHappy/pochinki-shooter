// DiscordUser.cs
public sealed class DiscordUser : User
{
    public string Username;
    public bool IsSelf;

    public DiscordUser(string discordUserId, string username, bool isSelf)
        : base(discordUserId)
    {
        Username = username;
        IsSelf = isSelf;
    }
}
