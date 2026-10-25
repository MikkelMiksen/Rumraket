using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Put this on the root of the imported rocket prefab.
/// Stages are listed BOTTOM -> TOP (booster first, flight deck last).
/// The flight deck is always present; upgrades add stages underneath it.
///
///   SetTier(3)               -> full rocket, new stages slide in and the shell halves clamp shut
///   SeparateLowestStage()    -> in flight: drops the bottom stage and blows off the shell
///                               that was covering the next stage's engine
///   SetTier(n, false)        -> instant (use for loading a save / returning to the menu)
///
/// Assumes the model is in its fully ASSEMBLED pose when the prefab loads.
/// </summary>
public class RocketAssembly : MonoBehaviour
{
    [Serializable]
    public class Stage
    {
        public string name = "Stage";
        [Tooltip("The stage mesh. Nozzle rings/connectors should be its children so they travel with it.")]
        public Transform body;
        [Tooltip("The clamshell halves covering THIS stage's engine. Only visible while a stage is attached below it.")]
        public Transform[] shellHalves = new Transform[0];
    }

    public List<Stage> stages = new List<Stage>();

    [Header("Strap-on boosters (optional)")]
    [Tooltip("The booster objects (Stage1Booster_1..4). They are shown and dropped as a group.")]
    public Transform[] boosters = new Transform[0];
    [Tooltip("The stage the boosters are strapped to (0 = Stage 1). They are only visible while it is attached.")]
    public int boostersStage = 0;
    [Tooltip("How far out a booster starts when it clamps onto the stage.")]
    public float boosterOpenDistance = 1.5f;
    public float boosterClampTime = 0.45f;
    [Tooltip("Speed a booster is pushed outwards when dropped (m/s).")]
    public float boosterPopSpeed = 4f;
    public float boosterSpin = 2f;
    public float boosterPopDelay = 0.1f;

    [Header("Upgrade / attach animation")]
    [Tooltip("Where a new stage slides in from (local units, relative to its assembled position).")]
    public Vector3 attachFrom = new Vector3(0f, -12f, 0f);
    public float attachTime = 0.9f;
    [Tooltip("How far the shell halves start apart before clamping shut.")]
    public float shellOpenDistance = 0.8f;
    public float shellClampTime = 0.3f;

    [Header("Separation (flight)")]
    public float stageKickSpeed = 3f;       // spent stage pushed backwards (m/s)
    public float shellPopSpeed = 6f;        // shell halves pushed outwards (m/s)
    public float shellSpin = 3f;            // tumble (rad/s)
    public float shellPopDelay = 0.2f;      // pause between separation and the shell bursting open
    [Tooltip("The stage lets go this long after separation starts, so the pyro bolts and ignition are seen first.")]
    public float stageReleaseDelay = 0.35f;
    public float debrisLifetime = 6f;
    [Tooltip("Off = debris ignores everything (safe, no bumping into the rocket). On = debris gets a convex MeshCollider.")]
    public bool debrisCollides = false;
    [Tooltip("Only needed if you move the rocket by script instead of a Rigidbody: set this every frame to the rocket's velocity so debris keeps drifting with it.")]
    public Vector3 externalVelocity;

    public event Action<int> StageAttached;   // stage index
    public event Action<int> StageSeparated;  // stage index (hook engine ignition / sound / VFX here)
    public event Action BoostersDropped;      // the strap-on boosters were jettisoned
    public event Action<int> SeparationStarted;   // stage index: fires first, before anything moves (effects start here)

    struct Rest
    {
        public Transform parent;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
    }

    readonly Dictionary<Transform, Rest> rests = new Dictionary<Transform, Rest>();
    readonly HashSet<Transform> addedColliders = new HashSet<Transform>();
    readonly HashSet<Transform> released = new HashSet<Transform>();   // parts that have been dropped
    int lowest;   // index of the lowest stage that is currently attached
    int boosterCount;     // how many boosters the player has (0..boosters.Length)
    int[] boosterRank;    // order boosters appear in: opposite pairs first

    public int Tier { get { return stages.Count - lowest; } }
    public int StageCount { get { return stages.Count; } }
    /// <summary>Index of the bottom-most stage still attached (0 = Stage 1).</summary>
    public int LowestIndex { get { return lowest; } }

    /// <summary>How many boosters are currently owned (not necessarily visible if their stage is missing).</summary>
    public int BoosterCount { get { return boosterCount; } }
    public int BoosterMax { get { return boosters.Length; } }
    /// <summary>True when the stage the boosters are strapped to is attached.</summary>
    public bool BoostersAvailable { get { return boosters.Length > 0 && lowest <= boostersStage; } }

