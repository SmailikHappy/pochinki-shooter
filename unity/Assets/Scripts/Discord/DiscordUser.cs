// DiscordUser.cs
public sealed class DiscordUser : User
{
    public string Username;
    public float MouseX;
    public float MouseY;
    public bool IsSelf;

    public DiscordUser(ulong discordUserId, string username, bool isSelf)
        : base(discordUserId)
    {
        Username = username;
        IsSelf = isSelf;
    }
}