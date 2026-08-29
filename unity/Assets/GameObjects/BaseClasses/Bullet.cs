using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Player owner { get; private set; }
    public User ownerUser => owner != null ? owner.user : null;

    private Vector3 direction;
    private float speed;

    public void Init(Player owner, Vector3 direction, float speed, float scale)
    {
        Debug.Log($"Bullet Init: Direction: {direction}, Speed: {speed}, Scale: {scale}, Owner: {ownerUser?.UniqueId}");
        this.owner = owner;
        this.direction = direction;
        this.speed = speed;
        transform.localScale = new Vector3(scale, scale, scale);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = direction * speed;
    }
}
