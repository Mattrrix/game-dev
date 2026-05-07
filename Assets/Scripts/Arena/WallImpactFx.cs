using UnityEngine;
using Game.Magnet;

namespace Game.Arena
{
    public class WallImpactFx : MonoBehaviour
    {
        [SerializeField] float minImpactSpeed = 3.5f;
        [SerializeField] float refImpactSpeed = 22f;
        [SerializeField] float retriggerCooldown = 0.06f;

        [Header("Dust burst")]
        [SerializeField] Color dustColor = new Color(0.78f, 0.8f, 0.88f, 1f);
        [SerializeField] int dustCountMin = 4;
        [SerializeField] int dustCountMax = 18;
        [SerializeField] float dustSizeMin = 0.18f;
        [SerializeField] float dustSizeMax = 0.42f;
        [SerializeField] float dustSpeedMin = 3f;
        [SerializeField] float dustSpeedMax = 9f;
        [SerializeField] float dustLifeMin = 0.28f;
        [SerializeField] float dustLifeMax = 0.55f;

        [Header("Sparks (only on hard impacts)")]
        [SerializeField] float sparkThreshold = 0.32f;
        [SerializeField] Color sparkColor = new Color(1.6f, 1.2f, 0.45f, 1f);
        [SerializeField] int sparkCountMin = 3;
        [SerializeField] int sparkCountMax = 9;

        float lastImpactTime = -10f;

        void OnCollisionEnter(Collision col)
        {
            if (col.contactCount == 0) return;
            if (Time.time - lastImpactTime < retriggerCooldown) return;

            float speed = col.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            lastImpactTime = Time.time;

            float t = Mathf.Clamp01((speed - minImpactSpeed) / Mathf.Max(0.01f, refImpactSpeed - minImpactSpeed));
            var contact = col.GetContact(0);
            Vector3 pos = contact.point;

            int dustCount = Mathf.RoundToInt(Mathf.Lerp(dustCountMin, dustCountMax, t));
            float dustSize = Mathf.Lerp(dustSizeMin, dustSizeMax, t);
            float dustSpeed = Mathf.Lerp(dustSpeedMin, dustSpeedMax, t);
            float dustLife = Mathf.Lerp(dustLifeMin, dustLifeMax, t);
            SimpleBurst.Spawn(pos, dustColor, speed: dustSpeed, count: dustCount, size: dustSize, life: dustLife);

            if (t >= sparkThreshold)
            {
                float sparkT = (t - sparkThreshold) / (1f - sparkThreshold);
                int sparkCount = Mathf.RoundToInt(Mathf.Lerp(sparkCountMin, sparkCountMax, sparkT));
                float sparkSpeed = Mathf.Lerp(7f, 13f, sparkT);
                SimpleBurst.Spawn(pos, sparkColor, speed: sparkSpeed, count: sparkCount, size: 0.16f, life: 0.32f);
            }
        }
    }
}
