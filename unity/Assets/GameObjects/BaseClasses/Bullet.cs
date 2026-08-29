using UnityEngine;

public class Bullet : MonoBehaviour
{
    private Vector3 direction;
    private float speed;

    public void Init(Vector3 direction, float speed, float scale)
    {
        Debug.Log($"Bullet Init: Direction: {direction}, Speed: {speed}, Scale: {scale}");
        this.direction = direction;
        this.speed = speed;
        transform.localScale = new Vector3(scale, scale, scale); // Set the scale of the bullet

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = direction * speed; // Adjust the speed as needed
    }
}
