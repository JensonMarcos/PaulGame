using UnityEngine;

public class Projectile : MonoBehaviour
{
    public bool IsAlive { get; private set; }

    [SerializeField] TrailRenderer trail;
    [SerializeField] MeshRenderer model;
    [SerializeField] float collideDespawnDelay;

    Vector3 velocity;
    float gravity;
    float size;
    float lifetime;
    float age;
    LayerMask hitMask;
    bool waitingDespawn;
    float despawnTimer;

    public void Launch(Vector3 direction, float speed, float gravity, float size, float lifetime, LayerMask hitMask)
    {
        this.gravity = gravity;
        this.size = size;
        this.lifetime = lifetime;
        this.hitMask = hitMask;
        velocity = direction.normalized * speed;
        age = 0f;
        waitingDespawn = false;
        despawnTimer = 0f;
        IsAlive = true;
        transform.rotation = Quaternion.LookRotation(direction);
        if (trail != null) trail.Clear();
        if (model != null) model.enabled = true;
    }

    void FixedUpdate()
    {
        if (!IsAlive) return;

        float dt = Time.fixedDeltaTime;

        if (waitingDespawn)
        {
            despawnTimer += dt;
            if (despawnTimer >= collideDespawnDelay)
                IsAlive = false;
            return;
        }

        age += dt;
        if (age >= lifetime)
        {
            IsAlive = false;
            return;
        }

        velocity += Vector3.down * gravity * dt;

        Vector3 displacement = velocity * dt;
        float distance = displacement.magnitude;
        if (distance <= 0f) return;

        Vector3 direction = displacement / distance;
        if (Physics.SphereCast(transform.position, size, direction, out RaycastHit hit, distance, hitMask))
        {
            transform.position = hit.point;
            waitingDespawn = true;
            despawnTimer = 0f;
            if (model != null) model.enabled = false;
            return;
        }

        transform.position += displacement;
        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity);
    }

    public void ResetProjectile()
    {
        IsAlive = false;
        age = 0f;
        waitingDespawn = false;
        despawnTimer = 0f;
        if (trail != null) trail.Clear();
    }
}
