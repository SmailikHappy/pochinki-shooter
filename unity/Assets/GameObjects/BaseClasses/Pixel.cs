using UnityEngine;

[RequireComponent(typeof(PlayerOwnable))]
public class Pixel : MonoBehaviour
{
    public void Init(Player owner)
    {
        GetComponent<PlayerOwnable>().SetOwner(owner);
    }
}