    /// <summary>
    /// Local height of the bottom of the current stack (the lowest attached stage's pivot).
    /// With only the flight deck bought this is ~34, so move your visual root down by this
    /// amount to keep the rocket's base on the launch pad.
    /// </summary>
    public float BottomLocalY
    {
        get
        {
            Transform b = stages[lowest].body;
            return b && rests.ContainsKey(b) ? rests[b].pos.y : 0f;
        }
    }

    // ------------------------------------------------------------------ setup

#if UNITY_EDITOR
    // Runs when you add the component (or right-click > Reset): re-fills everything from the object names.
    void Reset() { AutoFill(true, true); }
#endif

    // Fills the lists from the object names in the Blender file.
    // force = replace even if already filled; otherwise only an EMPTY list is filled in.
    void AutoFill(bool forceStages, bool forceBoosters)
    {
        if (forceStages || stages == null || stages.Count == 0)
        {
            string[] bodies = { "Stage1Rocket", "Stage2Rocket", "Stage3FlightDeck" };
            stages = new List<Stage>();
            for (int i = 0; i < bodies.Length; i++)
            {
                var stage = new Stage { name = bodies[i], body = FindDeep(bodies[i]) };
                var halves = new List<Transform>();
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Stage" + (i + 1) + "ThrusterProtection")) halves.Add(t);
                stage.shellHalves = halves.ToArray();
                stages.Add(stage);
            }
        }

        if (forceBoosters || boosters == null || boosters.Length == 0)
        {
            var found = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Stage1Booster_")) found.Add(t);
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            boosters = found.ToArray();
            boostersStage = 0;
        }
    }

