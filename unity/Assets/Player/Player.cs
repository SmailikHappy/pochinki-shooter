// Player.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public sealed class Player : MonoBehaviour
{
    public User user { get; private set; }
    [SerializeField] private TextMesh usernameText;
    public Material playerMaterial { get; private set; }

    public void Bind(User user)
    {
        this.user = user;
        gameObject.name = $"Player - {user.UniqueId}";

        string displayName = user is DiscordUser discordUser &&
            !string.IsNullOrWhiteSpace(discordUser.Username)
                ? discordUser.Username
                : user.UniqueId;

        if (usernameText != null)
        {
            usernameText.text = displayName;
        }
    }

    public void SetMaterial(Material material)
    {
        if (material == null)
        {
            Debug.LogWarning("Player: base material is not assigned.", this);
            return;
        }

        playerMaterial = new Material(material);
        playerMaterial.color = CreateDeterministicColor(user?.UniqueId ?? string.Empty);
    }

    private static Color CreateDeterministicColor(string uniqueId)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        uint hash = offsetBasis;

        unchecked
        {
            foreach (char character in uniqueId)
            {
                hash ^= character;
                hash *= prime;
            }
        }

        float hue = (hash % 360u) / 360f;
        float saturation = 0.65f + ((hash >> 8) & 0xffu) / 255f * 0.2f;
        float value = 0.78f + ((hash >> 16) & 0xffu) / 255f * 0.17f;
        return Color.HSVToRGB(hue, saturation, value);
    }

    private void OnDestroy()
    {
        if (playerMaterial != null)
        {
            Destroy(playerMaterial);
        }
    }

    public void LoadAvatarTexture(string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl) || playerMaterial == null)
            return;

        StartCoroutine(DownloadAvatarRoutine(avatarUrl));
    }

    private IEnumerator DownloadAvatarRoutine(string url)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Player: не удалось загрузить аватар с {url}: {request.error}");
            yield break;
        }

        if (playerMaterial == null) // игрок мог уже быть уничтожен к моменту ответа сети
            yield break;

        Texture2D avatarTexture = DownloadHandlerTexture.GetContent(request);
        playerMaterial.mainTexture = avatarTexture;
        playerMaterial.color = Color.white; // текстура не должна тускнеть от цветового тинта
    }
}
