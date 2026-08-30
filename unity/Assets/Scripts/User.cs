// User.cs
public class User
{
    public string UniqueId { get; }

    public User(string uniqueId)
    {
        UniqueId = uniqueId ?? string.Empty;
    }
}
