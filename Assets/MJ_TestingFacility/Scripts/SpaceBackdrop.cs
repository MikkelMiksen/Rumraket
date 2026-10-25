using UnityEngine;

/// <summary>
/// Procedural background from the launch pad up into space (Built-in Render Pipeline).
///
/// One number drives everything: the rocket's height above the pad.
///   * the sky goes from blue to black
///   * stars and a faint nebula fade in
///   * clouds drift past low down
///   * planets fade in, slide down the sky and grow as you climb
///   * sunlight and ambient light adjust
///
/// Setup: put this on an empty object standing at the launch pad (scale 1), then either
///   - assign Target (the ship) and it works out the height itself, or
///   - call SetHeight(heightAboveThePad) every frame from your game code.
/// </summary>
[DefaultExecutionOrder(1000)]   // after the camera has moved this frame
public class SpaceBackdrop : MonoBehaviour
{
    // ------------------------------------------------------------------ planets

    [System.Serializable]
    public class Planet
    {
        public string name = "Planet";

        [Header("Where it hangs in the sky")]
        [Tooltip("Direction around you, in degrees.")]
        public float azimuth = 30f;
        [Tooltip("Degrees above the horizon at the launch pad.")]
        public float elevation = 10f;
        public float distance = 600f;
        public float radius = 150f;
        [Tooltip("How much bigger it gets by orbit. 0 = never, 1 = twice the size.")]
        public float growth = 0.5f;
        [Tooltip("How fast it slides down the sky as you climb.")]
        public float parallax = 0.25f;
        [Tooltip("Altitude (0..1, see the heights below) where it starts to fade in.")]
        public float appearAt = 0.3f;
        [Tooltip("Spin in degrees per second.")]
        public float spin = 1.5f;

        [Header("Look")]
        public Color colorA = new Color(0.08f, 0.25f, 0.6f);     // ocean / low
        public Color colorB = new Color(0.2f, 0.5f, 0.25f);      // land / mid
        public Color colorC = new Color(0.85f, 0.85f, 0.8f);     // peaks / high
        public Color cloudColor = Color.white;
        public Color atmosphere = new Color(0.4f, 0.7f, 1f);
        public float seed = 1f;
        public float featureScale = 3f;
        [Range(0f, 1f)] public float seaLevel = 0.5f;
        [Range(0f, 1f)] public float cloudAmount = 0.4f;
        [Tooltip("1 = a gas giant with stripes instead of land and sea.")]
        [Range(0f, 1f)] public float bands = 0f;
        public float bandFrequency = 12f;

        [System.NonSerialized] public Transform instance;
        [System.NonSerialized] public Material material;
        [System.NonSerialized] public Renderer rend;
    }

    static Planet[] DefaultPlanets()
    {
        return new Planet[]
        {
            new Planet
            {
                name = "Blue world", azimuth = -25f, elevation = 8f, distance = 650f, radius = 180f,
                growth = 0.8f, parallax = 0.3f, appearAt = 0.35f, spin = 1f, seed = 1f,
                colorA = new Color(0.08f, 0.25f, 0.6f), colorB = new Color(0.2f, 0.5f, 0.25f),
                colorC = new Color(0.85f, 0.85f, 0.8f), atmosphere = new Color(0.4f, 0.7f, 1f),
                cloudAmount = 0.45f
            },
            new Planet
            {
                name = "Red world", azimuth = 40f, elevation = 28f, distance = 700f, radius = 60f,
                growth = 0.6f, parallax = 0.2f, appearAt = 0.5f, spin = 2f, seed = 5f,
                colorA = new Color(0.35f, 0.12f, 0.08f), colorB = new Color(0.75f, 0.35f, 0.18f),
                colorC = new Color(0.9f, 0.7f, 0.5f), atmosphere = new Color(1f, 0.5f, 0.3f),
                cloudColor = new Color(1f, 0.85f, 0.7f), seaLevel = 0.35f, cloudAmount = 0.1f
            },
            new Planet
            {
                name = "Gas giant", azimuth = 110f, elevation = 15f, distance = 800f, radius = 140f,
                growth = 0.5f, parallax = 0.15f, appearAt = 0.65f, spin = 3f, seed = 9f,
                colorA = new Color(0.55f, 0.35f, 0.25f), colorB = new Color(0.9f, 0.75f, 0.55f),
                colorC = new Color(0.65f, 0.45f, 0.35f), atmosphere = new Color(1f, 0.85f, 0.6f),
                bands = 1f, bandFrequency = 14f, cloudAmount = 0f
            }
        };
    }

