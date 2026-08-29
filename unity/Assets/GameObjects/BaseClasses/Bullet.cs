using UnityEngine;

[RequireComponent(typeof(PlayerOwnable))]
[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    private Vector3 direction;
    private float speed;

    public void Init(Player owner, Vector3 direction, float speed, float scale)
    {
        Debug.Log($"Bullet Init: Direction: {direction}, Speed: {speed}, Scale: {scale}, Owner: {owner.user.UniqueId}");
        GetComponent<PlayerOwnable>().SetOwner(owner);
        this.direction = direction;
        this.speed = speed;
        transform.localScale = new Vector3(scale, scale, scale);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = direction * speed;

        GetComponent<MeshRenderer>().material = owner.playerMaterial;
    }
}
