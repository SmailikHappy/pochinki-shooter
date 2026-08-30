// User.cs
public class User
{
    public ulong UniqueId { get; }
    static ulong _nextUniqueId = 1;

    public User()
    {
        UniqueId = GetUniqueId();
    }

    static ulong GetUniqueId()
    {
        return _nextUniqueId++;
    }

    public virtual string GetDisplayName()
    {
        return $"User {UniqueId}";
    }
}