    Transform FindDeep(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    void Awake()
    {
        AutoFill(false, false);   // finds the stages and boosters by name if the lists were left empty

        foreach (var s in stages)
        {
            Record(s.body);
            foreach (var h in s.shellHalves) Record(h);
        }
        foreach (var b in boosters) Record(b);
        BuildBoosterRanks();

        lowest = 0;
        ApplyState(0, 0);
    }

    // With an even number of boosters, they appear in opposite pairs (0 & 2, then 1 & 3) so the rocket stays balanced.
    void BuildBoosterRanks()
    {
        int n = boosters.Length;
        boosterRank = new int[n];
        for (int k = 0; k < n; k++)
        {
            int idx = (n % 2 == 0) ? ((k % 2 == 0) ? k / 2 : n / 2 + k / 2) : k;
            boosterRank[idx] = k;
        }
    }

    bool BoosterWanted(int idx) { return boosterRank[idx] < boosterCount; }

    bool IsAttached(Transform t) { return t.gameObject.activeSelf && !released.Contains(t); }

    void Record(Transform t)
    {
        if (!t || rests.ContainsKey(t)) return;
        rests[t] = new Rest { parent = t.parent, pos = t.localPosition, rot = t.localRotation, scale = t.localScale };
    }

    // ------------------------------------------------------------------ upgrades

    /// <summary>tier = number of stages on the rocket (1 = flight deck only ... StageCount = everything).</summary>
    public void SetTier(int tier, bool animate = true)
    {
        tier = Mathf.Clamp(tier, 1, stages.Count);
        int newLowest = stages.Count - tier;
        int oldLowest = lowest;

        RestoreAll();   // also puts back anything that was blown off in flight
        lowest = newLowest;

        if (!animate || newLowest >= oldLowest)
        {
            ApplyState(newLowest, newLowest);
            return;
        }

        // Stages that already existed keep their shells; the new ones appear when they connect.
        ApplyState(newLowest, oldLowest);
        float delay = 0f;
        for (int i = oldLowest - 1; i >= newLowest; i--)   // top-most new stage connects first
        {
            if (stages[i].body) stages[i].body.gameObject.SetActive(false);
            StartCoroutine(SlideIn(i, delay));
            delay += attachTime * 0.7f;
        }
    }

    // Stage i is shown if i >= lowestAttached. Its shell halves are shown if the stage is
    // attached AND i > shellsFrom (i.e. there is a stage below it to protect the engine from).
    void ApplyState(int lowestAttached, int shellsFrom)
    {
        for (int i = 0; i < stages.Count; i++)
        {
            bool attached = i >= lowestAttached;
            if (stages[i].body) stages[i].body.gameObject.SetActive(attached);
            bool shellOn = attached && i > shellsFrom;
            foreach (var h in stages[i].shellHalves)
                if (h) h.gameObject.SetActive(shellOn);
        }

        // Boosters: shown if their stage is attached (and was already there) and the player owns this one.
        for (int i = 0; i < boosters.Length; i++)
            if (boosters[i])
                boosters[i].gameObject.SetActive(boostersStage >= shellsFrom && BoosterWanted(i));
    }

    IEnumerator SlideIn(int i, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Transform body = stages[i].body;
        if (body)
        {
            Rest rest = rests[body];
            body.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime / attachTime)
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);               // ease-out
                body.localPosition = rest.pos + attachFrom * (1f - e);
                yield return null;
            }
            body.localPosition = rest.pos;
        }

        StageAttached?.Invoke(i);

        // The stage above has a shell over its engine: close it around the new joint.
        if (i + 1 < stages.Count)
            foreach (var h in stages[i + 1].shellHalves)
                if (h) StartCoroutine(ClampIn(h, shellOpenDistance, shellClampTime));

        // Boosters strapped to this stage clamp on once it is in place.
        if (i == boostersStage)
            for (int k = 0; k < boosters.Length; k++)
                if (boosters[k] && BoosterWanted(k))
                    StartCoroutine(ClampIn(boosters[k], boosterOpenDistance, boosterClampTime));
    }

    /// <summary>A part starts out to the side and slides in against the rocket.</summary>
    IEnumerator ClampIn(Transform part, float distance, float time)
    {
        Rest rest = rests[part];
        part.gameObject.SetActive(true);
        Vector3 outward = OutwardDir(part) * distance;
        Vector3 open = part.parent ? part.parent.InverseTransformVector(outward) : outward;

        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            float e = t * t * (3f - 2f * t);                         // smoothstep
            part.localPosition = rest.pos + open * (1f - e);
            yield return null;
        }
        part.localPosition = rest.pos;
    }

    // ------------------------------------------------------------------ boosters

    /// <summary>
    /// How many boosters the player owns (0..BoosterMax). They clamp on if their stage is attached,
    /// otherwise they appear when it is. Call SetTier afterwards if boosters have been dropped.
    /// </summary>
    public void SetBoosterCount(int count, bool animate = true)
    {
        boosterCount = Mathf.Clamp(count, 0, boosters.Length);
        bool stageThere = lowest <= boostersStage;

        for (int i = 0; i < boosters.Length; i++)
        {
            Transform b = boosters[i];
            if (!b || released.Contains(b)) continue;

            if (!(stageThere && BoosterWanted(i)))
                b.gameObject.SetActive(false);
            else if (!b.gameObject.activeSelf)
            {
                if (animate) StartCoroutine(ClampIn(b, boosterOpenDistance, boosterClampTime));
                else b.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>Jettisons every attached booster at once, each pushed outwards from the rocket.</summary>
    public bool DropBoosters()
    {
        bool any = false;
        foreach (var b in boosters)
        {
            if (!b || !IsAttached(b)) continue;
            released.Add(b);            // mark now so a second call during the delay does nothing
            StartCoroutine(PopBooster(b));
            any = true;
        }
        if (any) BoostersDropped?.Invoke();
        return any;
    }

    IEnumerator PopBooster(Transform b)
    {
        yield return new WaitForSeconds(boosterPopDelay);
        Vector3 outward = OutwardDir(b);
        Vector3 velocity = (outward - transform.up * 0.15f).normalized * boosterPopSpeed;
        Vector3 spin = Vector3.Cross(transform.up, outward) * boosterSpin + Random.insideUnitSphere * 0.5f;
        Release(b, velocity, spin);
    }

    // ------------------------------------------------------------------ flight: staging

    /// <summary>Drops the bottom stage. Returns false if only the flight deck is left.</summary>
    public bool SeparateLowestStage()
    {
        if (lowest >= stages.Count - 1) return false;

        int index = lowest;
        Stage gone = stages[index];
        Stage next = stages[index + 1];

        SeparationStarted?.Invoke(index);   // pyro bolts / ignition effects begin now

        // 1) The spent stage (and any boosters still on it) lets go a moment later.
        StartCoroutine(ReleaseStage(gone, index));

        // 2) The shell that was hiding the next engine bursts open on the way.
        foreach (var h in next.shellHalves)
            if (h && h.gameObject.activeSelf) StartCoroutine(PopShell(h));

        lowest++;                           // the next stage counts as the active one right away
        StageSeparated?.Invoke(index);
        return true;
    }

    IEnumerator ReleaseStage(Stage gone, int index)
    {
        if (stageReleaseDelay > 0f) yield return new WaitForSeconds(stageReleaseDelay);

        // Spent stage falls away backwards.
        if (gone.body)
            Release(gone.body, -transform.up * stageKickSpeed, Random.insideUnitSphere * 0.3f);

        // Boosters still strapped to this stage leave with it.
        if (index == boostersStage)
            foreach (var b in boosters)
                if (b && IsAttached(b))
                    Release(b, -transform.up * stageKickSpeed, Random.insideUnitSphere * 0.3f);
    }

    /// <summary>
    /// World-space info about the joint where stage 'index' meets the stage above it, for effects:
    /// the ring where the shell halves sit, its radius, and the point under the next stage's engine bell.
    /// </summary>
    public void GetSeam(int index, out Vector3 ringCenter, out float radius, out Vector3 enginePoint)
    {
        Stage next = stages[Mathf.Min(index + 1, stages.Count - 1)];

        bool have = false;
        Bounds shell = new Bounds(transform.position, Vector3.zero);
        foreach (var h in next.shellHalves)
        {
            if (!h) continue;
            var r = h.GetComponentInChildren<Renderer>();
            if (!r) continue;
            if (!have) { shell = r.bounds; have = true; } else shell.Encapsulate(r.bounds);
        }

        Bounds body = shell;
        if (next.body)
        {
            var r = next.body.GetComponentInChildren<Renderer>();
            if (r) body = r.bounds;
        }
        if (!have) shell = body;

        // Both points sit on the rocket's own axis.
        Vector3 up = transform.up;
        Vector3 origin = transform.position;
        ringCenter = origin + up * (Vector3.Dot(shell.min - origin, up) + 0.25f);
        enginePoint = origin + up * Vector3.Dot(body.min - origin, up);
        radius = Mathf.Max(shell.extents.x, shell.extents.z);
    }

    IEnumerator PopShell(Transform half)
    {
        yield return new WaitForSeconds(shellPopDelay);
        Vector3 outward = OutwardDir(half);
        Vector3 velocity = (outward - transform.up * 0.25f).normalized * shellPopSpeed;
        Vector3 spin = Vector3.Cross(transform.up, outward) * shellSpin + Random.insideUnitSphere;
        Release(half, velocity, spin);
    }

    void Release(Transform t, Vector3 extraVelocity, Vector3 spin)
    {
        Rigidbody rocketRb = GetComponentInParent<Rigidbody>();
        Vector3 inherited = rocketRb ? rocketRb.GetPointVelocity(t.position) : externalVelocity;

        released.Add(t);
        t.SetParent(null, true);

        Rigidbody rb;
        if (!t.TryGetComponent(out rb)) rb = t.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;

        if (debrisCollides && !t.TryGetComponent<Collider>(out _))
        {
            var mc = t.gameObject.AddComponent<MeshCollider>();
            mc.convex = true;
            addedColliders.Add(t);
        }

        rb.AddForce(inherited + extraVelocity, ForceMode.VelocityChange);
        rb.AddTorque(spin, ForceMode.VelocityChange);
        StartCoroutine(Retire(t));
    }

    IEnumerator Retire(Transform t)
    {
        yield return new WaitForSeconds(debrisLifetime);
        Vector3 s = t.localScale;
        for (float k = 0f; k < 1f; k += Time.deltaTime)              // shrink over ~1 s
        {
            t.localScale = s * (1f - k);
            yield return null;
        }
        t.gameObject.SetActive(false);   // not destroyed, so RestoreAll can bring it back
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Horizontal direction from the rocket's axis to the middle of a shell half.</summary>
    Vector3 OutwardDir(Transform half)
    {
        // Use the mesh bounds, not the pivot: your halves have their pivot on the rocket axis.
        var r = half.GetComponentInChildren<Renderer>();
        Vector3 c = (r ? r.bounds.center : half.position) - transform.position;
        c = Vector3.ProjectOnPlane(c, transform.up);
        return c.sqrMagnitude > 1e-6f ? c.normalized : half.right;
    }

    /// <summary>Puts every part back where it was in the prefab (reparent, pose, remove physics).</summary>
    public void RestoreAll()
    {
        StopAllCoroutines();
        foreach (var kv in rests)
        {
            Transform t = kv.Key;
            if (!t) continue;
            Rest r = kv.Value;

            if (t.TryGetComponent(out Rigidbody rb)) Destroy(rb);
            if (addedColliders.Contains(t) && t.TryGetComponent(out MeshCollider mc)) Destroy(mc);

            t.SetParent(r.parent, false);
            t.localPosition = r.pos;
            t.localRotation = r.rot;
            t.localScale = r.scale;
            t.gameObject.SetActive(true);
        }
        addedColliders.Clear();
        released.Clear();
    }
}
