// Player.cs
using UnityEngine;

public sealed class Player : MonoBehaviour
{
    public User user { get; private set; }
    [SerializeField] private TextMesh usernameText;
    public Color playerColor { get; private set; }

    public void Bind(User user)
    {
        this.user = user;
        gameObject.name = $"Player - {user.UniqueId}";
        usernameText.text = $"User: {user.UniqueId}";
        playerColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
    }
}