    // ------------------------------------------------------------------ settings

    [Header("Where the height comes from")]
    [Tooltip("Optional. If set, height = this object's Y minus the pad height. Otherwise call SetHeight().")]
    public Transform target;
    [Tooltip("Optional. Its Y is the pad height. Empty = the height of the object this script is on.")]
    public Transform pad;
    [Tooltip("Smooths jumps, for example when the rocket is put back on the pad.")]
    public float smoothTime = 0.4f;

    [Header("Heights (world units above the pad)")]
    public float atmosphereHeight = 150f;
    public float orbitHeight = 500f;
    [Tooltip("How far along the sky's blue-to-black change the 'Atmosphere' height is (0 to 1).")]
    [Range(0.05f, 0.9f)] public float atmosphereAltitude = 0.35f;

    [Header("Sky colours by altitude (0 = pad, 1 = orbit)")]
    public Gradient zenith = MakeGradient(
        new[] { 0f, 0.35f, 0.7f, 1f },
        new[] { new Color(0.22f, 0.46f, 0.88f), new Color(0.05f, 0.12f, 0.40f),
                new Color(0.005f, 0.012f, 0.06f), new Color(0f, 0f, 0.01f) });
    public Gradient horizon = MakeGradient(
        new[] { 0f, 0.35f, 0.7f, 1f },
        new[] { new Color(0.72f, 0.84f, 0.96f), new Color(0.30f, 0.50f, 0.80f),
                new Color(0.03f, 0.06f, 0.16f), new Color(0.01f, 0.02f, 0.05f) });
    public Gradient belowHorizon = MakeGradient(
        new[] { 0f, 0.35f, 0.7f, 1f },
        new[] { new Color(0.45f, 0.52f, 0.58f), new Color(0.12f, 0.20f, 0.30f),
                new Color(0.01f, 0.02f, 0.05f), new Color(0f, 0f, 0.01f) });
    public Gradient ambient = MakeGradient(
        new[] { 0f, 0.35f, 1f },
        new[] { new Color(0.50f, 0.58f, 0.70f), new Color(0.30f, 0.35f, 0.50f), new Color(0.18f, 0.20f, 0.28f) });

    [Header("Stars")]
    [Tooltip("Altitude where the first stars appear, and where they are at full brightness.")]
    public float starsStart = 0.30f;
    public float starsFull = 0.75f;
    [Range(0f, 1f)] public float starDensity = 0.5f;
    [Range(0f, 1f)] public float nebulaStrength = 0.35f;

    [Header("Sun")]
    [Tooltip("Optional. Your directional light: its direction is used for the sun in the sky and on the planets.")]
    public Light sun;
    public Vector3 sunDirection = new Vector3(0.4f, 0.45f, 0.8f);   // used if no light is assigned
    public float sunIntensityAtPad = 1.1f;
    public float sunIntensityInSpace = 1.3f;

    [Header("Clouds")]
    public int cloudCount = 45;
    [Tooltip("Highest a cloud can be above the pad.")]
    public float cloudMaxHeight = 160f;
    [Tooltip("How far from the rocket's flight path clouds can be (and the closest they come).")]
    public float cloudSpread = 90f;
    public float cloudMinDistance = 18f;
    public Vector2 cloudWidth = new Vector2(25f, 70f);
    [Range(0f, 1f)] public float cloudOpacity = 0.85f;

    [Header("Planets")]
    public Planet[] planets = DefaultPlanets();

    [Header("Shaders (filled in automatically)")]
    public Shader skyShader;
    public Shader planetShader;

    // ------------------------------------------------------------------ state

    float heightTarget, height, heightVelocity;
    float padY;
    Camera cam, configuredCam;
    Material skyMat;
    Transform root;
    Material[] cloudMats;
    Transform[] clouds;
    float lastCloudAlpha = -1f;

    /// <summary>Tell the backdrop how high the rocket is above the pad.</summary>
    public void SetHeight(float heightAbovePad) { heightTarget = heightAbovePad; }

    /// <summary>0 at the pad, 1 in orbit.</summary>
    public float Altitude01 { get { return ToAltitude01(height); } }

#if UNITY_EDITOR
    void Reset()
    {
        skyShader = Shader.Find("Rumraket/ProceduralSky");
        planetShader = Shader.Find("Rumraket/ProceduralPlanet");
    }
#endif

