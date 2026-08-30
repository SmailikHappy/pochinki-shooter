// DiscordUser.cs
public sealed class DiscordUser : User
{
    public string Username;
    public float MouseX;
    public float MouseY;
    public bool IsSelf;

    public DiscordUser(string discordUserId, string username, bool isSelf)
        : base()
    {
        Username = username;
        IsSelf = isSelf;
    }
}
