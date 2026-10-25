using System;
using UnityEngine;

/// <summary>
/// An asteroid made of pre-broken chunks (see Asteroids.blend). Looks like one rock until the ship
/// hits it: then the ship loses speed, the rock bursts into its chunks, and dust, sparks and a flash
/// fly out. Everything is built from code, there are no assets to assign.
///
/// Put this on the asteroid's root object (Asteroid_A / _B / _C, whose children are the chunks).
/// The ship needs a Rigidbody and a collider. A trigger sphere is added to the asteroid
/// automatically if it has no collider.
/// </summary>
public class Asteroid : MonoBehaviour
{
    [Header("Hit")]
    [Tooltip("If empty reacts to anything")]
    public string requiredTag = "";
    [Tooltip("How much of its speed the ship keeps after smashing through. 1 = no slowdown, 0 = stops dead.")]
    [Range(0f, 1f)] public float speedKept = 0.55f;

    [Header("Break up")]
    [Tooltip("How hard the chunks are thrown outwards (units per second).")]
    public float explosionSpeed = 8f;
    [Tooltip("Chunks nearer the impact are thrown harder: 0 = all the same, 1 = a lot harder.")]
    [Range(0f, 1f)] public float impactBias = 0.6f;
    [Tooltip("How much of the ship's speed the chunks are dragged along with.")]
    [Range(0f, 1f)] public float inheritShipVelocity = 0.3f;
    public float spin = 4f;
    public float debrisLifetime = 5f;
    [Tooltip("Off for deep space. Turn on if asteroids are low enough that the chunks should fall.")]
    public bool debrisGravity = false;
    public float debrisDrag = 0.15f;

    [Header("Effects")]
    public int dustCount = 45;
    public int sparkCount = 25;
    [Tooltip("Scales dust, sparks and the flash.")]
    public float effectSize = 1f;

    /// <summary>Raised when an asteroid is destroyed: (asteroid, the ship's Rigidbody or null, impact speed).
    /// Use it for score, camera shake, sound, fuel loss...</summary>
    public static event Action<Asteroid, Rigidbody, float> Shattered;

