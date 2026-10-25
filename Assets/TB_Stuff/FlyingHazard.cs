using System;
using UnityEngine;

/// <summary>
/// Bird or plane that reacts when the rocket hits it. Same idea as Asteroid.cs: no assets to assign.
///
/// Bird : disappears in a puff and leaves feathers drifting down.
/// Plane: either veers off its course out of control, or loses a wing (random, see wingBreakChance).
///
/// Put this on the ROOT of the imported model (Bird, Plane_Airliner, Plane_Prop).
/// Child parts are found by name: "WingL*", "WingR*" (wings + engines), "Propeller".
/// The rocket needs a Rigidbody and a collider, like for asteroids.
/// </summary>
public class FlyingHazard : MonoBehaviour
{
    public enum Kind { Bird, Plane }

    [Header("Type")]
    public Kind kind = Kind.Plane;

    [Header("Hit")]
    [Tooltip("If empty reacts to anything")]
    public string requiredTag = "";
    [Tooltip("How much of its speed the rocket keeps. 1 = no slowdown.")]
    [Range(0f, 1f)] public float speedKept = 0.85f;

    [Header("Flying (set to 0 if your spawner moves it)")]
    [Tooltip("Units per second along the model's forward (+Z).")]
    public float flySpeed = 10f;
    public float despawnAfter = 60f;
    [Tooltip("Bird wings flap, plane propeller spins.")]
    public bool animate = true;
    public float flapAngle = 28f;
    public float flapSpeed = 9f;
    public float propellerSpeed = 1500f;   // degrees per second

    [Header("Bird: poof")]
    public int featherCount = 14;
    public float featherSize = 0.35f;
    public float featherLifetime = 3.5f;
    public float puffSize = 1f;

    [Header("Plane: crash")]
    [Range(0f, 1f)] public float wingBreakChance = 0.5f;
    [Tooltip("How far it swerves off course when it veers (degrees).")]
    public Vector2 veerAngle = new Vector2(35f, 70f);
    public float tumbleSpeed = 120f;
    [Tooltip("0 = drifts away in a straight line, 1 = falls like a stone.")]
    [Range(0f, 1f)] public float crashGravity = 0.35f;
    public float crashLifetime = 5f;
    public bool smokeTrail = true;

    /// <summary>(hazard, the rocket's Rigidbody or null, impact speed). Use it for score, shake, sound...</summary>
    public static event Action<FlyingHazard, Rigidbody, float> Hit;

    bool hit;
    float age;

    // crash state
    Vector3 crashVelocity, spinAxis;
    float crashAge, nextSmoke;
    Vector3 startScale;

    // animation
    Transform[] wings = new Transform[0];
    float[] wingSide = new float[0];
    Transform propeller;

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        var renderers = GetComponentsInChildren<Renderer>();

