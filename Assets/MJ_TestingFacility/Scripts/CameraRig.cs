using UnityEngine;

// Replaces CamBehaviour_01. GameManager tells it where to go.
public class CameraRig : MonoBehaviour
{
    [SerializeField] private float anchorSmoothing = 3f;
    [SerializeField] private float followSmoothing = 6f;

    private Transform anchor;
    private Transform followTarget;
    private Vector3 followOffset;

    // Glide to a fixed spot (title, pad, hub)
    public void GoTo(Transform newAnchor)
    {
        anchor = newAnchor;
        followTarget = null;
    }

    // Trail a moving target (flight)
    public void Follow(Transform target, Vector3 offset)
    {
        followTarget = target;
        followOffset = offset;
        anchor = null;
    }

    // Stop following and stop moving: the camera stays exactly where it is now
    public void Freeze()
    {
        followTarget = null;
        anchor = null;
    }

    private void LateUpdate()
    {
        if (followTarget != null)
        {
            float blend = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);

            Vector3 desiredPosition = followTarget.position + followOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);

            Quaternion desiredRotation = Quaternion.LookRotation(followTarget.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }
        else if (anchor != null)
        {
            float blend = 1f - Mathf.Exp(-anchorSmoothing * Time.deltaTime);

            transform.position = Vector3.Lerp(transform.position, anchor.position, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, anchor.rotation, blend);
        }
    }
}