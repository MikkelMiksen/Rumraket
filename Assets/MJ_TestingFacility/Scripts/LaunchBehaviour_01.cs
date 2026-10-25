using UnityEngine;

public class LaunchBehaviour_01 : MonoBehaviour
{
    [SerializeField] private float Fuel_Amount, Fuel_Capacity, Engine_Thrust_Power, Engine_Consumption_Factor;
    private int boosterCount = 0;
    private float baseFuelCapacity;
    [SerializeField] private Rigidbody rb;

    public int MkUpgradeLevel { get; private set; } = 0; // Starts at 0, max 5
    public int BoosterUpgradeLevel { get; private set; } = 0; // Starts at 0, max 5

    // How much one upgrade level adds
    [SerializeField] private float fuelUpgradeAmount = 1f;
    [SerializeField] private float engineUpgradeAmount = 1f;

    // Gravity strength once the tank is empty (2 = twice normal gravity)
    [SerializeField] private float emptyTankGravityMultiplier = 2f;

    [Header("Steering")]
    [Tooltip("World axis the rocket spins around. Z = side view, camera looking along +Z.")]
    [SerializeField] private Vector3 steerAxis = Vector3.forward;
    [Tooltip("Spin acceleration at full input, in radians per second squared.")]
    [SerializeField] private float steerTorque = 2f;
    [Tooltip("Tick this if the rocket spins the wrong way.")]
    [SerializeField] private bool invertSteering;

    [Header("Pitch (W / S)")]
    [Tooltip("The other horizontal world axis. X when the camera looks along +Z.")]
    [SerializeField] private Vector3 pitchAxis = Vector3.right;
    [Tooltip("Spin acceleration at full input, in radians per second squared.")]
    [SerializeField] private float pitchTorque = 2f;
    [Tooltip("Tick this if W tips the nose towards the camera instead of away.")]
    [SerializeField] private bool invertPitch;

    private bool isFlying;
    private float steerInput;   // -1 (left) to +1 (right)
    private float pitchInput;   // -1 (S) to +1 (W)

    public float GetETPAmount() => Engine_Thrust_Power;
    public float GetFuelCapacity() => Fuel_Capacity;
    public float GetFuelAmount() => Fuel_Amount;
    public float GetFuelUpgradeAmount() => fuelUpgradeAmount;
    public float GetEngineUpgradeAmount() => engineUpgradeAmount;

    // Setters: replace the value (so callers pass the new total, e.g. Get...() + 1)
    public void SetFuelCapacity(float newCapacity)
    {
        baseFuelCapacity = newCapacity;
        UpdateFuelCapacity();
    }
    public void SetETP(float newEtp) => Engine_Thrust_Power = newEtp;

    public void SetBoosterCount(int count)
    {
        boosterCount = count;
        UpdateFuelCapacity();
    }

    public void IncrementMkUpgradeLevel()
    {
        if (MkUpgradeLevel < 5) MkUpgradeLevel++;
    }

    public void IncrementBoosterUpgradeLevel()
    {
        if (BoosterUpgradeLevel < 5) BoosterUpgradeLevel++;
    }

    private void UpdateFuelCapacity()
    {
        // Each booster multiplies the capacity by 1.25 (1.25x, 1.56x, 1.95x, 2.44x)
        Fuel_Capacity = baseFuelCapacity * Mathf.Pow(1.25f, boosterCount);
    }

    // Applies one upgrade level. GameManager checks the cost before calling these.
    public void OnFuelUpgrade()
    {
        // Change the base value, so boosters keep multiplying the upgraded capacity
        baseFuelCapacity += fuelUpgradeAmount;
        UpdateFuelCapacity();
    }

    public void OnEngineUpgrade()
    {
        Engine_Thrust_Power += engineUpgradeAmount;
    }

    // GameManager passes the horizontal input here every frame while flying
    // x = horizontal (A/D), y = vertical (W/S)
    public void SetSteerInput(Vector2 input)
    {
        steerInput = Mathf.Clamp(input.x, -1f, 1f);
        pitchInput = Mathf.Clamp(input.y, -1f, 1f);
    }

    // GameManager calls this when the Flying state starts
    public void StartEngine()
    {
        isFlying = true;
    }

    // GameManager calls this before every run (and when returning to the pad)
    public void ResetRun()
    {
        // Refill using the current, upgraded capacity
        Fuel_Amount = Fuel_Capacity;

        // Engine off until StartEngine is called
        isFlying = false;
        steerInput = 0f;
        pitchInput = 0f;

        // Remove leftover motion
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // Awake (not Start) so rb is ready before GameManager's first reset
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        baseFuelCapacity = Fuel_Capacity;
        Fuel_Amount = Fuel_Capacity;
    }

    // Physics belongs in FixedUpdate
    void FixedUpdate()
    {
        // Tank empty: add the missing share of gravity so the total is
        // emptyTankGravityMultiplier x normal gravity. This sits above the
        // isFlying check because the engine is already off by this point.
        if (Fuel_Amount <= 0f)
        {
            Vector3 extraGravity = Physics.gravity * (emptyTankGravityMultiplier - 1f);
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }

        if (!isFlying)
            return;

        if (Fuel_Amount <= 0)
        {
            Fuel_Amount = 0;
            isFlying = false;
            return;
        }

        Fuel_Amount -= Engine_Consumption_Factor * Time.fixedDeltaTime;
        rb.AddForce(transform.up * (Engine_Thrust_Power * 15));

        // Steering: horizontal input spins the rocket around steerAxis.
        // Acceleration mode ignores the rocket's mass, so the feel stays the same
        // however the colliders and parts change.
        // Right input = clockwise when the camera looks along the axis.
        float direction = invertSteering ? 1f : -1f;
        Vector3 torque = steerAxis.normalized * (steerInput * direction * steerTorque);
        rb.AddTorque(torque, ForceMode.Acceleration);

        // Pitch: vertical input spins the rocket around the other horizontal axis.
        // W tips the nose away from a camera looking along +Z.
        float pitchDirection = invertPitch ? -1f : 1f;
        Vector3 pitch = pitchAxis.normalized * (pitchInput * pitchDirection * pitchTorque);
        rb.AddTorque(pitch, ForceMode.Acceleration);
    }
}