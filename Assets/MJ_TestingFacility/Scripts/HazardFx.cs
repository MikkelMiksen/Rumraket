using UnityEngine;

/// <summary>
/// Particle effects for the sky hazards, built from code (nothing to assign):
/// puffs and sparks (Burst), drifting feathers (Feathers) and a smoke trail (SmokeTrail).
/// Uses the Sprites/Default shader, which works in the Built-in pipeline and in URP.
/// </summary>
public static class HazardFx
{
    static Material puffMaterial, featherMaterial;

    // ------------------------------------------------------------------ materials

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

        puffMaterial = MakeMaterial(tex);
        return puffMaterial;
    }

    // A single feather: a narrow leaf shape with a darker spine and faint stripes.
    static Material FeatherMaterial()
    {
        if (featherMaterial) return featherMaterial;

        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            float v = y / (float)(n - 1);
            float half = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.8f) * 0.13f;   // widest in the middle
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)(n - 1);
                float d = Mathf.Abs(u - 0.5f);
                float a = Mathf.Clamp01((half - d) / 0.02f);
                float shade = 0.92f + 0.08f * Mathf.Sin(v * 40f + d * 80f);
                if (d < 0.01f) shade *= 0.75f;                               // the spine
                tex.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        featherMaterial = MakeMaterial(tex);
        return featherMaterial;
    }

    static Material MakeMaterial(Texture2D tex)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (!sh) return null;
        var m = new Material(sh);
        m.mainTexture = tex;
        return m;
    }

    // ------------------------------------------------------------------ puffs and sparks

    /// <summary>A one-shot burst of round particles (puff, sparks, flash).</summary>
    public static void Burst(Vector3 position, int count, float lifeMin, float lifeMax, float speedMin, float speedMax,
                             float sizeMin, float sizeMax, Color colourA, Color colourB, float spread, float cleanupAfter)
    {
        if (count <= 0) return;

        var go = new GameObject("HazardBurst");
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
        if (spread > 0f) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = spread; }
        else shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOut(0.5f));

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
        Object.Destroy(go, cleanupAfter + 2f);
    }

    // ------------------------------------------------------------------ feathers

    /// <summary>Feathers that burst out, slow quickly and then drift and flutter down where the bird was.</summary>
    public static void Feathers(Vector3 position, Vector3 pushVelocity, int count, float size)
    {
        if (count <= 0) return;

        var go = new GameObject("Feathers");
        go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 10;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f * size, 0.9f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 1f), new Color(0.78f, 0.80f, 0.85f, 1f));
        main.gravityModifier = 0.03f;                 // they float, they don't drop

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f * size;

        // Dragged along by the ship for a moment, then left behind
        var carry = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 0.15f), new Keyframe(1f, 0f));
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(pushVelocity.x * 0.3f, carry);
        vel.y = new ParticleSystem.MinMaxCurve(pushVelocity.y * 0.3f, carry);
        vel.z = new ParticleSystem.MinMaxCurve(pushVelocity.z * 0.3f, carry);

        // Air drag: the burst slows down quickly
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = new ParticleSystem.MinMaxCurve(1.2f);
        limit.dampen = 0.12f;

        // Flutter
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.8f);
        noise.frequency = 0.5f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.6f);

        var spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOut(0.75f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Material m = FeatherMaterial();
        if (m) r.sharedMaterial = m;

        ps.Play();
        Object.Destroy(go, 8f);
    }

    // ------------------------------------------------------------------ smoke

    /// <summary>A smoke trail that follows a falling plane and keeps hanging in the air behind it.</summary>
    public static void SmokeTrail(Transform follow, float seconds, float size)
    {
        var go = new GameObject("SmokeTrail");
        go.transform.position = follow.position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f * size, 2.4f * size);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.18f, 0.2f, 0.85f), new Color(0.35f, 0.35f, 0.38f, 0.85f));

        var emission = ps.emission;
        emission.rateOverTime = 35f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f * size;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOut(0.3f));

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Material m = PuffMaterial();
        if (m) r.sharedMaterial = m;

        var follower = go.AddComponent<HazardFollower>();
        follower.target = follow;
        follower.stopAfter = seconds;
        follower.ps = ps;

        ps.Play();
    }

    // Opaque for the first part of a particle's life, then fading to nothing.
    static Gradient FadeOut(float fadeStart)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}

/// <summary>Moves a smoke trail along with its plane, and lets the smoke fade once the plane is gone.</summary>
public class HazardFollower : MonoBehaviour
{
    public Transform target;
    public float stopAfter = 5f;
    public ParticleSystem ps;

    float age;
    bool stopped;

    void LateUpdate()
    {
        if (stopped) return;

        age += Time.deltaTime;
        if (target) transform.position = target.position;

        if (!target || age > stopAfter)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            stopped = true;
            Destroy(gameObject, 3f);
        }
    }
}

/// <summary>Removes a knocked-out plane after its time is up, shrinking it away over the last second.</summary>
public class HazardLife : MonoBehaviour
{
    public float life = 7f;
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
