using UnityEngine;
using Game.Asteroids;
using Game.Core;
using Game.Magnet;

namespace Game.Sectors
{
    [RequireComponent(typeof(Collider))]
    public class SortingSector : MonoBehaviour
    {
        [SerializeField] AsteroidType acceptedType = AsteroidType.Rock;
        [SerializeField] int correctScore = 10;
        [SerializeField] float bounceImpulse = 14f;

        Collider sectorCol;

        void Awake()
        {
            sectorCol = GetComponent<Collider>();
            if (sectorCol != null) sectorCol.isTrigger = true;
        }

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var a = other.GetComponentInParent<Asteroid>();
            if (a == null || a.IsConsumed) return;

            if (a.IsSealed)
            {
                if (a.NextRequiredVisit == acceptedType)
                {
                    a.OnSectorVisit(acceptedType);
                    SimpleBurst.Spawn(a.transform.position, new Color(1f, 0.95f, 0.4f), speed: 7f, count: 18, size: 0.3f, life: 0.5f);
                    if (!a.IsSealed)
                        ScorePopup.Spawn(a.transform.position + Vector3.up * 0.6f, "UNLOCKED", new Color(1f, 0.95f, 0.4f));
                }
                else
                {
                    BounceAway(a);
                }
                return;
            }

            if (a.Size > 1)
            {
                BounceAway(a);
                return;
            }

            bool correct = a.type == acceptedType;
            if (!correct)
            {
                BounceAway(a);
                return;
            }

            int actualDelta = GameManager.Instance != null
                ? GameManager.Instance.OnAsteroidSorted(a.transform.position, correctScore, true, a.type)
                : correctScore;
            int combo = GameManager.Instance != null ? GameManager.Instance.Combo : 1;

            Color burstColor = new Color(0.4f, 1f, 0.5f);
            SimpleBurst.Spawn(a.transform.position, burstColor, speed: 8f, count: 22, size: 0.32f, life: 0.55f);
            string popupText = combo > 1 ? $"+{actualDelta}  x{combo}" : $"+{actualDelta}";
            Color popupColor = combo > 1 ? new Color(1f, 0.95f, 0.3f) : burstColor;
            ScorePopup.Spawn(a.transform.position + Vector3.up * 0.5f, popupText, popupColor);

            a.Consume();
        }

        void BounceAway(Asteroid a)
        {
            var rb = a.GetComponent<Rigidbody>();
            if (rb == null) return;
            Vector3 dir = a.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                Vector2 r = Random.insideUnitCircle.normalized;
                dir = new Vector3(r.x, 0f, r.y);
            }
            dir.Normalize();
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(dir * bounceImpulse, ForceMode.Impulse);
            SimpleBurst.Spawn(a.transform.position, new Color(1f, 0.7f, 0.7f), speed: 5f, count: 8, size: 0.2f, life: 0.3f);
        }

        public AsteroidType AcceptedType => acceptedType;
    }
}
