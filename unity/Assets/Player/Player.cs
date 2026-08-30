// Player.cs
using UnityEngine;

public sealed class Player : MonoBehaviour
{
    public User user { get; private set; }
    [SerializeField] private TextMesh usernameText;
    public Material playerMaterial { get; private set; }

    public void Bind(User user)
    {
        this.user = user;
        gameObject.name = $"Player - {user.UniqueId}";

        string displayName = user.GetDisplayName();

        usernameText.text = displayName;
    }

    public void SetMaterial(Material material)
    {
        playerMaterial = new Material(material);
        playerMaterial.parent = material;
        playerMaterial.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f); // Random color for each player
    }

    private void OnDestroy()
    {
        if (playerMaterial != null)
        {
            Destroy(playerMaterial);
        }
    }
}
