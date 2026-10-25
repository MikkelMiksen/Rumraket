using UnityEngine;
using UnityEngine.PlayerLoop;

namespace MJ_TestingFacility.Scripts
{
    public class MJ_FlightCameraController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private float lerpSpeed = 5f;
        public Transform follow_position_target;
        private GameObject[] objects_to_follow;

        [SerializeField] public Transform lookat_target;
        
        void Awake()
        {
            objects_to_follow = new GameObject[3];
            GameObject[] foundTargets = GameObject.FindGameObjectsWithTag("FlightTarget");
            SortFlightTargets(foundTargets);

            if (gameManager != null)
            {
                gameManager.OnModuleTierChanged += OnModuleTierChanged;
                if (objects_to_follow.Length > 0 && objects_to_follow[0] != null)
                {
                    follow_position_target = objects_to_follow[0].transform;
                }
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnModuleTierChanged -= OnModuleTierChanged;
            }
        }

        private void OnModuleTierChanged(UpgradeType type, int newTier)
        {
            Debug.Log("Module tier changed: " + type + " to " + newTier);
            if (newTier >= 1 && newTier <= 3)
            {
                follow_position_target = objects_to_follow[newTier - 1]?.transform;
            }
        }
        
        public void SetCameraTarget(Transform positionTarget, Transform lookatTarget)
        {
            follow_position_target = positionTarget;
            this.lookat_target = lookatTarget;
        }

        void Update()
        {
            if (follow_position_target != null)
            {
                transform.position = Vector3.Lerp(transform.position, follow_position_target.position, Time.deltaTime * lerpSpeed);
                if (lookat_target != null)
                {
                    transform.LookAt(lookat_target.position);
                }
            }
        }

        private void SortFlightTargets(GameObject[] objects_to_sort_by_name)
        {
            foreach (GameObject obj in objects_to_sort_by_name)
            {
                if(obj.name.Contains("1")) objects_to_follow[0] = obj;
                else if(obj.name.Contains("2")) objects_to_follow[1] = obj;
                else if(obj.name.Contains("3")) objects_to_follow[2] = obj;
            }
        }
    }
}