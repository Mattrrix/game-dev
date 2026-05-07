using UnityEngine;

namespace Game.Player
{
    public class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 22f, -6f);
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] bool lookAtTarget = true;

        Vector3 vel;

        public void SetTarget(Transform t) => target = t;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref vel, smoothTime);
            if (lookAtTarget) transform.LookAt(target.position);
        }
    }
}
