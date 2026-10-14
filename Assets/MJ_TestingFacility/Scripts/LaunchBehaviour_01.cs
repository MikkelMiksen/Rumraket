using UnityEngine;

public class LaunchBehaviour_01 : MonoBehaviour
{
    [SerializeField] private float Fuel_Amount, Fuel_Capacity, Engine_Thrust_Power, Engine_Consumption_Factor;
    [SerializeField] private Rigidbody rb;
    
    private bool isFlying;

    public float GetETPAmount() => Engine_Thrust_Power;
    public float GetFuelCapacity() => Fuel_Capacity;
    public void SetFuelCapacity(float new_capacity) => Fuel_Capacity = Fuel_Capacity + new_capacity;
    public void SetETPAmount(float new_etp) => Engine_Thrust_Power = Engine_Thrust_Power + new_etp;
    
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Fuel_Amount = Fuel_Capacity;
    }
    
    // Update is called once per frame
    void Update()
    {
        if (Fuel_Amount <= 0)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isFlying = !isFlying;
        }

        if (isFlying)
        {
            Fuel_Amount -= Engine_Consumption_Factor * Time.deltaTime;
            rb.AddForce(transform.up * (Engine_Thrust_Power * 15));
        }
    }
}
