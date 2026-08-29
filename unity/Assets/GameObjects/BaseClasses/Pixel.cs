using UnityEngine;

public class Pixel : MonoBehaviour
{
    public Player owner { get; private set; }

    public void Initialize(Player owner)
    {
        this.owner = owner;
    }
}