        // Trigger box around the whole model (in local space so rotation is fine).
        if (!GetComponent<Collider>())
        {
            bool first = true;
            Bounds local = new Bounds();
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = transform.InverseTransformPoint(r.transform.TransformPoint(c));
                    if (first) { local = new Bounds(p, Vector3.zero); first = false; }
                    else local.Encapsulate(p);
                }
            }
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = local.center;
            box.size = local.size * 1.2f;
        }
        else GetComponent<Collider>().isTrigger = true;

        // A kinematic Rigidbody makes sure triggers fire even though we move by transform.
        if (!GetComponent<Rigidbody>())
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Find the parts that animate
        var wingList = new System.Collections.Generic.List<Transform>();
        var sideList = new System.Collections.Generic.List<float>();
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t == transform) continue;
            if (t.name == "WingL" || t.name == "WingR")
            {
                var r = t.GetComponent<Renderer>();
                float x = r ? transform.InverseTransformPoint(r.bounds.center).x : 0f;
                wingList.Add(t);
                sideList.Add(x >= 0f ? 1f : -1f);
            }
            else if (t.name == "Propeller") propeller = t;
        }
        wings = wingList.ToArray();
        wingSide = sideList.ToArray();
    }

    // ------------------------------------------------------------------ every frame

    void Update()
    {
        age += Time.deltaTime;

        if (!hit)
        {
            transform.position += transform.forward * flySpeed * Time.deltaTime;

            if (animate)
            {
                if (kind == Kind.Bird)
                {
                    float a = Mathf.Sin(age * flapSpeed) * flapAngle;
                    for (int i = 0; i < wings.Length; i++)
                        wings[i].localRotation = Quaternion.Euler(0f, 0f, a * wingSide[i]);
                }
                if (propeller) propeller.localRotation = Quaternion.Euler(0f, 0f, age * propellerSpeed);
            }

            if (despawnAfter > 0f && age > despawnAfter) Destroy(gameObject);
            return;
        }

        if (kind == Kind.Plane) CrashUpdate();
    }

    // ------------------------------------------------------------------ hit

    void OnTriggerEnter(Collider other)
    {
        if (hit || other.isTrigger) return;

        Rigidbody rb = other.attachedRigidbody;
        if (!rb) return;
        if (rb.GetComponentInParent<FlyingHazard>() || rb.GetComponentInParent<Asteroid>()) return;
        if (requiredTag.Length > 0 && !other.CompareTag(requiredTag) && !rb.CompareTag(requiredTag)) return;

        Vector3 shipVelocity = rb.linearVelocity;
        rb.linearVelocity = shipVelocity * speedKept;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        OnHit(hitPoint, shipVelocity, rb);
    }

    /// <summary>Can also be called from other code (a missile, a shockwave...).</summary>
    public void OnHit(Vector3 hitPoint, Vector3 pushVelocity, Rigidbody ship = null)
    {
        if (hit) return;
        hit = true;

        if (kind == Kind.Bird) Poof(hitPoint, pushVelocity);
        else Crash(hitPoint, pushVelocity);

        if (Hit != null) Hit(this, ship, pushVelocity.magnitude);
    }

    // ------------------------------------------------------------------ bird

    void Poof(Vector3 hitPoint, Vector3 pushVelocity)
    {
        Vector3 centre = GetCentre();

        // White puff + a few soft grey ones
        Asteroid.Burst(centre, 1, 0.25f, 0.35f, 0f, 0f, 3f * puffSize, 4f * puffSize,
            Color.white, new Color(0.9f, 0.95f, 1f, 1f), 0f, 1f);
        Asteroid.Burst(centre, 10, 0.5f, 0.9f, 1f, 4f, 0.7f * puffSize, 1.4f * puffSize,
            new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.9f, 1f, 0.8f), 0.4f * puffSize, 2f);

        Mesh feather = FeatherMesh();
        Material mat = FeatherMaterial();
        for (int i = 0; i < featherCount; i++)
        {
            var go = new GameObject("Feather");
            go.transform.position = centre + UnityEngine.Random.insideUnitSphere * 0.4f;
            go.transform.rotation = UnityEngine.Random.rotation;
            go.transform.localScale = Vector3.one * featherSize * UnityEngine.Random.Range(0.7f, 1.3f);
            go.AddComponent<MeshFilter>().sharedMesh = feather;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var f = go.AddComponent<Feather>();
            f.velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 6f) + pushVelocity * 0.25f;
            f.life = featherLifetime * UnityEngine.Random.Range(0.8f, 1.2f);
            f.tint = UnityEngine.Random.value < 0.6f ? Color.white : new Color(0.82f, 0.9f, 1f, 1f);
        }

        Destroy(gameObject);   // the bird itself is gone
    }

    // ------------------------------------------------------------------ plane

    void Crash(Vector3 hitPoint, Vector3 pushVelocity)
    {
        Vector3 centre = GetCentre();
        startScale = transform.localScale;

        // Sparks and a puff of smoke where it was hit
        Asteroid.Burst(hitPoint, 18, 0.4f, 0.8f, 5f, 12f, 0.12f, 0.25f,
            new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.5f, 0.15f, 1f), 0.3f, 2f);
        Asteroid.Burst(hitPoint, 12, 0.8f, 1.5f, 1f, 4f, 1f, 2.2f,
            new Color(0.3f, 0.3f, 0.32f, 0.8f), new Color(0.15f, 0.15f, 0.17f, 0.8f), 0.4f, 3f);

        bool breakWing = UnityEngine.Random.value < wingBreakChance && wings.Length > 0;
        Vector3 forwardVel = transform.forward * flySpeed;

        if (breakWing)
        {
            // Pick a side and snap off everything whose name starts with WingL / WingR
            string prefix = UnityEngine.Random.value < 0.5f ? "WingL" : "WingR";
            Vector3 sideDir = Vector3.zero; int n = 0;

            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t == transform || !t.name.StartsWith(prefix)) continue;
                var r = t.GetComponent<Renderer>();
                if (!r) continue;
                Vector3 c = r.bounds.center;
                sideDir += c - centre; n++;

                // Pivot at the piece's own centre so it tumbles around itself and shrinks in place.
                var piece = new GameObject(t.name + "_Piece");
                piece.transform.position = c;
                t.SetParent(piece.transform, true);

                var rb = piece.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.linearDamping = 0.2f;
                rb.mass = 1f;
                Vector3 away = (c - centre).normalized;
                rb.AddForce(forwardVel * 0.7f + away * UnityEngine.Random.Range(3f, 6f) + Vector3.up * 2f, ForceMode.VelocityChange);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
                piece.AddComponent<AsteroidDebris>().life = crashLifetime;
            }

            // The rest of the plane rolls toward the side that lost its wing and drops.
            sideDir = n > 0 ? sideDir.normalized : transform.right;
            float roll = Vector3.Dot(sideDir, transform.right) >= 0f ? 1f : -1f;
            crashVelocity = forwardVel * 0.6f;
            spinAxis = (transform.forward * -roll * 2.5f + transform.up * 0.4f).normalized;
            tumbleSpeedNow = tumbleSpeed * 2f;
            gravityNow = Mathf.Max(crashGravity, 0.8f);
        }
        else
        {
            // Knocked off course: swerves left or right, nose drops a bit, rolls slowly.
            float yaw = UnityEngine.Random.Range(veerAngle.x, veerAngle.y) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            crashVelocity = Quaternion.AngleAxis(yaw, Vector3.up) * forwardVel * 0.8f;
            if (crashVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(crashVelocity.normalized, Vector3.up);
            spinAxis = (transform.forward * 1.5f + transform.up * 0.5f).normalized;
            tumbleSpeedNow = tumbleSpeed;
            gravityNow = crashGravity;
        }

        // Stop pushing through the rocket
        var col = GetComponent<Collider>(); if (col) col.enabled = false;
    }

    float tumbleSpeedNow, gravityNow;

    void CrashUpdate()
    {
        crashAge += Time.deltaTime;
        crashVelocity += Physics.gravity * gravityNow * Time.deltaTime;
        transform.position += crashVelocity * Time.deltaTime;
        transform.Rotate(spinAxis * tumbleSpeedNow * Time.deltaTime, Space.World);

        if (propeller) propeller.Rotate(0f, 0f, propellerSpeed * Time.deltaTime, Space.Self);

        if (smokeTrail && crashAge >= nextSmoke)
        {
            nextSmoke = crashAge + 0.18f;
            Asteroid.Burst(transform.position, 3, 0.8f, 1.4f, 0.2f, 1f, 0.8f, 1.6f,
                new Color(0.35f, 0.35f, 0.37f, 0.7f), new Color(0.2f, 0.2f, 0.22f, 0.7f), 0.2f, 2f);
        }

        float shrinkFrom = Mathf.Max(0f, crashLifetime - 1f);
        if (crashAge > shrinkFrom)
            transform.localScale = startScale * Mathf.Clamp01(1f - (crashAge - shrinkFrom));
        if (crashAge >= crashLifetime) Destroy(gameObject);
    }

    // ------------------------------------------------------------------ helpers

    Vector3 GetCentre()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return transform.position;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b.center;
    }

    static Mesh featherMesh;
    static Mesh FeatherMesh()
    {
        if (featherMesh) return featherMesh;
        // A thin diamond, low-poly like the rest.
        featherMesh = new Mesh { name = "Feather" };
        featherMesh.vertices = new[]
        {
            new Vector3(0f, 0f, -0.5f), new Vector3(0.12f, 0f, -0.1f),
            new Vector3(0f, 0f, 0.5f),  new Vector3(-0.12f, 0f, -0.1f)
        };
        featherMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        featherMesh.RecalculateNormals();
        return featherMesh;
    }

    static Material featherMaterial;
    static Material FeatherMaterial()
    {
        if (featherMaterial) return featherMaterial;
        Shader sh = Shader.Find("Sprites/Default");   // unlit and double-sided
        featherMaterial = new Material(sh);
        return featherMaterial;
    }
}

/// <summary>A drifting feather: flutters down, spins, shrinks away.</summary>
public class Feather : MonoBehaviour
{
    public Vector3 velocity;
    public float life = 3.5f;
    public Color tint = Color.white;

    float age, seed;
    Vector3 startScale;
    MeshRenderer mr;
    MaterialPropertyBlock block;

    void Start()
    {
        startScale = transform.localScale;
        seed = UnityEngine.Random.value * 10f;
        mr = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        block.SetColor("_Color", tint);
        mr.SetPropertyBlock(block);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        velocity = Vector3.Lerp(velocity, Vector3.zero, 1.8f * dt);          // air drag
        velocity += Vector3.down * 0.9f * dt;                                // slowly falls
        velocity.x += Mathf.Sin(age * 3f + seed) * 1.5f * dt;                // flutter side to side
        transform.position += velocity * dt;
        transform.Rotate(new Vector3(90f, 140f, 60f) * dt, Space.Self);

        float shrinkFrom = Mathf.Max(0f, life - 1f);
        if (age > shrinkFrom) transform.localScale = startScale * Mathf.Clamp01(1f - (age - shrinkFrom));
        if (age >= life) Destroy(gameObject);
    }
}
