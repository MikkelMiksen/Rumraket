using System;
using UnityEngine;

public class CamBehaviour_01 : MonoBehaviour
{
    private Vector3 _offset;
    [SerializeField] private float smoothTime;
    [SerializeField] private GameObject target;
    private Vector3 currentVelocity = Vector3.zero;

    // Update is called once per frame
    void Awake()
    {
        _offset = transform.position - target.transform.position;
    }

    void FixedUpdate()
    {
        Vector3 targetPosition = target.transform.position + _offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
    }
}
