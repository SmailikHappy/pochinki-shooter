using System;
using UnityEngine;

public class PlayerOwnable : MonoBehaviour
{
    protected Player owner { get; set; }

    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Material neutralMaterial;
    private Material defaultMaterial;

    public event Action<Player> OnOwnerChanged;

    private void Awake()
    {
        if (playerRenderer != null)
            defaultMaterial = playerRenderer.sharedMaterial;
    }

    public void SetOwner(Player owner)
    {
        this.owner = owner;
        OnOwnerChanged?.Invoke(owner);

        if (playerRenderer == null)
            return;

        Material targetMaterial = owner != null && owner.playerMaterial != null
            ? owner.playerMaterial
            : neutralMaterial != null
                ? neutralMaterial
                : defaultMaterial;

        if (targetMaterial != null)
            playerRenderer.sharedMaterial = targetMaterial;
    }

    public Player GetOwner()
    {
        return owner;
    }
}
