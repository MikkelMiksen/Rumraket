using UnityEngine;

/// <summary>
/// Shared behaviour for things that fly through the sky and that the rocket can hit (Bird, AirPlane).
/// It flies in a straight line along the model's nose, and when the ship touches it, slows the ship
/// down and calls OnHit, which each kind of hazard answers in its own way.
///
/// The ship needs a Rigidbody and a collider. A trigger sphere is added automatically if the
/// hazard has no collider, so the ship passes through it as it breaks up.
/// </summary>
public abstract class FlyingHazard : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("Metres per second along the nose. 0 = hangs in the air.")]
    public float speed = 10f;
    [Tooltip("Which way the nose points, in the object's own axes. If it flies tail first, try (0,0,-1).")]
    public Vector3 noseDirection = Vector3.forward;
    [Tooltip("Removed after this many seconds if nothing hit it.")]
    public float despawnAfter = 60f;

    [Header("Hit")]
    [Tooltip("Leave empty to react to anything with a Rigidbody, or enter a tag such as Player.")]
    public string requiredTag = "";
    [Tooltip("How much of its speed the ship keeps after the hit. 1 = no slowdown.")]
    [Range(0f, 1f)] public float speedKept = 0.95f;

    /// <summary>Raised on every hit: (hazard, the ship's Rigidbody, the ship's speed on impact).
    /// Use it for score, camera shake, sound...</summary>
    public static event System.Action<FlyingHazard, Rigidbody, float> Hit;

    SphereCollider addedSphere;
    Rigidbody followBody;
    float followFraction;
    protected bool hasBeenHit;
    protected float radius;
    Vector3 centreLocal;
    float age;

    /// <summary>The middle of the model in world space (it moves with the hazard).</summary>
    protected Vector3 Centre { get { return transform.TransformPoint(centreLocal); } }

    /// <summary>The direction it is flying, in world space.</summary>
    protected Vector3 FlyDirection { get { return transform.TransformDirection(noseDirection.normalized); } }

    protected virtual void Awake()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        Vector3 centre = transform.position;
        radius = 1f;
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            centre = b.center;
            radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        }
        centreLocal = transform.InverseTransformPoint(centre);

        var existing = GetComponent<Collider>();
        if (existing)
        {
            existing.isTrigger = true;
        }
        else
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            addedSphere = sphere;
            sphere.isTrigger = true;
            sphere.center = centreLocal;
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            sphere.radius = radius * 0.9f / Mathf.Max(scale, 0.0001f);
        }
    }

    /// <summary>Makes the automatic hit sphere bigger by this many metres (used so a very fast ship cannot skip over it).</summary>
    public void WidenTrigger(float extraMetres)
    {
        if (!addedSphere || extraMetres <= 0f) return;
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        addedSphere.radius += extraMetres / Mathf.Max(scale, 0.0001f);
    }

    /// <summary>Makes the hazard climb along with the ship, at this fraction (0 to 1) of the ship's upward speed.
    /// At 0.8 the ship only gains on it at 20% of its own speed, which leaves time to dodge.</summary>
    public void FollowShip(Rigidbody ship, float fraction)
    {
        followBody = ship;
        followFraction = Mathf.Clamp01(fraction);
    }

    /// <summary>How fast the hazard is being carried upwards right now (zero if it does not follow the ship).</summary>
    protected Vector3 FollowVelocity
    {
        get
        {
            if (!followBody) return Vector3.zero;
            return Vector3.up * (Mathf.Max(0f, followBody.linearVelocity.y) * followFraction);
        }
    }

    void Update()
    {
        if (hasBeenHit) return;

        transform.position += (FlyDirection * speed + FollowVelocity) * Time.deltaTime;

        age += Time.deltaTime;
        if (age > despawnAfter) { Destroy(gameObject); return; }

        OnFly();
    }

    /// <summary>Called every frame while flying (flapping wings, spinning propellers...).</summary>
    protected virtual void OnFly() { }

    /// <summary>The hazard's own reaction. The ship has already been slowed.</summary>
    protected abstract void OnHit(Vector3 hitPoint, Vector3 shipVelocity, Rigidbody ship);

    void OnTriggerEnter(Collider other)
    {
        if (hasBeenHit || other.isTrigger) return;

        Rigidbody rb = other.attachedRigidbody;
        if (!rb) return;
        if (rb.GetComponentInParent<FlyingHazard>() || rb.GetComponentInParent<Asteroid>()) return;
        if (requiredTag.Length > 0 && !other.CompareTag(requiredTag) && !rb.CompareTag(requiredTag)) return;

        Vector3 shipVelocity = rb.linearVelocity;

        // Where it was hit: the point nearest the ship, on a sphere around the hazard.
        Vector3 centre = Centre;
        Vector3 toShip = rb.worldCenterOfMass - centre;
        Vector3 hitPoint = centre + toShip.normalized * Mathf.Min(toShip.magnitude, radius);

        rb.linearVelocity = shipVelocity * speedKept;

        hasBeenHit = true;
        OnHit(hitPoint, shipVelocity, rb);

        if (Hit != null) Hit(this, rb, shipVelocity.magnitude);
    }
}