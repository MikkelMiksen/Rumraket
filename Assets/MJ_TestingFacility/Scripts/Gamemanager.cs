using MJ_TestingFacility.Scripts;
using UnityEngine;
using UnityEngine.UIElements;

public class GameManager : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private PanelRenderer panelRenderer;
    [SerializeField] private LaunchBehaviour_01 ship;
    [SerializeField] private Rigidbody shipBody;
    [SerializeField] private CameraRig cameraRig;
    [SerializeField] private Transform titleAnchor;
    [SerializeField] private Transform padAnchor;
    [SerializeField] private Transform hubAnchor;

    [Header("Tuning")]
    [SerializeField] private int startingCredits = 0;
    [SerializeField] private float creditsPerPoint = 1f;
    [SerializeField] private int maxUpgradeLevel = 5;
    [SerializeField] private int baseUpgradeCost = 100;
    [SerializeField] private float upgradeCostGrowth = 1.5f;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 6f, -20f);

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
    private bool stateInitialized;

    // ---------- UI readiness ----------
    private int uiVersion;
    private bool uiReady;

    // ---------- Persistent data ----------
    private int credits;
    private int fuelLevel;
    private int engineLevel;
    private float bestHeight;

    // ---------- Current run ----------
    private float runMaxHeight;
    private float runMaxSpeed;

    // ---------- Ship start pose ----------
    private Vector3 startPosition;
    private Quaternion startRotation;

    // ===================================================================
    // Unity lifecycle
    // ===================================================================

    private void Awake()
    {
        // Step 1: remember where the ship sits on the pad. This is in Awake
        // because the UI callback below can fire before Start runs.
        startPosition = ship.transform.position;
        startRotation = ship.transform.rotation;
        credits = startingCredits;

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

    // Called by the PanelRenderer when the UI is first built, and again if the
    // UI is rebuilt (for example by live reload after you edit a UXML file).
    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
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
                break;

            case GameState.LaunchPad:
                break;

            case GameState.UpgradeHub:
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

        currentState = newState;
        stateInitialized = true;

        switch (newState)
        {
            case GameState.Title:
                ResetShipToPad();
                cameraRig.GoTo(titleAnchor);
                break;

            case GameState.LaunchPad:
                ResetShipToPad();
                cameraRig.GoTo(padAnchor);
                ui_functionality.SetCredits(credits);
                RefreshJourney();
                break;

            case GameState.UpgradeHub:
                cameraRig.GoTo(hubAnchor);
                RefreshUpgradeUI();
                break;

            case GameState.Flying:
                StartRun();
                break;

            case GameState.RunSummary:
                cameraRig.Freeze();
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

    private void OnPlayClicked() { SetState(GameState.LaunchPad); }
    private void OnUpgradesClicked() { SetState(GameState.UpgradeHub); }
    private void OnLaunchClicked() { SetState(GameState.Flying); }
    private void OnBackClicked() { SetState(GameState.LaunchPad); }
    private void OnContinueClicked() { SetState(GameState.LaunchPad); }

    private void OnUpgradeBuyClicked(UpgradeType type)
    {
        // Step 1: refuse if already at the top level
        int level = GetLevel(type);

        if (level >= maxUpgradeLevel)
        {
            ui_functionality.SetUpgradeMessage(type, "Already at max level.");
            return;
        }

        // Step 2: refuse if the player cannot afford it
        int cost = GetUpgradeCost(level);

        if (credits < cost)
        {
            ui_functionality.SetUpgradeMessage(type, "Not enough credits.");
            return;
        }

        // Step 3: spend, apply to the ship, and refresh the display
        credits -= cost;
        ApplyUpgrade(type);

        ui_functionality.SetUpgradeMessage(UpgradeType.FuelCapacity, "");
        ui_functionality.SetUpgradeMessage(UpgradeType.EnginePower, "");
        RefreshUpgradeUI();
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
        cameraRig.Follow(ship.transform, followOffset);
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
        int distancePoints = Mathf.FloorToInt(runMaxHeight);
        int speedPoints = Mathf.FloorToInt(runMaxSpeed);
        int totalPoints = distancePoints + speedPoints;
        int creditsEarned = Mathf.FloorToInt(totalPoints * creditsPerPoint);

        // Step 2: update best distance and bank the credits
        bool isNewBest = runMaxHeight > bestHeight;

        if (isNewBest)
        {
            bestHeight = runMaxHeight;
        }

        credits += creditsEarned;

        // Step 3: show the breakdown
        ui_functionality.SetSummary(
            Mathf.FloorToInt(runMaxHeight), distancePoints,
            Mathf.FloorToInt(runMaxSpeed), speedPoints,
            totalPoints, creditsPerPoint,
            creditsEarned, isNewBest);
    }

    private void ResetShipToPad()
    {
        shipBody.linearVelocity = Vector3.zero;
        shipBody.angularVelocity = Vector3.zero;
        shipBody.position = startPosition;
        shipBody.rotation = startRotation;
        ship.transform.SetPositionAndRotation(startPosition, startRotation);

        ship.ResetRun();
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

    private void ApplyUpgrade(UpgradeType type)
    {
        if (type == UpgradeType.FuelCapacity)
        {
            ship.OnFuelUpgrade();
            fuelLevel++;
        }
        else
        {
            ship.OnEngineUpgrade();
            engineLevel++;
        }
    }

    private void RefreshUpgradeUI()
    {
        // Fuel card
        float fuelNow = ship.GetFuelCapacity();
        string fuelEffect = fuelNow.ToString("0.#") + " L to " + (fuelNow + ship.GetFuelUpgradeAmount()).ToString("0.#") + " L";
        SetCard(UpgradeType.FuelCapacity, fuelLevel, fuelEffect);

        // Engine card
        float engineNow = ship.GetETPAmount();
        string engineEffect = engineNow.ToString("0.#") + " to " + (engineNow + ship.GetEngineUpgradeAmount()).ToString("0.#");
        SetCard(UpgradeType.EnginePower, engineLevel, engineEffect);

        ui_functionality.SetCredits(credits);
        ui_functionality.SetStats(
            "Fuel capacity " + fuelNow.ToString("0.#") + " L   |   Engine power " + engineNow.ToString("0.#"));
    }

    private void SetCard(UpgradeType type, int level, string effectText)
    {
        if (level >= maxUpgradeLevel)
        {
            ui_functionality.SetUpgradeCard(type, level, "Max level", "");
            return;
        }

        string costText = GetUpgradeCost(level).ToString("N0") + " credits";
        ui_functionality.SetUpgradeCard(type, level, effectText, costText);
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