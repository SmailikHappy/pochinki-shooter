using UnityEngine;

public class PlayerOwnable : MonoBehaviour
{
    protected Player owner { get; set; }
    
    [SerializeField] private Renderer playerRenderer;

    public void SetOwner(Player owner)
    {
        this.owner = owner;

        if (owner != null) // Sometimes the owner might be null (for now)
            playerRenderer.material = owner.playerMaterial;
    }

    public Player GetOwner()
    {
        return owner;
    }
}
