using UnityEngine;
using UnityEngine.PlayerLoop;

namespace MJ_TestingFacility.Scripts
{
    public class MJ_FlightCameraController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        public Transform follow_position_target;
        private GameObject[] stayHereTargets = new GameObject[3];
        private GameObject lookHere;

        void Awake()
        {
            stayHereTargets[0] = GameObject.Find("StayHereMK.1");
            stayHereTargets[1] = GameObject.Find("StayHereMK.2");
            stayHereTargets[2] = GameObject.Find("StayHereMK.3");
            lookHere = GameObject.Find("LookHereUnderFlight");

            if (gameManager != null)
            {
                gameManager.OnModuleTierChanged += OnModuleTierChanged;
                if (stayHereTargets[0] != null) follow_position_target = stayHereTargets[0].transform;
            }
        }

        void Start()
        {
            if (gameManager != null && gameManager.Ship != null)
            {
                transform.SetParent(gameManager.Ship.transform);
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnModuleTierChanged -= OnModuleTierChanged;
            }
        }

        private void OnModuleTierChanged(int newTier)
        {
            Debug.Log("Module tier changed to " + newTier);
            if (newTier >= 1 && newTier <= 3)
            {
                follow_position_target = stayHereTargets[newTier - 1]?.transform;
            }
        }
        
        public void SetCameraTarget(Transform positionTarget)
        {
            follow_position_target = positionTarget;
        }

        void FixedUpdate()
        {
            if (gameManager != null && gameManager.CurrentState != GameState.Flying)
            {
                GameObject launchPadTarget = GameObject.FindGameObjectWithTag("LaunchPadTarget");
                if (launchPadTarget != null)
                {
                    transform.position = launchPadTarget.transform.position;
                    transform.rotation = launchPadTarget.transform.rotation;
                    return;
                }
            }

            if (follow_position_target != null)
            {
                // Calculate position relative to ship if camera is a child
                Vector3 targetLocalPos = follow_position_target.localPosition;
                
                
                // If not a child, calculate relative to ship
                if (transform.parent != null && transform.parent != follow_position_target.parent)
                {
                    targetLocalPos = transform.parent.InverseTransformPoint(follow_position_target.position);
                    
                }

                transform.localPosition = targetLocalPos;
                transform.LookAt(lookHere.transform);
            }
        }

    }
}