using MJ_TestingFacility.Scripts;
using UnityEngine;
using UnityEngine.UIElements;

public class GameManager : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private PanelRenderer panelRenderer;
    [SerializeField] private LaunchBehaviour_01 ship;
    [SerializeField] private Rigidbody shipBody;
    [SerializeField] private MJ_FlightCameraController flightCameraController;
    [SerializeField] private Transform titleAnchor;
    [Tooltip("Optional. Leave empty to run without the 3D rocket model.")]
    [SerializeField] private RocketAssembly rocket;
    [Tooltip("Object whose height is the top of the launch pad. Empty = the rocket's starting height.")]
    [SerializeField] private Transform pad;

    [Header("Tuning")]
    [SerializeField] private int startingCredits = 0;
    [SerializeField] private float creditsPerPoint = 1f;
    [SerializeField] private int maxUpgradeLevel = 5;
    [SerializeField] private int baseUpgradeCost = 100;
    [SerializeField] private float upgradeCostGrowth = 1.5f;

    [Header("Module purchases (after the last upgrade level)")]
    [Tooltip("How many module generations each part can reach. 2 = the original plus one bought upgrade.")]
    [SerializeField] private int maxModuleTier = 3;
    [Tooltip("Price of a new module = the next upgrade's price x this.")]
    [SerializeField] private float moduleBuyCostMultiplier = 5f;
    [Tooltip("A new module gives this many upgrade levels' worth of stats at once.")]
    [SerializeField] private int moduleBuyBonusSteps = 3;

    [Header("Boosters (after Mk 3 is maxed on both parts)")]
    [SerializeField] private int boosterCost = 1000;

    [Header("Rocket stages")]
    [Tooltip("Seconds the rocket takes to rise as a new stage is added.")]
    [SerializeField] private float riseTime = 0.8f;

    // Heights are in world units above the pad. Tune these to your scene's scale.
    [SerializeField]
    private Milestone[] milestones = new Milestone[]
    {
        new Milestone { name = "Ground", height = 0f },
        new Milestone { name = "Atmosphere", height = 150f },
        new Milestone { name = "Orbit", height = 500f }
    };

    public UIFunctionalityProvider ui_functionality = new();

    // ---------- Game state ----------
    private GameState currentState;
    public GameState CurrentState => currentState;
    private bool stateInitialized;

    // ---------- UI readiness ----------
    private int uiVersion;
    private bool uiReady;

    // ---------- Persistent data ----------
    private int credits;
    private int fuelLevel;
    private int engineLevel;
    private int moduleTier = 1;   // shared by fuel and engine (Mk 1, 2, 3)
    private float bestHeight;

    // ---------- Current run ----------
    private float runMaxHeight;
    private float runMaxSpeed;

    // ---------- Ship start pose ----------
    private Vector3 startPosition;
    private Quaternion startRotation;

    // ---------- Rocket visuals ----------
    private float padY;
    private float riseVelocity;
    private int rocketTier;
    private bool rocketReady;
    private bool rocketNeedsRestore;

    public Rigidbody ShipBody => shipBody;
    public LaunchBehaviour_01 Ship => ship;
    public RocketAssembly Rocket => rocket;

    private void Awake()
    {
        if (ship == null)
        {
            Debug.LogError("GameManager: 'ship' reference is not assigned in the Inspector!");
            return;
        }

        // Step 1: remember where the ship sits on the pad. This is in Awake
        // because the UI callback below can fire before Start runs.
        startPosition = ship.transform.position;
        startRotation = ship.transform.rotation;
        credits = startingCredits;

        // The height of the launch pad, so the bottom of the rocket stack can sit on it
        if (pad != null)
        {
            padY = pad.position.y;
        }
        else if (rocket != null)
        {
            padY = rocket.transform.position.y;
        }

        // Step 2: subscribe to the UI events. The events belong to
        // ui_functionality, which lives as long as this component, so this
        // only has to happen once (even if the UI is rebuilt later).
        ui_functionality.PlayClicked += OnPlayClicked;
        ui_functionality.UpgradesClicked += OnUpgradesClicked;
        ui_functionality.LaunchClicked += OnLaunchClicked;
        ui_functionality.BackClicked += OnBackClicked;
        ui_functionality.ContinueClicked += OnContinueClicked;
        ui_functionality.UpgradeBuyClicked += OnUpgradeBuyClicked;

        // Step 3: ask the PanelRenderer to tell us when its UI is ready.
        // The root element does not exist yet in Awake, OnEnable or Start.
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    private void Start()
    {
        if (rocket == null)
        {
            return;
        }

        // RocketAssembly records its parts in its own Awake, so this waits until Start.
        // The whole rocket rises when a stage is added, so stages do not also slide in from below.
        rocket.attachFrom = Vector3.zero;

        rocketTier = GetRocketTier();
        rocket.SetTier(rocketTier, false);

        rocketReady = true;
        UpdateRocketHeight(true);
        startPosition = ship.transform.position;
    }

    // Called by the PanelRenderer when the UI is first built, and again if the
    // UI is rebuilt (for example by live reload after you edit a UXML file).
    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        Debug.Log("GameManager: OnUIReload called. Version: " + version);
        // Skip if the visual tree has not been rebuilt since the last call
        if (version == uiVersion)
        {
            return;
        }

        uiVersion = version;

        // Step 1: the old elements are gone, so release them and bind the new ones
        ui_functionality.Dispose();
        ui_functionality.Init(root);
        uiReady = true;

        // Step 2: first time, enter the first state. After a rebuild, redraw the current one.
        if (!stateInitialized)
        {
            SetState(GameState.Title);
        }
        else
        {
            RefreshAllUI();
        }
    }

    private void RefreshAllUI()
    {
        ui_functionality.ShowState(currentState);
        ui_functionality.SetCredits(credits);
        RefreshUpgradeUI();
        RefreshJourney();
    }

    private void OnDestroy()
    {
        ui_functionality.PlayClicked -= OnPlayClicked;
        ui_functionality.UpgradesClicked -= OnUpgradesClicked;
        ui_functionality.LaunchClicked -= OnLaunchClicked;
        ui_functionality.BackClicked -= OnBackClicked;
        ui_functionality.ContinueClicked -= OnContinueClicked;
        ui_functionality.UpgradeBuyClicked -= OnUpgradeBuyClicked;

        panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        ui_functionality.Dispose();
    }

    // ===================================================================
    // Switch 2: what runs every frame in the current state
    // ===================================================================

    private void Update()
    {
        // The UI is not built yet during the first frames
        if (!uiReady)
        {
            return;
        }

        switch (currentState)
        {
            case GameState.Title:
                UpdateRocketHeight(false);
                break;

            case GameState.LaunchPad:
                UpdateRocketHeight(false);
                break;

            case GameState.UpgradeHub:
                UpdateRocketHeight(false);
                break;

            case GameState.Flying:
                UpdateFlight();
                break;

            case GameState.RunSummary:
                break;
        }
    }

    // ===================================================================
    // Switch 1: what runs once when a state is entered
    // ===================================================================

    private void SetState(GameState newState)
    {
        // Reject transitions that are not in the flow chart
        if (stateInitialized && !IsTransitionAllowed(currentState, newState))
        {
            Debug.LogWarning("Blocked transition " + currentState + " to " + newState);
            return;
        }

        GameState oldState = currentState;
        currentState = newState;
        stateInitialized = true;

        switch (newState)
        {
            case GameState.Title:
                break;

            case GameState.LaunchPad:
                ResetShipToPad();

                flightCameraController.enabled = true;
                
                ui_functionality.SetCredits(credits);
                RefreshJourney();
                break;

            case GameState.UpgradeHub:
                flightCameraController.enabled = true;
                
                RefreshUpgradeUI();
                break;

            case GameState.Flying:
                flightCameraController.enabled = true;

                StartRun();
                break;

            case GameState.RunSummary:
                flightCameraController.enabled = true;
                
                FinishRun();
                break;
        }

        // The page for the new state is shown last, after its data is ready
        ui_functionality.ShowState(newState);
    }

    private bool IsTransitionAllowed(GameState from, GameState to)
    {
        switch (from)
        {
            case GameState.Title:
                return to == GameState.LaunchPad;

            case GameState.LaunchPad:
                return to == GameState.UpgradeHub || to == GameState.Flying;

            case GameState.UpgradeHub:
                return to == GameState.LaunchPad;

            case GameState.Flying:
                return to == GameState.RunSummary;

            case GameState.RunSummary:
                return to == GameState.LaunchPad;
        }

        return false;
    }

    // ===================================================================
    // UI events: a click is only a request
    // ===================================================================
    public event System.Action<int> OnModuleTierChanged;

    private void OnPlayClicked() 
    { 
        Debug.Log("GameManager: OnPlayClicked received.");
        SetState(GameState.LaunchPad); 
    }
    private void OnUpgradesClicked() { SetState(GameState.UpgradeHub); }
    private void OnLaunchClicked() { StartCoroutine(LaunchDelay()); }

    private System.Collections.IEnumerator LaunchDelay()
    {
        yield return new WaitForSeconds(0.3f);
        SetState(GameState.Flying);
    }
    private void OnBackClicked() { SetState(GameState.LaunchPad); }
    private void OnContinueClicked() { SetState(GameState.LaunchPad); }

    private void OnUpgradeBuyClicked(UpgradeType type)
    {
        // The same button does two jobs: another upgrade level, or (once the
        // levels are used up) buying the next module
        if (IsBoosterEligible() && rocket.BoosterCount < rocket.BoosterMax)
        {
            BuyBooster();
        }
        else if (CanBuyModule())
        {
            BuyModule();
        }
        else
        {
            UpgradeModule(type);
        }
    }

    // ===================================================================
    // Flight
    // ===================================================================

    private void StartRun()
    {
        // Step 1: put the ship back on the pad with a full, upgraded tank
        ResetShipToPad();
        runMaxHeight = 0f;
        runMaxSpeed = 0f;

        // Step 2: camera trails the ship, engine on
        shipBody.isKinematic = false;
        ship.StartEngine();
    }

    private void UpdateFlight()
    {
        // Step 1: measure this frame
        float height = ship.transform.position.y - startPosition.y;
        float speed = shipBody.linearVelocity.magnitude;

        if (height > runMaxHeight)
        {
            runMaxHeight = height;
        }

        if (speed > runMaxSpeed)
        {
            runMaxSpeed = speed;
        }

        // Step 2: push values to the HUD
        float fuelAmount = ship.GetFuelAmount();
        float fuelCapacity = ship.GetFuelCapacity();
        float fuelPercent = 0f;

        if (fuelCapacity > 0f)
        {
            fuelPercent = fuelAmount / fuelCapacity * 100f;
        }

        ui_functionality.SetHud(speed, height, fuelPercent, fuelAmount);
        RefreshJourney();

        // Drop spent stages as the fuel runs down
        UpdateStaging(fuelAmount, fuelCapacity);

        // Step 3: the run ends the moment the tank is empty
        bool tankEmpty = fuelAmount <= 0f;

        if (tankEmpty)
        {
            SetState(GameState.RunSummary);
        }
    }

    private void FinishRun()
    {
        // Step 1: turn the run into points and credits
        int heightPoints = Mathf.FloorToInt(runMaxHeight);
        int speedPoints = Mathf.FloorToInt(runMaxSpeed);
        int totalPoints = heightPoints + speedPoints;
        int creditsEarned = Mathf.FloorToInt(totalPoints * creditsPerPoint);

        // Step 2: update best height and bank the credits
        bool isNewBest = runMaxHeight > bestHeight;

        if (isNewBest)
        {
            bestHeight = runMaxHeight;
        }

        credits += creditsEarned;

        // Step 3: show the breakdown
        ui_functionality.SetSummary(
            Mathf.FloorToInt(runMaxHeight), heightPoints,
            Mathf.FloorToInt(runMaxSpeed), speedPoints,
            totalPoints, creditsPerPoint,
            creditsEarned, isNewBest);

        rocketNeedsRestore = true;
    }

    private void ResetShipToPad()
    {
        shipBody.isKinematic = true;
        shipBody.linearVelocity = Vector3.zero;
        shipBody.angularVelocity = Vector3.zero;
        shipBody.position = startPosition;
        shipBody.rotation = startRotation;
        ship.transform.SetPositionAndRotation(startPosition, startRotation);

        ship.ResetRun();

        // Bring back any stages and shell halves that were dropped in flight
        if (rocketNeedsRestore)
        {
            RestoreRocket();
            rocketNeedsRestore = false;
        }
        UpdateRocketHeight(true);
    }

    // ===================================================================
    // Upgrades
    // ===================================================================

    private int GetLevel(UpgradeType type)
    {
        if (type == UpgradeType.FuelCapacity)
        {
            return fuelLevel;
        }

        return engineLevel;
    }

    private int GetUpgradeCost(int level)
    {
        // Each level costs more than the one before it
        float cost = baseUpgradeCost * Mathf.Pow(upgradeCostGrowth, level);
        return Mathf.RoundToInt(cost);
    }

    public int ModuleTier => moduleTier;

    // Upgrade levels count across module generations, so prices keep rising
    private int GetEffectiveLevel(UpgradeType type)
    {
        return (moduleTier - 1) * maxUpgradeLevel + GetLevel(type);
    }

    private bool CanBuyModule()
    {
        bool nextModuleExists = moduleTier < maxModuleTier;
        bool fuelMaxed = fuelLevel >= maxUpgradeLevel;
        bool engineMaxed = engineLevel >= maxUpgradeLevel;
        return nextModuleExists && fuelMaxed && engineMaxed;
    }

    // The price of the upgrade level that would have come next, times the multiplier
    private int GetModuleBuyCost()
    {
        int nextLevel = moduleTier * maxUpgradeLevel;
        return Mathf.RoundToInt(GetUpgradeCost(nextLevel) * moduleBuyCostMultiplier);
    }

    private void UpgradeModule(UpgradeType type)
    {
        // Step 1: nothing left to buy at all
        if (GetLevel(type) >= maxUpgradeLevel)
        {
            if (moduleTier < maxModuleTier)
            {
                ui_functionality.SetUpgradeMessage(type, "Max the other part to unlock Mk " + (moduleTier + 1) + ".");
            }
            else
            {
                ui_functionality.SetUpgradeMessage(type, "Fully upgraded.");
            }
            return;
        }

        // Step 2: refuse if the player cannot afford it
        int cost = GetUpgradeCost(GetEffectiveLevel(type));

        if (credits < cost)
        {
            ui_functionality.SetUpgradeMessage(type, "Not enough credits.");
            return;
        }

        // Step 3: spend, change the ship, count the level, refresh the display
        credits -= cost;
        ApplyStat(type);
        SetLevel(type, GetLevel(type) + 1);

        ClearUpgradeMessages();
        RefreshUpgradeUI();
    }

    private void BuyModule()
    {
        // Step 1: one price for both parts
        int cost = GetModuleBuyCost();

        if (credits < cost)
        {
            ui_functionality.SetUpgradeMessage(UpgradeType.FuelCapacity, "Not enough credits.");
            ui_functionality.SetUpgradeMessage(UpgradeType.EnginePower, "Not enough credits.");
            return;
        }

        credits -= cost;

        // Step 2: the stat jump of the new module is applied to BOTH parts
        for (int i = 0; i < moduleBuyBonusSteps; i++)
        {
            ApplyStat(UpgradeType.FuelCapacity);
            ApplyStat(UpgradeType.EnginePower);
        }

        // Step 3: a new module generation, both level counters start again
        moduleTier++;
        fuelLevel = 0;
        engineLevel = 0;

        OnModuleTierChanged?.Invoke(moduleTier);

        // Step 4: the module adds one stage to the rocket (plays the attach animation)
        UpdateRocketTier();

        ClearUpgradeMessages();
        RefreshUpgradeUI();
    }

    // Changes the ship's stat by one upgrade step (uses your existing methods)
    private void ApplyStat(UpgradeType type)
    {
        if (type == UpgradeType.FuelCapacity)
        {
            ship.OnFuelUpgrade();
        }
        else
        {
            ship.OnEngineUpgrade();
        }
    }

    private void SetLevel(UpgradeType type, int value)
    {
        if (type == UpgradeType.FuelCapacity)
        {
            fuelLevel = value;
        }
        else
        {
            engineLevel = value;
        }
    }

    private void ClearUpgradeMessages()
    {
        ui_functionality.SetUpgradeMessage(UpgradeType.FuelCapacity, "");
        ui_functionality.SetUpgradeMessage(UpgradeType.EnginePower, "");
    }

    private void RefreshUpgradeUI()
    {
        float fuelNow = ship.GetFuelCapacity();
        float engineNow = ship.GetETPAmount();

        // What one upgrade level does
        string fuelUpgradeEffect = fuelNow.ToString("0.#") + " L to " + (fuelNow + ship.GetFuelUpgradeAmount()).ToString("0.#") + " L";
        string engineUpgradeEffect = engineNow.ToString("0.#") + " to " + (engineNow + ship.GetEngineUpgradeAmount()).ToString("0.#");

        // What buying the next module does
        string fuelBuyEffect = "New module: +" + (moduleBuyBonusSteps * ship.GetFuelUpgradeAmount()).ToString("0.#") + " L";
        string engineBuyEffect = "New module: +" + (moduleBuyBonusSteps * ship.GetEngineUpgradeAmount()).ToString("0.#");

        RefreshCard(UpgradeType.FuelCapacity, "Fuel capacity", fuelUpgradeEffect, fuelBuyEffect);
        RefreshCard(UpgradeType.EnginePower, "Engine power", engineUpgradeEffect, engineBuyEffect);

        ui_functionality.SetCredits(credits);

        string stats = "Fuel capacity " + fuelNow.ToString("0.#") + " L   |   Engine power " + engineNow.ToString("0.#");

        if (rocket != null)
        {
            stats += "   |   Rocket stage " + rocketTier + "/" + rocket.StageCount;
        }

        ui_functionality.SetStats(stats);
    }

    // Chooses which of three modes a card is in: upgrade, buy a new module, or maxed
    private void RefreshCard(UpgradeType type, string baseName, string upgradeEffect, string buyEffect)
    {
        int level = GetLevel(type);
        int tier = moduleTier;

        string title = baseName;

        if (tier > 1)
        {
            title += " Mk " + tier;
        }

        if (level < maxUpgradeLevel)
        {
            string cost = GetUpgradeCost(GetEffectiveLevel(type)).ToString("N0") + " credits";
            ui_functionality.SetUpgradeCard(type, title, level, upgradeEffect, cost, "Upgrade", false);
        }
        else if (tier < maxModuleTier)
        {
            if (CanBuyModule())
            {
                // Both cards show the same shared purchase
                string cost = GetModuleBuyCost().ToString("N0") + " credits";
                ui_functionality.SetUpgradeCard(type, title, level, buyEffect, cost, "Buy Mk " + (tier + 1), true);
            }
            else
            {
                // This part is 5/5, the other one is not yet
                ui_functionality.SetUpgradeCard(type, title, level, "Waiting for the other part", "", "Locked", false);
            }
        }
        else if (IsBoosterEligible() && rocket.BoosterCount < rocket.BoosterMax)
        {
            string cost = boosterCost.ToString("N0") + " credits";
            ui_functionality.SetUpgradeCard(type, title, level, "Adds 1.25x fuel", cost, "Buy Booster", true);
        }
        else
        {
            ui_functionality.SetUpgradeCard(type, title, level, "Max level", "", "Maxed", false);
        }
    }

    // ===================================================================
    // Rocket visuals (stages from the 3D artist's RocketAssembly)
    // ===================================================================

    // Stage count follows the modules the player has bought
    private int GetRocketTier()
    {
        if (rocket == null)
        {
            return 1;
        }

        // The flight deck is stage 1, and every bought module adds one more
        return Mathf.Min(moduleTier, rocket.StageCount);
    }

    // After a purchase: if a new stage was earned, play the attach animation
    private void UpdateRocketTier()
    {
        if (rocket == null)
        {
            return;
        }

        int newTier = GetRocketTier();

        if (newTier == rocketTier)
        {
            return;
        }

        rocketTier = newTier;
        rocket.SetTier(rocketTier, true);
    }

    private bool IsBoosterEligible()
    {
        return moduleTier == maxModuleTier
            && fuelLevel >= maxUpgradeLevel
            && engineLevel >= maxUpgradeLevel;
    }

    private void BuyBooster()
    {
        if (credits < boosterCost)
        {
            ui_functionality.SetUpgradeMessage(UpgradeType.FuelCapacity, "Not enough credits.");
            ui_functionality.SetUpgradeMessage(UpgradeType.EnginePower, "Not enough credits.");
            return;
        }

        credits -= boosterCost;

        // The rocket model shows the booster, the ship applies the 1.25x fuel
        rocket.SetBoosterCount(rocket.BoosterCount + 1);
        ship.SetBoosterCount(rocket.BoosterCount);

        // Refresh UI
        ClearUpgradeMessages();
        RefreshUpgradeUI();
    }

    private void UpdateStaging(float fuelAmount, float fuelCapacity)
    {
        if (rocket == null || !rocketReady || fuelCapacity <= 0f)
        {
            return;
        }

        float remaining = fuelAmount / fuelCapacity;

        // A while loop, so a big drop in one frame still separates every stage that is due
        while (rocket.Tier > 1)
        {
            // With 3 stages at launch: drop the first at 2/3 fuel left, the next at 1/3
            float dropBelow = (rocket.Tier - 1) / (float)rocketTier;

            if (remaining > dropBelow)
            {
                break;
            }

            if (!rocket.SeparateLowestStage())
            {
                break;
            }

            rocketNeedsRestore = true;
        }
    }


    // Puts every part back where it was, with the stages the player owns attached
    private void RestoreRocket()
    {
        if (rocket == null || !rocketReady)
        {
            return;
        }

        rocket.SetTier(rocketTier, false);
        UpdateRocketHeight(true);
    }

    // Keeps the bottom of the lowest attached stage on the pad while the stack grows.
    // Only used outside of flight, so it never fights the Rigidbody.
    private void UpdateRocketHeight(bool instant)
    {
        if (!rocketReady)
        {
            return;
        }

        float targetY = padY - rocket.BottomLocalY * rocket.transform.lossyScale.y;
        Vector3 position = rocket.transform.position;

        if (instant)
        {
            position.y = targetY;
        }
        else
        {
            // If already close enough, just snap to target to avoid micro-adjustments
            if (Mathf.Abs(position.y - targetY) < 0.001f)
            {
                position.y = targetY;
            }
            else
            {
                position.y = Mathf.SmoothDamp(position.y, targetY, ref riseVelocity, riseTime);
            }
        }

        rocket.transform.position = position;
    }

    // ===================================================================
    // Journey progress (milestones)
    // ===================================================================

    private void RefreshJourney()
    {
        if (milestones.Length < 2)
        {
            return;
        }

        // Step 1: the furthest the player has reached, this run or ever
        float progress = Mathf.Max(bestHeight, runMaxHeight);

        // Step 2: find the segment between two milestones that contains it
        int index = 0;

        for (int i = 0; i < milestones.Length - 1; i++)
        {
            if (progress >= milestones[i].height)
            {
                index = i;
            }
        }

        Milestone from = milestones[index];
        Milestone to = milestones[index + 1];

        // Step 3: how far through that segment (0 to 100)
        float span = to.height - from.height;
        float fraction = Mathf.Clamp01((progress - from.height) / span);
        float percent = fraction * 100f;

        string text = "No flights yet";

        if (progress > 0f)
        {
            text = Mathf.RoundToInt(percent) + "% of the way to " + to.name;
        }

        ui_functionality.SetJourney(text, percent, from.name, to.name);
    }
}

// A named height the player can reach. GameManager keeps a list of these in the Inspector.
[System.Serializable]
public class Milestone
{
    public string name;
    public float height;
}