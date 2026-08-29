// Player.cs
using UnityEngine;

public sealed class Player : MonoBehaviour
{
    public User User { get; private set; }
    [SerializeField] private TextMesh usernameText;

    public void Bind(User user)
    {
        User = user;
        gameObject.name = $"Player - {user.UniqueId}";
        usernameText.text = $"User: {user.UniqueId}";
    }
}