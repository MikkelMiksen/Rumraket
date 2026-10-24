using UnityEngine;

/// <summary>
/// Test menu: one on-screen button that upgrades the rocket one tier per click.
///   Start   -> Stage3FlightDeck only
///   Click 1 -> + Stage2Rocket and the Stage3ThrusterProtection halves
///   Click 2 -> + Stage1Rocket and the Stage2ThrusterProtection halves
///
/// The button is drawn from code (OnGUI), so there is no Canvas, EventSystem or UI package to set up.
/// Swap it for your real UI later: just call Upgrade() from whatever button you build.
/// </summary>
public class UpgradeMenu : MonoBehaviour
{
    public RocketAssembly rocket;

    [Header("Launch pad")]
    [Tooltip("Optional. An empty object (or the pad) whose height is the TOP of the launch pad. " +
             "Empty = use the rocket's starting height, i.e. wherever you placed it on the pad.")]
    public Transform pad;
    [Tooltip("Seconds the rocket takes to rise as a new stage is added.")]
    public float riseTime = 0.8f;

    [Header("Camera (optional) - keeps the whole stack in view as it grows")]
    public Camera cam;
    [Tooltip("Direction from the rocket towards the camera.")]
    public Vector3 viewDirection = new Vector3(0.6f, 0.15f, -1f);
    [Tooltip("Top of the flight deck in the rocket's local space (the model is ~38.4 tall).")]
    public float rocketTopLocalY = 38.4f;
    public float distancePerUnit = 1.5f;
    public float minDistance = 12f;
    public float cameraSmooth = 3f;

    int tier = 1;
    float padY;
    float riseVelocity;
    bool frozen;   // true after a TEST drop: leave the rocket where it is

    void Start()
    {
        padY = pad ? pad.position.y : rocket.transform.position.y;

        // The whole rocket now rises instead, so new stages don't also slide in from below.
        rocket.attachFrom = Vector3.zero;

        rocket.SetTier(tier, false);   // game starts with the flight deck only
        SetRocketHeight(true);         // put its bottom on the pad straight away
        SnapCamera();
    }

    // Keeps the bottom of the lowest attached stage on the pad.
    void SetRocketHeight(bool instant)
    {
        float target = padY - rocket.BottomLocalY * rocket.transform.lossyScale.y;
        Vector3 p = rocket.transform.position;
        p.y = instant ? target : Mathf.SmoothDamp(p.y, target, ref riseVelocity, riseTime);
        rocket.transform.position = p;
    }

    public void Upgrade()
    {
        if (tier >= rocket.StageCount) return;
        tier++;
        rocket.SetTier(tier, true);    // new stage slides in, shell halves clamp shut
    }

    // ---- button ----------------------------------------------------------

    void OnGUI()
    {
        float h = Screen.height * 0.07f;
        float w = Screen.width * 0.25f;
        float gap = 10f;
        float y = Screen.height - h - 20f;
        var style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(h * 0.4f) };

        // 1) Add the next stage
        bool maxed = tier >= rocket.StageCount;
        GUI.enabled = !maxed && !frozen;
        string label = maxed ? "Fully upgraded" : "Upgrade rocket (" + tier + "/" + rocket.StageCount + ")";
        if (GUI.Button(new Rect(20f, y, w, h), label, style)) Upgrade();
        y -= h + gap;

        // 2) Boosters: 0 -> 2 -> 4 -> 0 (needs Stage 1, which they are strapped to)
        GUI.enabled = rocket.BoostersAvailable && !frozen;
        if (GUI.Button(new Rect(20f, y, w, h), "Boosters: " + rocket.BoosterCount + "/" + rocket.BoosterMax, style))
            rocket.SetBoosterCount(NextBoosterCount(), true);
        y -= h + gap;

        // 3) Test buttons, so you can see the drops without the flight code
        GUI.enabled = true;
        if (GUI.Button(new Rect(20f, y, w, h), "drop stage", style)) TestDropStage();
        y -= h + gap;
        if (GUI.Button(new Rect(20f, y, w, h), "drop boosters", style)) TestDropBoosters();
        y -= h + gap;
        if (GUI.Button(new Rect(20f, y, w, h), "reset rocket", style)) TestReset();
    }

    int NextBoosterCount()
    {
        int next = rocket.BoosterCount + 2;
        return next > rocket.BoosterMax ? 0 : next;
    }

    // Drops the bottom stage; any boosters still on Stage 1 fall away with it.
    void TestDropStage()
    {
        frozen = true;   // stop keeping the base on the pad, the rocket is "in flight"
        rocket.SeparateLowestStage();
    }

    // Drops only the boosters (what happens when they run out of fuel before the stage does).
    void TestDropBoosters()
    {
        frozen = true;
        rocket.DropBoosters();
    }

    void TestReset()
    {
        frozen = false;
        rocket.SetTier(tier, false);
    }

    // ---- camera ----------------------------------------------------------

    void LateUpdate()
    {
        if (!frozen) SetRocketHeight(false);

        if (!cam) return;
        Vector3 pos; Quaternion rot;
        Target(out pos, out rot);
        float k = 1f - Mathf.Exp(-cameraSmooth * Time.deltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, pos, k);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, k);
    }

    void SnapCamera()
    {
        if (!cam) return;
        Vector3 pos; Quaternion rot;
        Target(out pos, out rot);
        cam.transform.SetPositionAndRotation(pos, rot);
    }

    void Target(out Vector3 pos, out Quaternion rot)
    {
        Vector3 bottom = rocket.transform.TransformPoint(0f, rocket.BottomLocalY, 0f);
        Vector3 top = rocket.transform.TransformPoint(0f, rocketTopLocalY, 0f);
        Vector3 mid = (bottom + top) * 0.5f;
        float dist = Mathf.Max(minDistance, (top - bottom).magnitude * distancePerUnit);
        pos = mid + viewDirection.normalized * dist;
        rot = Quaternion.LookRotation(mid - pos);
    }
}