using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Player Owner { get; private set; }
    public User OwnerUser => Owner != null ? Owner.User : null;

    private Vector3 direction;
    private float speed;

    public void Init(Player owner, Vector3 direction, float speed, float scale)
    {
        Debug.Log($"Bullet Init: Direction: {direction}, Speed: {speed}, Scale: {scale}, Owner: {OwnerUser?.UniqueId}");
        this.Owner = owner;
        this.direction = direction;
        this.speed = speed;
        transform.localScale = new Vector3(scale, scale, scale);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = direction * speed; // Adjust the speed as needed
    }
}
