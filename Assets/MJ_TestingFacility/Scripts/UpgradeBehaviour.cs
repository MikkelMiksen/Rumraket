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
        
        fuelButton.clicked += OnFuelUpgrade;
        engineButton.clicked += OnEngineUpgrade;
    }
    
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.Q<Button>("fuelUpgradeButton").clicked += OnFuelUpgrade;
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

    void OnFuelUpgrade()
    {
        if (score >= cost)
        {
            player.GetComponent<LaunchBehaviour_01>().SetFuelCapacity(player.GetComponent<LaunchBehaviour_01>().GetFuelCapacity() + 1);
        }
    }

    public void OnEngineUpgrade()
    {
        if (score >= cost)
        {
            player.GetComponent<LaunchBehaviour_01>().SetFuelCapacity(player.GetComponent<LaunchBehaviour_01>().GetFuelCapacity() + 1);
        }
    }
}
