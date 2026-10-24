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

    [Header("Upgrade / attach animation")]
    [Tooltip("Where a new stage slides in from (local units, relative to its assembled position).")]
    public Vector3 attachFrom = new Vector3(0f, -12f, 0f);
    public float attachTime = 0.9f;
    [Tooltip("How far the shell halves start apart before clamping shut.")]
    public float shellOpenDistance = 20f;
    public float shellClampTime = 1f;

    [Header("Separation (flight)")]
    public float stageKickSpeed = 3f;       // spent stage pushed backwards (m/s)
    public float shellPopSpeed = 6f;        // shell halves pushed outwards (m/s)
    public float shellSpin = 3f;            // tumble (rad/s)
    public float shellPopDelay = 0.15f;     // pause between separation and the shell bursting open
    public float debrisLifetime = 6f;
    [Tooltip("Off = debris ignores everything (safe, no bumping into the rocket). On = debris gets a convex MeshCollider.")]
    public bool debrisCollides = false;
    [Tooltip("Only needed if you move the rocket by script instead of a Rigidbody: set this every frame to the rocket's velocity so debris keeps drifting with it.")]
    public Vector3 externalVelocity;

    public event Action<int> StageAttached;   // stage index
    public event Action<int> StageSeparated;  // stage index (hook engine ignition / sound / VFX here)

    struct Rest
    {
        public Transform parent;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
    }

    readonly Dictionary<Transform, Rest> rests = new Dictionary<Transform, Rest>();
    readonly HashSet<Transform> addedColliders = new HashSet<Transform>();
    int lowest;   // index of the lowest stage that is currently attached

    public int Tier { get { return stages.Count - lowest; } }
    public int StageCount { get { return stages.Count; } }
    /// <summary>Index of the bottom-most stage still attached (0 = Stage 1).</summary>
    public int LowestIndex { get { return lowest; } }

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
    // Runs when you add the component: fills the list from the names in the Blender file.
    void Reset()
    {
        string[] bodies = { "Stage1Rocket", "Stage2Rocket", "Stage3FlightDeck" };
        stages.Clear();
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

    Transform FindDeep(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }
#endif

    void Awake()
    {
        foreach (var s in stages)
        {
            Record(s.body);
            foreach (var h in s.shellHalves) Record(h);
        }
        lowest = 0;
        ApplyState(0, 0);
    }

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
                if (h) StartCoroutine(ClampShell(h));
    }

    IEnumerator ClampShell(Transform half)
    {
        Rest rest = rests[half];
        half.gameObject.SetActive(true);
        Vector3 outward = OutwardDir(half) * shellOpenDistance;
        Vector3 open = half.parent ? half.parent.InverseTransformVector(outward) : outward;

        for (float t = 0f; t < 1f; t += Time.deltaTime / shellClampTime)
        {
            float e = t * t * (3f - 2f * t);                         // smoothstep
            half.localPosition = rest.pos + open * (1f - e);
            yield return null;
        }
        half.localPosition = rest.pos;

        shellOpenDistance *= 2f; // next time a stage is added, the shell halves will start further apart
    }

    // ------------------------------------------------------------------ flight: staging

    /// <summary>Drops the bottom stage. Returns false if only the flight deck is left.</summary>
    public bool SeparateLowestStage()
    {
        if (lowest >= stages.Count - 1) return false;

        int index = lowest;
        Stage gone = stages[index];
        Stage next = stages[index + 1];

        // 1) Spent stage falls away backwards.
        if (gone.body)
            Release(gone.body, -transform.up * stageKickSpeed, Random.insideUnitSphere * 0.3f);

        // 2) The shell that was hiding the next engine bursts open a moment later.
        foreach (var h in next.shellHalves)
            if (h && h.gameObject.activeSelf) StartCoroutine(PopShell(h));

        lowest++;
        StageSeparated?.Invoke(index);
        return true;
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
    }
}