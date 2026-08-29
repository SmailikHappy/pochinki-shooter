// User.cs
public class User
{
    public ulong UniqueId { get; }

    public User(ulong uniqueId)
    {
        UniqueId = uniqueId;
    }
}