using System.Collections.Generic;
using UnityEngine;

public class FlightDataLogger : MonoBehaviour
{
    private GameObject Player;
    
    private List<float> data = new();
    private float MaxHeight, MaxVelocity;
    private Vector3 lastFramePosition;
    private Transform initialPosition;
    public int score;
    
    private void Start()
    {
        Player = GameObject.FindGameObjectWithTag("Player");
        lastFramePosition = Player.transform.position;
        initialPosition = Player.transform;
    }

    public void Update()
    {
        MaxHeight = Player.transform.position.y > MaxHeight ? Player.transform.position.y : MaxHeight;
        
        MaxVelocity = Vector3.Distance(Player.transform.position, lastFramePosition) / Time.deltaTime > MaxVelocity ? 
            Vector3.Distance(Player.transform.position, lastFramePosition) / Time.deltaTime : MaxVelocity;
        
        lastFramePosition = Player.transform.position;

        if (Player.transform.position.y <= MaxHeight - 20)
        {
            // TODO: Display Reset prompt

            if (Input.GetKeyDown(KeyCode.R))
            {
                score += (int)MaxHeight + (int)MaxVelocity;
                Player.transform.position = initialPosition.position;
                MaxHeight = 0;
                MaxVelocity = 0;
            }
        }
    }
    
    public List<float> GetData()
    {
        data.Add(MaxVelocity);
        data.Add(MaxHeight);
        return data;
    }
    
    public int GetScore()
    {
        return score;
    }
}