    float ToAltitude01(float h)
    {
        if (h <= 0f) return 0f;
        if (h < atmosphereHeight)
            return atmosphereAltitude * h / Mathf.Max(atmosphereHeight, 0.01f);
        float t = Mathf.InverseLerp(atmosphereHeight, Mathf.Max(orbitHeight, atmosphereHeight + 1f), h);
        return Mathf.Lerp(atmosphereAltitude, 1f, t);
    }

    // ------------------------------------------------------------------ setup

    void Start()
    {
        if (!skyShader) skyShader = Shader.Find("Rumraket/ProceduralSky");
        if (!planetShader) planetShader = Shader.Find("Rumraket/ProceduralPlanet");
        if (!skyShader || !planetShader)
        {
            Debug.LogError("SpaceBackdrop: the Rumraket shaders were not found. Add the .shader and .cginc files to the project.");
            enabled = false;
            return;
        }

        padY = pad ? pad.position.y : transform.position.y;
        if (target) { heightTarget = target.position.y - padY; height = heightTarget; }

        // An object with no parent, so nothing here inherits a scale or a move from the rocket.
        root = new GameObject("SpaceBackdrop_Objects").transform;

        skyMat = new Material(skyShader);
        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        BuildPlanets();
        BuildClouds();
    }

    void OnDestroy()
    {
        if (root) Destroy(root.gameObject);
    }

