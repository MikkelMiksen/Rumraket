using UnityEngine;

/// <summary>
/// A plane flying across the sky. Hit it and it is knocked off course: it is thrown sideways,
/// tumbles, trails smoke and falls away. Put this on the Plane_Jet / Plane_Prop root object.
/// A child called "Propeller" (Plane_Prop) spins while flying.
/// </summary>
public class AirPlane : FlyingHazard
{
    [Header("Propeller")]
    [Tooltip("Degrees per second.")]
    public float propellerSpin = 1800f;

    [Header("Knocked off course")]
    [Tooltip("How hard it is thrown away from the impact (units per second).")]
    public float knockSpeed = 22f;
    [Tooltip("How fast it tumbles (radians per second).")]
    public float tumble = 2.5f;
    [Tooltip("How much of the ship's speed drags it along.")]
    [Range(0f, 1f)] public float inheritShipVelocity = 0.35f;
    [Tooltip("Off = it keeps drifting. On = it falls, as a plane that has been hit would.")]
    public bool fallsAfterHit = true;
    public float lifeAfterHit = 7f;

    [Header("Effects")]
    public float smokeSeconds = 5f;
    [Tooltip("Scales the smoke and the impact puff.")]
    public float effectSize = 1.5f;

    Transform propeller;

    void Reset()
    {
        speed = 18f;
        speedKept = 0.8f;    // heavier than a bird, so it costs the rocket some speed
    }

    protected override void Awake()
    {
        base.Awake();
        foreach (var t in GetComponentsInChildren<Transform>())
            if (t != transform && t.name.IndexOf("propeller", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                propeller = t;
                break;
            }
    }

    protected override void OnFly()
    {
        if (propeller) propeller.Rotate(noseDirection.normalized, propellerSpin * Time.deltaTime, Space.Self);
    }

    protected override void OnHit(Vector3 hitPoint, Vector3 shipVelocity, Rigidbody ship)
    {
        // The plane stops being steered and becomes a falling, tumbling object.
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = fallsAfterHit;
        rb.mass = 20f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;

        var trigger = GetComponent<Collider>();
        if (trigger) trigger.enabled = false;

        Vector3 centre = Centre;
        Vector3 away = centre - hitPoint;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.right;

        // Thrown away from the impact, keeping some of its own forward speed and some of the ship's.
        Vector3 own = FlyDirection * speed * 0.6f + FollowVelocity;
        rb.AddForce(own + away * knockSpeed + shipVelocity * inheritShipVelocity, ForceMode.VelocityChange);

        // Spun by the hit: mostly around the axis the impact would twist it on.
        Vector3 twist = Vector3.Cross(away, shipVelocity.sqrMagnitude > 0.01f ? shipVelocity.normalized : Vector3.up);
        rb.AddTorque(twist * tumble + Random.insideUnitSphere * (tumble * 0.4f), ForceMode.VelocityChange);

        float s = effectSize;
        HazardFx.Burst(hitPoint, 14, 0.4f, 0.9f, 2f, 8f, 1f * s, 2.4f * s,
                       new Color(0.9f, 0.9f, 0.9f, 0.8f), new Color(0.5f, 0.5f, 0.5f, 0.8f), radius * 0.2f, 2f);
        HazardFx.Burst(hitPoint, 18, 0.4f, 0.9f, 5f, 14f, 0.12f * s, 0.25f * s,
                       new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.5f, 0.15f, 1f), radius * 0.2f, 1f);
        HazardFx.SmokeTrail(transform, smokeSeconds, s);

        gameObject.AddComponent<HazardLife>().life = lifeAfterHit;
    }
}