using UnityEngine;

public class LookAtRocket : MonoBehaviour
{
    public Transform rocket;

    void Start()
    {
        transform.LookAt(rocket);
    }
}