    void BuildPlanets()
    {
        foreach (var p in planets)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Planet_" + p.name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);

            p.material = new Material(planetShader);
            p.material.SetColor("_ColorA", p.colorA);
            p.material.SetColor("_ColorB", p.colorB);
            p.material.SetColor("_ColorC", p.colorC);
            p.material.SetColor("_CloudColor", p.cloudColor);
            p.material.SetColor("_AtmoColor", p.atmosphere);
            p.material.SetFloat("_Seed", p.seed);
            p.material.SetFloat("_Scale", p.featureScale);
            p.material.SetFloat("_SeaLevel", p.seaLevel);
            p.material.SetFloat("_CloudAmount", p.cloudAmount);
            p.material.SetFloat("_Bands", p.bands);
            p.material.SetFloat("_BandFrequency", p.bandFrequency);
            p.material.SetFloat("_Fade", 0f);

            p.rend = go.GetComponent<Renderer>();
            p.rend.sharedMaterial = p.material;
            p.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.rend.receiveShadows = false;
            p.rend.enabled = false;
            p.instance = go.transform;
        }
    }

    void BuildClouds()
    {
        if (cloudCount <= 0) return;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (!spriteShader) return;

        cloudMats = new Material[3];
        for (int i = 0; i < cloudMats.Length; i++)
        {
            cloudMats[i] = new Material(spriteShader);
            cloudMats[i].mainTexture = MakeCloudTexture(i + 1);
        }

        clouds = new Transform[cloudCount];
        Vector3 centre = transform.position;
        var rng = new System.Random(12345);

        for (int i = 0; i < cloudCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Cloud";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);

            double angle = rng.NextDouble() * Mathf.PI * 2.0;
            float radius = Mathf.Lerp(cloudMinDistance, Mathf.Max(cloudSpread, cloudMinDistance + 1f), (float)rng.NextDouble());
            float y = padY + Mathf.Lerp(10f, cloudMaxHeight, (float)rng.NextDouble());
            float width = Mathf.Lerp(cloudWidth.x, cloudWidth.y, (float)rng.NextDouble());

            go.transform.position = new Vector3(
                centre.x + (float)System.Math.Cos(angle) * radius, y,
                centre.z + (float)System.Math.Sin(angle) * radius);
            go.transform.localScale = new Vector3(width, width * 0.5f, 1f);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = cloudMats[i % cloudMats.Length];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            clouds[i] = go.transform;
        }
    }

    // A soft, puffy cloud made of a few overlapping blobs.
    static Texture2D MakeCloudTexture(int seed)
    {
        const int w = 128, h = 64;
        var rng = new System.Random(seed * 7919 + 13);
        var puffs = new Vector3[7];
        for (int i = 0; i < puffs.Length; i++)
            puffs[i] = new Vector3(0.2f + 0.6f * (float)rng.NextDouble(),
                                   0.35f + 0.3f * (float)rng.NextDouble(),
                                   0.12f + 0.14f * (float)rng.NextDouble());

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float v = y / (float)(h - 1);

                float density = 0f;
                foreach (var p in puffs)
                {
                    float dx = (u - p.x) * 2f;          // the texture is twice as wide as it is tall
                    float dy = v - p.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / p.z;
                    density += Mathf.Clamp01(1f - d);
                }

                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(density * 1.2f));
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.12f))
                           * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / 0.12f))
                           * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.15f))
                           * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.15f));
                float shade = Mathf.Lerp(0.78f, 1f, v);   // a slightly darker underside

                tex.SetPixel(x, y, new Color(shade, shade, shade, a * edge * 0.9f));
            }
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return tex;
    }

    void ConfigureCamera()
    {
        configuredCam = cam;
        cam.clearFlags = CameraClearFlags.Skybox;

        float farthest = 0f;
        foreach (var p in planets) farthest = Mathf.Max(farthest, p.distance + p.radius * 3f);
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, farthest * 1.2f);
    }

    // ------------------------------------------------------------------ every frame

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam || skyMat == null) return;
        if (cam != configuredCam) ConfigureCamera();

        if (target) heightTarget = target.position.y - padY;
        height = Mathf.SmoothDamp(height, heightTarget, ref heightVelocity, Mathf.Max(smoothTime, 0.0001f));

        float a = ToAltitude01(height);

        Vector3 sunDir = sun ? -sun.transform.forward : sunDirection.normalized;
        Shader.SetGlobalVector("_RumraketSunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));

        UpdateSky(a, sunDir);
        UpdateLighting(a);
        UpdatePlanets(a);
        UpdateClouds(a);
    }

    void UpdateSky(float a, Vector3 sunDir)
    {
        skyMat.SetColor("_ZenithColor", zenith.Evaluate(a));
        skyMat.SetColor("_HorizonColor", horizon.Evaluate(a));
        skyMat.SetColor("_GroundColor", belowHorizon.Evaluate(a));
        skyMat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
        skyMat.SetFloat("_StarVisibility", Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(starsStart, Mathf.Max(starsFull, starsStart + 0.01f), a)));
        skyMat.SetFloat("_StarDensity", starDensity);
        skyMat.SetFloat("_NebulaStrength", nebulaStrength);
    }

    void UpdateLighting(float a)
    {
        RenderSettings.ambientLight = ambient.Evaluate(a);
        if (sun) sun.intensity = Mathf.Lerp(sunIntensityAtPad, sunIntensityInSpace, a);
    }

    void UpdatePlanets(float a)
    {
        foreach (var p in planets)
        {
            if (p.instance == null) continue;

            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.appearAt, p.appearAt + 0.25f, a));
            p.rend.enabled = fade > 0.01f;
            if (!p.rend.enabled) continue;
            p.material.SetFloat("_Fade", fade);

            // Fixed direction around the camera, sliding down slowly as the rocket climbs.
            Vector3 dir = Quaternion.Euler(-p.elevation, p.azimuth, 0f) * Vector3.forward;
            p.instance.position = cam.transform.position + dir * p.distance + Vector3.down * (height * p.parallax);
            p.instance.localScale = Vector3.one * (p.radius * 2f * (1f + p.growth * a));
            p.instance.Rotate(Vector3.up, p.spin * Time.deltaTime, Space.Self);
        }
    }

    void UpdateClouds(float a)
    {
        if (clouds == null) return;

        // Clouds thin out as the sky gets thin.
        float alpha = cloudOpacity * (1f - Mathf.SmoothStep(0f, atmosphereAltitude * 0.9f, a));
        bool visible = alpha > 0.01f;

        if (Mathf.Abs(alpha - lastCloudAlpha) > 0.002f)
        {
            lastCloudAlpha = alpha;
            foreach (var m in cloudMats) m.color = new Color(1f, 1f, 1f, alpha);
        }

        foreach (var c in clouds)
        {
            if (c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
            if (visible) c.rotation = cam.transform.rotation;   // flat cards that always face the camera
        }
    }

    // ------------------------------------------------------------------ helpers

    static Gradient MakeGradient(float[] times, Color[] colours)
    {
        var colourKeys = new GradientColorKey[times.Length];
        for (int i = 0; i < times.Length; i++)
            colourKeys[i] = new GradientColorKey(colours[i], times[i]);

        var g = new Gradient();
        g.SetKeys(colourKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