    bool broken;
    float radius;
    Vector3 centre;

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            centre = b.center;
            radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        }
        else
        {
            centre = transform.position;
            radius = 1f;
        }

        // No collider yet? Add a trigger sphere that covers the rock.
        var existing = GetComponent<Collider>();
        if (existing)
        {
            existing.isTrigger = true;
        }
        else
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.center = transform.InverseTransformPoint(centre);
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            sphere.radius = radius * 0.9f / Mathf.Max(scale, 0.0001f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (broken || other.isTrigger) return;

        Rigidbody rb = other.attachedRigidbody;
        if (!rb) return;
        if (rb.GetComponentInParent<Asteroid>()) return;                    // asteroids ignore each other
        if (requiredTag.Length > 0 && !other.CompareTag(requiredTag) && !rb.CompareTag(requiredTag)) return;

        Vector3 shipVelocity = rb.linearVelocity;

        // Where on the rock it was hit: the point nearest the ship, on a sphere around the rock.
        Vector3 toShip = rb.worldCenterOfMass - centre;
        Vector3 hitPoint = centre + toShip.normalized * Mathf.Min(toShip.magnitude, radius);

        // The ship is slowed down.
        rb.linearVelocity = shipVelocity * speedKept;

        Shatter(hitPoint, shipVelocity, rb);
    }

    // ------------------------------------------------------------------ breaking apart

    /// <summary>Breaks the asteroid. Can also be called from other code (a missile, a shockwave...).</summary>
    public void Shatter(Vector3 hitPoint, Vector3 pushVelocity, Rigidbody ship = null)
    {
        if (broken) return;
        broken = true;

        // Turn the rock's chunks into free-flying pieces.
        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        {
            Transform chunk = mr.transform;
            Bounds b = mr.bounds;

            chunk.SetParent(null, true);

            var rb = chunk.gameObject.AddComponent<Rigidbody>();
            rb.useGravity = debrisGravity;
            rb.linearDamping = debrisDrag;
            rb.angularDamping = 0.05f;
            rb.mass = Mathf.Max(0.1f, b.size.x * b.size.y * b.size.z * 0.3f);

            // Thrown away from the impact; the ones nearest it go fastest.
            Vector3 away = b.center - hitPoint;
            float distance = away.magnitude;
            away = distance > 0.001f ? away / distance : UnityEngine.Random.onUnitSphere;
            float near = 1f - Mathf.Clamp01(distance / (radius * 2f));
            float speed = explosionSpeed * UnityEngine.Random.Range(0.7f, 1.2f) * Mathf.Lerp(1f, 0.6f + near, impactBias);

            rb.AddForce(away * speed + pushVelocity * inheritShipVelocity, ForceMode.VelocityChange);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * spin, ForceMode.VelocityChange);

            chunk.gameObject.AddComponent<AsteroidDebris>().life = debrisLifetime * UnityEngine.Random.Range(0.8f, 1.2f);
        }

        // Burst of dust, sparks and a quick flash.
        float s = effectSize;
        Burst(hitPoint, 1, 0.12f, 0.18f, 0f, 0f, 7f * s, 9f * s,
              new Color(1f, 0.85f, 0.55f, 1f), new Color(1f, 0.7f, 0.4f, 1f), 0f, 1f);
        Burst(hitPoint, sparkCount, 0.5f, 1.0f, 6f, 16f, 0.12f * s, 0.25f * s,
              new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.5f, 0.15f, 1f), radius * 0.2f, 2f);
        Burst(centre, dustCount, 0.8f, 1.8f, 2f, 9f, 1.2f * s, 3f * s,
              new Color(0.45f, 0.40f, 0.36f, 0.8f), new Color(0.25f, 0.22f, 0.20f, 0.8f), radius * 0.4f, 3.5f);

        float impactSpeed = pushVelocity.magnitude;
        if (Shattered != null) Shattered(this, ship, impactSpeed);

        Destroy(gameObject);   // the empty shell; the chunks are already free
    }

    // ------------------------------------------------------------------ one-shot particle burst

    static Material puffMaterial;

    static Material PuffMaterial()
    {
        if (puffMaterial) return puffMaterial;

        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default");
        if (!sh) return null;
        puffMaterial = new Material(sh);
        puffMaterial.mainTexture = tex;
        return puffMaterial;
    }

    public static void Burst(Vector3 position, int count, float lifeMin, float lifeMax, float speedMin, float speedMax,
                      float sizeMin, float sizeMax, Color colourA, Color colourB, float spread, float cleanupAfter)
    {
        if (count <= 0) return;

        var go = new GameObject("AsteroidBurst");
        go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // so the duration can be set

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(count, 8);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, Mathf.Max(speedMax, speedMin));
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, Mathf.Max(sizeMax, sizeMin));
        main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        if (spread > 0f)
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spread;
        }
        else
        {
            shape.enabled = false;
        }

        // Fade out over the particle's life
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(fade);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.6f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Material m = PuffMaterial();
        if (m) r.sharedMaterial = m;

        ps.Play();
        Destroy(go, cleanupAfter + 2f);
    }
}

/// <summary>Added to each flying chunk: shrinks it away at the end of its life, then removes it.</summary>
public class AsteroidDebris : MonoBehaviour
{
    public float life = 5f;
    float age;
    Vector3 startScale;

    void Start() { startScale = transform.localScale; }

    void Update()
    {
        age += Time.deltaTime;
        float shrinkFrom = Mathf.Max(0f, life - 1f);
        if (age > shrinkFrom)
            transform.localScale = startScale * Mathf.Clamp01(1f - (age - shrinkFrom));
        if (age >= life) Destroy(gameObject);
    }
}
