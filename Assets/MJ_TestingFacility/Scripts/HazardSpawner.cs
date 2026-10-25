using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns birds, planes and asteroids ahead of the rocket as it climbs, so they get in its way.
/// Birds come first (near the ground), then planes (higher up), then asteroids (in space).
/// Each kind has a height range; it fades in and out at the edges of its range.
///
/// Put this on any object (for example an empty "HazardSpawner"), drag the ship into "Ship",
/// and drag your prefabs into the three lists. Nothing else is needed.
/// Hazards are aimed so they cross the ship's path at about the moment the ship gets there.
/// </summary>
public class HazardSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Band
    {
        [Tooltip("One of these is picked at random for each spawn.")]
        public GameObject[] prefabs;
        [Tooltip("Where on the journey spawning starts. 0 = the first milestone (Ground), 1 = the second (Atmosphere), 2 = the third (Orbit). 1.5 is halfway between the second and third.")]
        public float startAt = 0f;
        [Tooltip("Where on the journey spawning has stopped (same scale). Use a big number such as 99 for 'never'.")]
        public float endAt = 1f;
        [Tooltip("Over this much of the journey scale, at each end of the range, the spawn rate ramps up / down.")]
        public float fade = 0.15f;
        [Tooltip("Seconds between spawns at full rate.")]
        public float interval = 2.5f;
        [Tooltip("No more than this many of this kind alive at once.")]
        public int maxAlive = 8;
        [Tooltip("0 = they appear anywhere around the path, 1 = every one is aimed straight at the ship.")]
        [Range(0f, 1f)] public float aimedChance = 0.6f;
        [Tooltip("Number spawned together (birds like company).")]
        public int groupMin = 1;
        public int groupMax = 1;
        [Tooltip("How far apart members of a group are.")]
        public float groupSpread = 6f;

        [System.NonSerialized] public float timer;
        [System.NonSerialized] public float nextInterval;
        [System.NonSerialized] public List<GameObject> alive = new List<GameObject>();
    }

    [Header("What to follow")]
    [Tooltip("The GameManager. Its milestones decide where each kind appears, and it knows the ship. Found automatically if left empty.")]
    public GameManager gameManager;
    [Tooltip("The rocket. Empty = the GameManager's ship.")]
    public Transform ship;
    [Tooltip("The launch pad. Height is measured from its Y. Empty = height 0 is world Y 0.")]
    public Transform pad;

    [Header("Kinds (birds first, then planes, then asteroids)")]
    [Tooltip("Start / End are on the journey scale: 0 = first milestone, 1 = second, 2 = third. Change the milestones in the GameManager and these move with them.")]
    public Band birds = new Band
    {
        startAt = 0.03f, endAt = 0.73f, fade = 0.17f, interval = 2.2f, maxAlive = 10,
        aimedChance = 0.6f, groupMin = 1, groupMax = 4, groupSpread = 6f
    };
    public Band planes = new Band
    {
        startAt = 0.47f, endAt = 1.4f, fade = 0.27f, interval = 4.5f, maxAlive = 4,
        aimedChance = 0.7f, groupMin = 1, groupMax = 1
    };
    public Band asteroids = new Band
    {
        startAt = 1.29f, endAt = 99f, fade = 0.29f, interval = 2.5f, maxAlive = 10,
        aimedChance = 0.5f, groupMin = 1, groupMax = 2, groupSpread = 15f
    };

    [Header("Where they appear")]
    [Tooltip("Planes and birds fly along this axis, across the ship's path. (1,0,0) is left/right on screen for a side view.")]
    public Vector3 crossAxis = Vector3.right;
    [Tooltip("Closest and furthest ahead of the ship that hazards appear (metres).")]
    public float minAhead = 60f;
    public float maxAhead = 160f;
    [Tooltip("At high speed, hazards are placed at least this many seconds of flight ahead of the ship (a random value between the two).")]
    public float leadTimeMin = 1.5f;
    public float leadTimeMax = 3.5f;
    [Tooltip("Above this ship speed, hazards spawn more often so the sky does not feel empty when you are fast.")]
    public float referenceSpeed = 40f;
    [Tooltip("The most the spawn rate can be sped up at high speed (1 = never).")]
    public float maxRateBoost = 3f;
    [Tooltip("How far sideways a hazard starts, at most.")]
    public float maxSide = 90f;
    [Tooltip("An aimed hazard misses the ship's exact path by up to this much.")]
    public float aimError = 5f;
    [Tooltip("Spread of un-aimed hazards around the ship's path.")]
    public float looseSpread = 45f;
    [Tooltip("Spread in depth (toward / away from the camera). 0 for a flat side view.")]
    public float depthSpread = 0f;

    [Header("Climbing with the ship")]
    [Tooltip("Birds and planes drift upwards with the rocket, just a little slower, so the ship only gains on them slowly and there is time to dodge.")]
    public bool followShip = true;
    [Tooltip("How much of the ship's upward speed they keep. 0.8 = the ship closes in at 20% of its own speed. 0 = they hang in the air.")]
    [Range(0f, 0.98f)] public float followFraction = 0.9f;

    [Header("Rules")]
    [Tooltip("Nothing spawns while the ship is slower than this, so the pad stays clear at launch.")]
    public float minShipSpeed = 2f;
    [Tooltip("Hazards this far behind the ship are removed.")]
    public float cleanupBehind = 120f;

    bool useOverride;
    float overrideHeight;

    /// <summary>Optional: set the height yourself (same value you give SpaceBackdrop.SetHeight). Normally not needed.</summary>
    public void SetHeight(float heightAbovePad) { useOverride = true; overrideHeight = heightAbovePad; }

    Rigidbody shipBody;
    float currentShipSpeed;

    void Start()
    {
        if (!gameManager) gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager)
        {
            if (!ship && gameManager.ShipBody) ship = gameManager.ShipBody.transform;
            shipBody = gameManager.ShipBody;
        }

        if (ship && !shipBody) shipBody = ship.GetComponent<Rigidbody>();
        if (!shipBody && ship) shipBody = ship.GetComponentInParent<Rigidbody>();
        Prepare(birds);
        Prepare(planes);
        Prepare(asteroids);
    }

    static void Prepare(Band b)
    {
        b.nextInterval = b.interval * Random.Range(0.6f, 1.4f);
        b.timer = 0f;
    }

    float Height()
    {
        if (useOverride) return overrideHeight;
        float baseY = pad ? pad.position.y : 0f;
        return ship.position.y - baseY;
    }

    // Falls back to the same defaults the GameManager has if there is no GameManager.
    static readonly Milestone[] DefaultMilestones = new Milestone[]
    {
        new Milestone { name = "Ground", height = 0f },
        new Milestone { name = "Atmosphere", height = 150f },
        new Milestone { name = "Orbit", height = 500f }
    };

    Milestone[] Milestones()
    {
        if (gameManager && JourneyScale.IsValid(gameManager.Milestones)) return gameManager.Milestones;
        return DefaultMilestones;
    }

    void Update()
    {
        if (!ship) return;

        // Hazards only exist during a flight. Back on the pad (or in the menus) they are cleared away.
        // (GameState is compared by name so this file does not need to know where the enum lives.)
        if (gameManager && gameManager.CurrentState.ToString() != "Flying")
        {
            ClearAll(birds);
            ClearAll(planes);
            ClearAll(asteroids);
            return;
        }

        Vector3 velocity = shipBody ? shipBody.linearVelocity : Vector3.zero;
        float shipSpeed = velocity.magnitude;
        Vector3 dir = shipSpeed > 0.5f ? velocity / shipSpeed : Vector3.up;

        Cleanup(birds, dir);
        Cleanup(planes, dir);
        Cleanup(asteroids, dir);

        currentShipSpeed = shipSpeed;
        if (shipSpeed < minShipSpeed) return;

        // Where the ship is on the journey (0 = Ground, 1 = Atmosphere, 2 = Orbit...)
        float journey = JourneyScale.ToUnits(Milestones(), Height());
        Tick(birds, journey, dir, shipSpeed, false);
        Tick(planes, journey, dir, shipSpeed, false);
        Tick(asteroids, journey, dir, shipSpeed, true);
    }

    // ------------------------------------------------------------------ timing

    // How fast the ship closes in on a hazard. Hazards that climb along with the ship are approached more slowly.
    float Closing(float shipSpeed)
    {
        return followShip ? Mathf.Max(shipSpeed * (1f - followFraction), 5f) : shipSpeed;
    }

    void Tick(Band b, float journey, Vector3 dir, float shipSpeed, bool still)
    {
        if (!still) shipSpeed = Closing(shipSpeed);   // everything below is about how fast the ship catches up
        if (b.prefabs == null || b.prefabs.Length == 0) return;

        float rate = Rate(b, journey);
        if (rate <= 0.001f) return;

        // Faster ship = more hazards per second, so the same amount ends up along the way.
        float boost = Mathf.Clamp(shipSpeed / Mathf.Max(referenceSpeed, 1f), 1f, Mathf.Max(maxRateBoost, 1f));
        b.timer += Time.deltaTime * rate * boost;
        if (b.timer < b.nextInterval) return;

        b.timer = 0f;
        b.nextInterval = b.interval * Random.Range(0.6f, 1.4f);

        if (b.alive.Count >= b.maxAlive) return;

        int count = Random.Range(Mathf.Max(1, b.groupMin), Mathf.Max(b.groupMin, b.groupMax) + 1);
        Vector3 groupCentre = Vector3.zero;
        bool first = true;

        for (int i = 0; i < count && b.alive.Count < b.maxAlive; i++)
        {
            GameObject prefab = b.prefabs[Random.Range(0, b.prefabs.Length)];
            if (!prefab) continue;

            Vector3 position, flyDir;
            if (first)
            {
                PickSpawn(b, prefab, dir, shipSpeed, still, out position, out flyDir);
                groupCentre = position;
                first = false;
                SpawnOne(b, prefab, position, flyDir, still);
                lastFlyDir = flyDir;
            }
            else
            {
                // Group members follow the leader: same direction, scattered a little around it.
                Vector3 scatter = Random.insideUnitSphere * b.groupSpread;
                SpawnOne(b, prefab, groupCentre + scatter, lastFlyDir, still);
            }
        }
    }

    Vector3 lastFlyDir = Vector3.right;

    static float Rate(Band b, float j)
    {
        if (j < b.startAt || j > b.endAt) return 0f;
        float f = Mathf.Max(b.fade, 0.001f);
        float rise = Mathf.Clamp01((j - b.startAt) / f);
        float fall = Mathf.Clamp01((b.endAt - j) / f);
        float k = Mathf.Min(rise, fall);
        return Mathf.Max(0.15f, k * k * (3f - 2f * k));   // smoothstep, never completely dead inside the range
    }

    // ------------------------------------------------------------------ placing

    void PickSpawn(Band b, GameObject prefab, Vector3 dir, float shipSpeed, bool still,
                   out Vector3 position, out Vector3 flyDir)
    {
        // Axes across the ship's path.
        Vector3 lateral = Vector3.ProjectOnPlane(crossAxis, dir);
        if (lateral.sqrMagnitude < 0.01f) lateral = Vector3.Cross(dir, Vector3.forward);
        if (lateral.sqrMagnitude < 0.01f) lateral = Vector3.Cross(dir, Vector3.right);
        lateral.Normalize();
        Vector3 depth = Vector3.Cross(dir, lateral).normalized;

        bool aimed = Random.value < b.aimedChance;
        // Far enough ahead that it is in view for a moment before the ship reaches it, however fast it is going.
        float ahead = Mathf.Max(Random.Range(minAhead, maxAhead), shipSpeed * Random.Range(leadTimeMin, leadTimeMax));
        float speedScale = Mathf.Max(1f, shipSpeed / Mathf.Max(referenceSpeed, 1f));
        float side = (Random.value < 0.5f ? -1f : 1f);
        Vector3 offsetDepth = depth * Random.Range(-depthSpread, depthSpread);

        flyDir = -side * lateral;
        float hazardSpeed = 0f;
        var fh = prefab.GetComponent<FlyingHazard>();
        if (fh) hazardSpeed = fh.speed;

        float sideDistance;
        if (still || hazardSpeed < 0.1f)
        {
            // Stationary (asteroids): just sit in or near the path.
            float spread = (aimed ? aimError : looseSpread) * (aimed ? 1f : speedScale);
            Vector2 disc = Random.insideUnitCircle * spread;
            position = ship.position + dir * ahead + lateral * disc.x + depth * disc.y + offsetDepth;
            flyDir = lateral;
            return;
        }

        if (aimed)
        {
            // It crosses the ship's path at the moment the ship gets there:
            // time for the ship to travel "ahead" = time for the hazard to travel "sideDistance".
            float time = ahead / Mathf.Max(shipSpeed, 0.1f);
            sideDistance = Mathf.Clamp(hazardSpeed * time, 15f, maxSide);
            float error = Random.Range(-aimError, aimError);
            position = ship.position + dir * ahead + lateral * (side * sideDistance + error) + offsetDepth;
        }
        else
        {
            sideDistance = Random.Range(20f, maxSide);
            float spreadAhead = Random.Range(-looseSpread, looseSpread) * speedScale;
            position = ship.position + dir * (ahead + spreadAhead) + lateral * side * sideDistance + offsetDepth;
        }
    }

    void SpawnOne(Band b, GameObject prefab, Vector3 position, Vector3 flyDir, bool still)
    {
        Quaternion rotation;
        var fh = prefab.GetComponent<FlyingHazard>();

        if (fh && !still)
        {
            // Start from the prefab's own rotation (an FBX often has -90 on X built in) and turn it
            // so that its nose, as the prefab sits, ends up pointing along flyDir.
            Quaternion baseRotation = prefab.transform.rotation;
            Vector3 nose = fh.noseDirection.sqrMagnitude > 0.0001f ? fh.noseDirection.normalized : Vector3.forward;
            Vector3 noseNow = baseRotation * nose;

            if (Mathf.Abs(Vector3.Dot(noseNow, Vector3.up)) < 0.99f && Mathf.Abs(Vector3.Dot(flyDir, Vector3.up)) < 0.99f)
                rotation = Quaternion.LookRotation(flyDir, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(noseNow, Vector3.up)) * baseRotation;
            else
                rotation = Quaternion.FromToRotation(noseNow, flyDir) * baseRotation;
        }
        else
        {
            rotation = Random.rotation;   // asteroids tumble into place at a random angle
        }

        GameObject go = Instantiate(prefab, position, rotation);

        // A fast ship moves several metres between physics steps, so make the hit area a bit bigger
        // or it could jump straight over a small bird.
        var hazard = go.GetComponent<FlyingHazard>();
        if (hazard)
        {
            hazard.WidenTrigger(currentShipSpeed * Time.fixedDeltaTime * 2f);
            if (followShip && shipBody) hazard.FollowShip(shipBody, followFraction);
        }

        b.alive.Add(go);
    }

    // ------------------------------------------------------------------ cleanup

    static void ClearAll(Band b)
    {
        for (int i = 0; i < b.alive.Count; i++)
            if (b.alive[i]) Destroy(b.alive[i]);
        b.alive.Clear();
    }

    void Cleanup(Band b, Vector3 dir)
    {
        for (int i = b.alive.Count - 1; i >= 0; i--)
        {
            GameObject go = b.alive[i];
            if (!go) { b.alive.RemoveAt(i); continue; }   // destroyed by a hit or by its own timer

            float behind = Vector3.Dot(ship.position - go.transform.position, dir);
            if (behind > cleanupBehind)
            {
                Destroy(go);
                b.alive.RemoveAt(i);
            }
        }
    }
}