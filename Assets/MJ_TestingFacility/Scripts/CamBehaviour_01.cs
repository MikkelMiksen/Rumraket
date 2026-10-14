using System;
using UnityEngine;

public class CamBehaviour_01 : MonoBehaviour
{
    [SerializeField] private float followSpeed;
    [SerializeField] private GameObject cat;

    // Update is called once per frame
    void Update()
    {
        transform.LookAt(cat.transform);

        if (cat.transform.position.y >= 30)
        {
            transform.position = Vector3.MoveTowards(transform.position ,
                new Vector3(transform.position.x, 
                    cat.transform.position.y - 20, 
                    transform.position.z), 
                Time.deltaTime * followSpeed * 
                Vector3.Distance(cat.transform.position, 
                    transform.position)
            );
        }
    }
}
