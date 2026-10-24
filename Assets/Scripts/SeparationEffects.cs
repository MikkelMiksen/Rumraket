using System.Collections;
using UnityEngine;

/// <summary>
/// Small explosions when a stage separates: pyro bolts pop around the joint, then the thruster
/// of the stage above lights as the shell halves blow off and the spent stage lets go.
///
/// Put it on the same object as RocketAssembly. It builds its own particle systems from code,
/// so there is nothing to import or assign.
/// </summary>
[RequireComponent(typeof(RocketAssembly))]
public class SeparationEffects : MonoBehaviour
{
    [Header("Pyro bolts around the joint")]
    public int explosionCount = 10;
    [Tooltip("Seconds the little explosions are spread over (keep at or below the Shell Pop Delay).")]
    public float pyroDuration = 0.2f;

    [Header("Look")]
    [Tooltip("Scales every effect. 1 suits the rocket as modelled.")]
    public float size = 1f;
    [Tooltip("Optional. Leave empty to use a built-in soft round material.")]
    public Material fireMaterial;
    public Material smokeMaterial;

    RocketAssembly rocket;
    ParticleSystem fire, smoke, sparks;
    Texture2D softDot;

    void Awake()
    {
        rocket = GetComponent<RocketAssembly>();
        softDot = MakeDot();

        Material fireMat = fireMaterial ? fireMaterial : MakeMaterial();
        Material smokeMat = smokeMaterial ? smokeMaterial : MakeMaterial();

        fire = MakeSystem("FX_Fire", fireMat, 0f, 400,
            Grad(new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.55f, 0.1f, 0.9f),
                 new Color(0.35f, 0.2f, 0.12f, 0.5f), new Color(0.15f, 0.15f, 0.15f, 0f)),
            new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.1f)));

        smoke = MakeSystem("FX_Smoke", smokeMat, 0f, 300,
            Grad(new Color(0.55f, 0.5f, 0.45f, 0.55f), new Color(0.3f, 0.3f, 0.3f, 0.4f),
                 new Color(0.2f, 0.2f, 0.2f, 0f)),
            new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.4f)));

        sparks = MakeSystem("FX_Sparks", fireMat, 0.3f, 400,
            Grad(new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.5f, 0.1f, 1f), new Color(1f, 0.3f, 0.05f, 0f)),
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
    }

    void OnEnable()  { rocket.SeparationStarted += OnSeparation; }
    void OnDisable() { rocket.SeparationStarted -= OnSeparation; }

    void OnSeparation(int index) { StartCoroutine(Sequence(index)); }

    IEnumerator Sequence(int index)
    {
        Vector3 ringWorld, engineWorld; float radius;
        rocket.GetSeam(index, out ringWorld, out radius, out engineWorld);

        // Remember the points relative to the rocket so the effect follows it while it flies.
        Transform tr = rocket.transform;
        Vector3 ringLocal = tr.InverseTransformPoint(ringWorld);
        Vector3 engineLocal = tr.InverseTransformPoint(engineWorld);
        float radiusWorld = radius;

        // 1) Pyro bolts: little explosions at random points around the joint.
        int n = Mathf.Max(1, explosionCount);
        float spent = 0f;
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            Vector3 dir = tr.right * Mathf.Cos(a) + tr.forward * Mathf.Sin(a);
            SmallExplosion(tr.TransformPoint(ringLocal) + dir * radiusWorld, dir);

            float wait = pyroDuration / n * Random.Range(0.5f, 1.5f);
            spent += wait;
            yield return new WaitForSeconds(wait);
        }

        // 2) The shells pop and the stage above lights its thruster.
        float untilPop = rocket.shellPopDelay - spent;
        if (untilPop > 0f) yield return new WaitForSeconds(untilPop);
        Ignition(tr.TransformPoint(engineLocal));
    }

    // ------------------------------------------------------------------ effects

    float Scale { get { return size * rocket.transform.lossyScale.y; } }

    void SmallExplosion(Vector3 pos, Vector3 outward)
    {
        for (int i = 0; i < 4; i++)
            Puff(fire, pos, outward * Random.Range(1.5f, 4f) + Random.insideUnitSphere,
                 Random.Range(0.35f, 0.7f), Random.Range(0.25f, 0.45f));
        for (int i = 0; i < 5; i++)
            Puff(sparks, pos, outward * Random.Range(3f, 8f) + Random.insideUnitSphere * 2f,
                 Random.Range(0.06f, 0.12f), Random.Range(0.4f, 0.8f));
        for (int i = 0; i < 2; i++)
            Puff(smoke, pos, outward * Random.Range(0.5f, 1.5f) + Random.insideUnitSphere * 0.5f,
                 Random.Range(0.7f, 1.2f), Random.Range(0.9f, 1.5f));
    }

    void Ignition(Vector3 pos)
    {
        Vector3 down = -rocket.transform.up;
        Vector3 right = rocket.transform.right, fwd = rocket.transform.forward;

        // bright flash
        Puff(fire, pos, Vector3.zero, 3.5f, 0.12f);

        // flame blast out of the nozzle
        for (int i = 0; i < 14; i++)
            Puff(fire, pos, (down + Random.insideUnitSphere * 0.4f).normalized * Random.Range(5f, 11f),
                 Random.Range(0.8f, 1.6f), Random.Range(0.35f, 0.6f));

        // billowing smoke ring
        for (int i = 0; i < 8; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            Vector3 dir = right * Mathf.Cos(a) + fwd * Mathf.Sin(a);
            Puff(smoke, pos, dir * Random.Range(2f, 5f) + down * Random.Range(0.5f, 2f),
                 Random.Range(1.5f, 2.5f), Random.Range(1.2f, 2f));
        }

        // sparks
        for (int i = 0; i < 24; i++)
            Puff(sparks, pos, (down * 0.6f + Random.insideUnitSphere).normalized * Random.Range(6f, 14f),
                 Random.Range(0.1f, 0.18f), Random.Range(0.5f, 1f));
    }

    void Puff(ParticleSystem ps, Vector3 pos, Vector3 velocity, float particleSize, float life)
    {
        float s = Scale;
        var p = new ParticleSystem.EmitParams();
        p.position = pos;
        p.velocity = velocity * s;
        p.startSize = particleSize * s;
        p.startLifetime = life;
        p.startColor = Color.white;     // the colour comes from the gradient over the particle's life
        ps.Emit(p, 1);
    }

    // ------------------------------------------------------------------ building the particle systems

    ParticleSystem MakeSystem(string objectName, Material mat, float gravity, int maxParticles,
                              Gradient colour, AnimationCurve sizeOverLife)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.gravityModifier = gravity;

        var emission = ps.emission; emission.enabled = false;   // we emit by hand
        var shape = ps.shape; shape.enabled = false;

        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(colour);

        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLife);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (mat) r.material = mat;

        ps.Play();
        return ps;
    }

    // Evenly spaced colour stops, alpha taken from each colour.
    static Gradient Grad(params Color[] stops)
    {
        var colours = new GradientColorKey[stops.Length];
        var alphas = new GradientAlphaKey[stops.Length];
        for (int i = 0; i < stops.Length; i++)
        {
            float t = i / (float)(stops.Length - 1);
            colours[i] = new GradientColorKey(stops[i], t);
            alphas[i] = new GradientAlphaKey(stops[i].a, t);
        }
        var g = new Gradient();
        g.SetKeys(colours, alphas);
        return g;
    }

    // A soft white dot, so the puffs are round and fade at the edges.
    static Texture2D MakeDot()
    {
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
        return tex;
    }

    // Sprites/Default works in every render pipeline; the others are fallbacks.
    Material MakeMaterial()
    {
        string[] shaders = { "Sprites/Default", "Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit" };
        foreach (var name in shaders)
        {
            Shader sh = Shader.Find(name);
            if (!sh) continue;
            var m = new Material(sh);
            m.mainTexture = softDot;
            return m;
        }
        Debug.LogWarning("SeparationEffects: no particle shader found. Assign Fire Material and Smoke Material.");
        return null;
    }
}