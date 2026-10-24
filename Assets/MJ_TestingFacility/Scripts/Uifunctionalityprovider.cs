using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MJ_TestingFacility.Scripts;

        [System.Serializable]
        public enum UpgradeType
        {
            FuelCapacity,
            EnginePower
        }
        
        // Plain C# class (not a MonoBehaviour). GameManager creates one instance and calls Init.
        // The UI reports clicks through events and shows values through Set... methods.
        // It never decides anything about the game itself.
        [System.Serializable]
        public class UIFunctionalityProvider
        {
            // ---------- Events: GameManager subscribes to these ----------
            public event Action PlayClicked;
            public event Action UpgradesClicked;
            public event Action LaunchClicked;
            public event Action BackClicked;
            public event Action ContinueClicked;
            public event Action<UpgradeType> UpgradeBuyClicked;

            // ---------- USS class names (defined in Theme.uss) ----------
            private const string HiddenPageClass = "page--hidden";
            private const string HiddenClass = "hidden";
            private const string FilledPipClass = "pip--filled";

            // ---------- Nested helper: the elements of one upgrade card ----------
            private class UpgradeCardView
            {
                public VisualElement Root;
                public Label Effect;
                public Label Cost;
                public Label Message;
                public Button BuyButton;
                public List<VisualElement> Pips = new List<VisualElement>();
            }

            // ---------- Pages ----------
            private VisualElement pageTitle;
            private VisualElement pageLaunchPad;
            private VisualElement pageUpgradeHub;
            private VisualElement pageHud;
            private VisualElement pageSummary;

            // ---------- Buttons ----------
            private Button playButton;
            private Button upgradesButton;
            private Button launchButton;
            private Button backButton;
            private Button continueButton;

            // ---------- Credits and stats ----------
            private Label launchPadCredits;
            private Label upgradeHubCredits;
            private Label statsLabel;

            // ---------- Upgrade cards ----------
            private UpgradeCardView fuelCard;
            private UpgradeCardView engineCard;

            // ---------- Journey (launch pad and HUD each have their own copy) ----------
            private Label padJourneyText;
            private ProgressBar padJourneyBar; private Label padJourneyFrom;
            private Label padJourneyTo;
            private Label hudJourneyText;
            private ProgressBar hudJourneyBar;
            private Label hudJourneyFrom;
            private Label hudJourneyTo;

            // ---------- HUD ----------
            private Label speedValue; 
            private Label heightValue;
            private ProgressBar fuelBar;
            private Label fuelValue;

            // ---------- Run summary ----------
            private Label distanceRaw;
            private Label distancePointsLabel;
            private Label speedRaw;
            private Label speedPointsLabel;
            private Label newBestLabel;
            private Label totalLabel;
            private Label creditsEarnedLabel;

            private bool initialized;

            // ===================================================================
            // Setup and teardown
            // ===================================================================

            // Call once from GameManager.Start, with uiDocument.rootVisualElement.
            public void Init(VisualElement root)
            {
                // Step 1: find the five pages
                pageTitle = Require<VisualElement>(root, "pageTitle");
                pageLaunchPad = Require<VisualElement>(root, "pageLaunchPad");
                pageUpgradeHub = Require<VisualElement>(root, "pageUpgradeHub");
                pageHud = Require<VisualElement>(root, "pageHud");
                pageSummary = Require<VisualElement>(root, "pageSummary");

                // Step 2: find the buttons, each inside its own page
                playButton = Require<Button>(pageTitle, "playButton");
                upgradesButton = Require<Button>(pageLaunchPad, "upgradesButton");
                launchButton = Require<Button>(pageLaunchPad, "launchButton");
                backButton = Require<Button>(pageUpgradeHub, "backButton");
                continueButton = Require<Button>(pageSummary, "continueButton");

                // Step 3: credits, stats and the two upgrade cards
                launchPadCredits = Require<Label>(pageLaunchPad, "creditsLabel");
                upgradeHubCredits = Require<Label>(pageUpgradeHub, "creditsLabel");
                statsLabel = Require<Label>(pageUpgradeHub, "statsLabel");
                fuelCard = BuildCardView(pageUpgradeHub, "fuelCard");
                engineCard = BuildCardView(pageUpgradeHub, "engineCard");

                // Step 4: journey elements on the launch pad and on the HUD
                padJourneyText = Require<Label>(pageLaunchPad, "journeyText");
                padJourneyBar = Require<ProgressBar>(pageLaunchPad, "journeyProgress");
                padJourneyFrom = Require<Label>(pageLaunchPad, "journeyFromLabel");
                padJourneyTo = Require<Label>(pageLaunchPad, "journeyToLabel");
                hudJourneyText = Require<Label>(pageHud, "journeyText");
                hudJourneyBar = Require<ProgressBar>(pageHud, "journeyProgress");
                hudJourneyFrom = Require<Label>(pageHud, "journeyFromLabel");
                hudJourneyTo = Require<Label>(pageHud, "journeyToLabel");

                // Step 5: HUD readouts and the fuel bar
                VisualElement speedReadout = Require<VisualElement>(pageHud, "speedReadout");
                VisualElement heightReadout = Require<VisualElement>(pageHud, "heightReadout");
                speedValue = Require<Label>(speedReadout, "readoutValue");
                heightValue = Require<Label>(heightReadout, "readoutValue");
                fuelBar = Require<ProgressBar>(pageHud, "fuelBar");
                fuelValue = Require<Label>(pageHud, "fuelValue");

                // Step 6: run summary rows and labels
                VisualElement distanceRow = Require<VisualElement>(pageSummary, "distanceRow");
                VisualElement speedRow = Require<VisualElement>(pageSummary, "speedRow");
                distanceRaw = Require<Label>(distanceRow, "rowRaw");
                distancePointsLabel = Require<Label>(distanceRow, "rowPoints");
                speedRaw = Require<Label>(speedRow, "rowRaw");
                speedPointsLabel = Require<Label>(speedRow, "rowPoints");
                newBestLabel = Require<Label>(pageSummary, "newBestLabel");
                totalLabel = Require<Label>(pageSummary, "totalLabel");
                creditsEarnedLabel = Require<Label>(pageSummary, "creditsEarnedLabel");

                // Step 7: subscribe to the button clicks
                playButton.clicked += OnPlayButton;
                upgradesButton.clicked += OnUpgradesButton;
                launchButton.clicked += OnLaunchButton;
                backButton.clicked += OnBackButton;
                continueButton.clicked += OnContinueButton;
                fuelCard.BuyButton.clicked += OnFuelBuyButton;
                engineCard.BuyButton.clicked += OnEngineBuyButton;

                initialized = true;
        } 
    
        // Call from GameManager.OnDestroy so the handlers do not linger.
        public void Dispose()
        {
            if (!initialized)
            {
                return;
            }

            playButton.clicked -= OnPlayButton;
            upgradesButton.clicked -= OnUpgradesButton;
            launchButton.clicked -= OnLaunchButton;
            backButton.clicked -= OnBackButton;
            continueButton.clicked -= OnContinueButton;
            fuelCard.BuyButton.clicked -= OnFuelBuyButton;
            engineCard.BuyButton.clicked -= OnEngineBuyButton;

            initialized = false;
        }

        // ===================================================================
        // Showing pages
        // ===================================================================

        // GameManager calls this from the init switch of SetState.
        public void ShowState(GameState state)
        {
            SetPageVisible(pageTitle, state == GameState.Title);
            SetPageVisible(pageLaunchPad, state == GameState.LaunchPad);
            SetPageVisible(pageUpgradeHub, state == GameState.UpgradeHub);

            // The HUD stays visible behind the summary popup
            bool hudVisible = state == GameState.Flying || state == GameState.RunSummary;
            SetPageVisible(pageHud, hudVisible);

            SetPageVisible(pageSummary, state == GameState.RunSummary);
        }

        private void SetPageVisible(VisualElement page, bool visible)
        {
            if (visible)
            {
                page.RemoveFromClassList(HiddenPageClass);
            }
            else
            {
                page.AddToClassList(HiddenPageClass);
            }
        }

        // ===================================================================
        // Setters: GameManager pushes values in, the UI only displays them
        // ===================================================================

        public void SetCredits(int credits)
        {
            string text = credits.ToString("N0") + " credits";
            launchPadCredits.text = text;
            upgradeHubCredits.text = text;
        }

        public void SetStats(string text)
        {
            statsLabel.text = text;
        }

        public void SetUpgradeCard(UpgradeType type, int level, string effectText, string costText)
        {
            UpgradeCardView card = GetCard(type);

            // Fill the first "level" pips, empty the rest
            for (int i = 0; i < card.Pips.Count; i++)
            {
                if (i < level)
                {
                    card.Pips[i].AddToClassList(FilledPipClass);
                }
                else
                {
                    card.Pips[i].RemoveFromClassList(FilledPipClass);
                }
            }

            card.Effect.text = effectText;
            card.Cost.text = costText;
        }

        public void SetUpgradeMessage(UpgradeType type, string message)
        {
            GetCard(type).Message.text = message;
        }

        // Updates both the launch pad copy and the HUD copy
        public void SetJourney(string text, float percent, string fromName, string toName)
        {
            padJourneyText.text = text;
            padJourneyBar.value = percent;
            padJourneyFrom.text = fromName;
            padJourneyTo.text = toName;

            hudJourneyText.text = text;
            hudJourneyBar.value = percent;
            hudJourneyFrom.text = fromName;
            hudJourneyTo.text = toName;
        }

        // Call every frame while flying
        public void SetHud(float speed, float height, float fuelPercent, float fuelLiters)
        {
            speedValue.text = speed.ToString("0") + " m/s";
            heightValue.text = height.ToString("N0") + " m";
            fuelBar.value = fuelPercent;
            fuelValue.text = fuelLiters.ToString("0") + " L";
        }

        public void SetSummary(
            int heightMeters, int distancePoints,
            int topSpeed, int speedPoints,
            int totalPoints, float conversionRate,
            int creditsEarned, bool isNewBest)
        {
            distanceRaw.text = heightMeters.ToString("N0") + " m";
            distancePointsLabel.text = distancePoints.ToString("N0") + " pts";
            speedRaw.text = topSpeed.ToString("N0") + " m/s";
            speedPointsLabel.text = speedPoints.ToString("N0") + " pts";

            totalLabel.text = totalPoints.ToString("N0") + " pts x " + conversionRate.ToString("0.0");
            creditsEarnedLabel.text = "+" + creditsEarned.ToString("N0") + " credits";

            if (isNewBest)
            {
                newBestLabel.RemoveFromClassList(HiddenClass);
            }
            else
            {
                newBestLabel.AddToClassList(HiddenClass);
            }
        }

        // ===================================================================
        // Click handlers: each one only raises an event
        // ===================================================================

        private void OnPlayButton() { PlayClicked?.Invoke(); }
        private void OnUpgradesButton() { UpgradesClicked?.Invoke(); }
        private void OnLaunchButton() { LaunchClicked?.Invoke(); }
        private void OnBackButton() { BackClicked?.Invoke(); }
        private void OnContinueButton() { ContinueClicked?.Invoke(); }
        private void OnFuelBuyButton() { UpgradeBuyClicked?.Invoke(UpgradeType.FuelCapacity); }
        private void OnEngineBuyButton() { UpgradeBuyClicked?.Invoke(UpgradeType.EnginePower); }

        // ===================================================================
        // Helpers
        // ===================================================================

        private UpgradeCardView GetCard(UpgradeType type)
        {
            if (type == UpgradeType.FuelCapacity)
            {
                return fuelCard;
            }

            return engineCard;
        }

        private UpgradeCardView BuildCardView(VisualElement page, string cardName)
        {
            UpgradeCardView view = new UpgradeCardView();
            view.Root = Require<VisualElement>(page, cardName);
            view.Effect = Require<Label>(view.Root, "cardEffect");
            view.Cost = Require<Label>(view.Root, "cardCost");
            view.Message = Require<Label>(view.Root, "cardMessage");
            view.BuyButton = Require<Button>(view.Root, "buyButton");
            view.Pips = view.Root.Query<VisualElement>(className: "pip").ToList();
            return view;
        }

        // Finds an element by name and logs a clear error if the UXML does not have it
        private T Require<T>(VisualElement parent, string elementName) where T : VisualElement
        {
            T element = parent.Q<T>(elementName);

            if (element == null)
            {
                Debug.LogError("UIFunctionalityProvider: no " + typeof(T).Name
                + " named '" + elementName + "'. Check the UXML.");
            }

            return element;
        }
    }

