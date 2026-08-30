using UnityEngine;

public class PlayerOwnable : MonoBehaviour
{
    protected Player owner { get; set; }

    [SerializeField] private Renderer playerRenderer;

    public void SetOwner(Player owner)
    {
        this.owner = owner;

        if (playerRenderer == null)
        {
            return;
        }

        // The Player already owns one runtime material. Sharing it avoids an
        // extra material allocation for every pixel on the board.
        if (owner != null && owner.playerMaterial != null)
        {
            playerRenderer.sharedMaterial = owner.playerMaterial;
        }
    }

    public Player GetOwner()
    {
        return owner;
    }
}
