using UnityEngine;

/// <summary>
/// A bird that flaps along in a straight line. Hit it and it poofs: the bird is gone in a puff and
/// only drifting feathers are left behind. Put this on the Bird_A root object.
/// Wings are found by name (any child with "Wing" in its name).
/// </summary>
public class Bird : FlyingHazard
{
    [Header("Flapping")]
    public float flapSpeed = 9f;
    [Tooltip("How far each wing swings, in degrees.")]
    public float flapAngle = 35f;
    [Tooltip("How much the bird bobs up and down.")]
    public float bobHeight = 0.25f;

    [Header("Feathers")]
    public int featherCount = 40;
    [Tooltip("Scales the feathers and the poof.")]
    public float effectSize = 1f;

    Transform[] wings;
    Quaternion[] wingRest;
    float[] wingSide;
    float phase;

    void Reset()
    {
        speed = 8f;
        speedKept = 0.97f;   // a bird barely slows a rocket
    }

    protected override void Awake()
    {
        base.Awake();
        phase = Random.value * 10f;

        var found = new System.Collections.Generic.List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>())
            if (t != transform && t.name.IndexOf("wing", System.StringComparison.OrdinalIgnoreCase) >= 0)
                found.Add(t);

        wings = found.ToArray();
        wingRest = new Quaternion[wings.Length];
        wingSide = new float[wings.Length];
        for (int i = 0; i < wings.Length; i++)
        {
            wingRest[i] = wings[i].localRotation;
            // The two wings swing in opposite directions around the nose axis, so they flap together.
            wingSide[i] = wings[i].localPosition.x >= 0f ? 1f : -1f;
        }
    }

    protected override void OnFly()
    {
        float t = Time.time * flapSpeed + phase;
        float swing = Mathf.Sin(t) * flapAngle;
        Vector3 axis = noseDirection.normalized;

        for (int i = 0; i < wings.Length; i++)
            if (wings[i])
                wings[i].localRotation = Quaternion.AngleAxis(swing * wingSide[i], axis) * wingRest[i];

        // Gentle up and down while it flies
        transform.position += Vector3.up * (Mathf.Cos(t * 0.5f) * bobHeight * flapSpeed * 0.25f * Time.deltaTime);
    }

    protected override void OnHit(Vector3 hitPoint, Vector3 shipVelocity, Rigidbody ship)
    {
        float s = effectSize;
        Vector3 centre = Centre;

        // A soft white poof...
        HazardFx.Burst(centre, 12, 0.35f, 0.7f, 1f, 4f, 0.8f * s, 1.6f * s,
                       new Color(1f, 1f, 1f, 0.7f), new Color(0.85f, 0.88f, 0.95f, 0.7f), radius * 0.3f, 1.5f);

        // ...and feathers that hang in the air where the bird was
        HazardFx.Feathers(centre, shipVelocity, featherCount, s);

        Destroy(gameObject);
    }
}
