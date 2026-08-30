using System;
using UnityEngine;

public class PlayerOwnable : MonoBehaviour
{
    protected Player owner { get; set; }

    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Material neutralMaterial;

    public event Action<Player> OnOwnerChanged;

    public void SetOwner(Player owner)
    {
        this.owner = owner;
        OnOwnerChanged?.Invoke(owner);

        if (playerRenderer == null)
            return;

        if (owner != null && owner.playerMaterial != null)
            playerRenderer.sharedMaterial = owner.playerMaterial;
        else if (neutralMaterial != null)
            playerRenderer.sharedMaterial = neutralMaterial;
    }

    public Player GetOwner()
    {
        return owner;
    }
}