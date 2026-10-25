using UnityEngine;
using UnityEngine.UIElements;

public class UpgradeBehaviour : MonoBehaviour
{
    private int score; 
    private int cost, numberOfUpgrades;
    private int lvl;
    private GameObject player;

    private Button fuelButton;
    private Button engineButton;
    
    public void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        
        fuelButton = root.Q("fuelCard").Q<Button>("buyButton");
        engineButton = root.Q("engineCard").Q<Button>("buyButton");

        if (fuelButton != null) fuelButton.clicked += OnMkUpgrade;
        if (engineButton != null) engineButton.clicked += OnBoosterUpgrade;
    }
    
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
    }

    public int CalculateCost()
    {
        lvl = (int)player.GetComponent<LaunchBehaviour_01>().GetFuelCapacity() - 5 + (int)player.GetComponent<LaunchBehaviour_01>().GetETPAmount() - 5;
        // TODO: make sure player progression is logarithmic
        cost = (int)(lvl/lvl + 10 + lvl - (int)player.GetComponent<LaunchBehaviour_01>().GetETPAmount());
        return cost;
    }
    
    public void LateUpdate()
    {
        score = GameObject.FindGameObjectWithTag("Data").GetComponent<FlightDataLogger>().GetScore();
    }

    void OnMkUpgrade()
    {
        if (score >= cost)
        {
            var launchBehaviour = player.GetComponent<LaunchBehaviour_01>();
            if (launchBehaviour.MkUpgradeLevel < 5)
            {
                launchBehaviour.SetFuelCapacity(launchBehaviour.GetFuelCapacity() + 1);
                launchBehaviour.SetETP(launchBehaviour.GetETPAmount() + 1);
                launchBehaviour.IncrementMkUpgradeLevel();
                
                var rocket = player.GetComponent<RocketAssembly>();
                if (rocket != null)
                {
                    rocket.SetTier(rocket.Tier + 1);
                }
            }
        }
    }

    public void OnBoosterUpgrade()
    {
        if (score >= cost)
        {
            var launchBehaviour = player.GetComponent<LaunchBehaviour_01>();
            if (launchBehaviour.BoosterUpgradeLevel < 5)
            {
                var rocket = player.GetComponent<RocketAssembly>();
                if (rocket != null && rocket.BoosterCount < rocket.BoosterMax)
                {
                    rocket.SetBoosterCount(rocket.BoosterCount + 1);
                    launchBehaviour.SetBoosterCount(rocket.BoosterCount);
                    launchBehaviour.IncrementBoosterUpgradeLevel();
                }
            }
        }
    }
}
