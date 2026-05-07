using UnityEngine;
using Unity.Cinemachine;

namespace Game.Core
{
    public class CameraShakeService : MonoBehaviour
    {
        public static CameraShakeService Instance { get; private set; }

        [SerializeField] CinemachineImpulseSource source;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (source == null) source = GetComponent<CinemachineImpulseSource>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Pulse(Vector3 worldPos, float magnitude)
        {
            if (Instance == null || Instance.source == null) return;
            Instance.source.GenerateImpulseAt(worldPos, Vector3.one * magnitude);
        }

        public static void Pulse(float magnitude)
        {
            if (Instance == null || Instance.source == null) return;
            Instance.source.GenerateImpulseWithVelocity(Random.onUnitSphere * magnitude);
        }
    }
}
