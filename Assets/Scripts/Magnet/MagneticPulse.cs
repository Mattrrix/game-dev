using UnityEngine;
using UnityEngine.InputSystem;
using Game.Asteroids;
using Game.Core;

namespace Game.Magnet
{
    public class MagneticPulse : MonoBehaviour
    {
        [Header("Range")]
        [SerializeField] float attractRadius = 5f;
        [SerializeField] int maxAttractTargets = 4;

        [Header("Attract (Space) — PD velocity controller")]
        [SerializeField] float pullSpeed = 55f;
        [SerializeField] float pullGain = 38f;
        [SerializeField] float pullStopDistance = 0.35f;

        [Header("Charged Repel (LMB — hold to charge, release to fire)")]
        [SerializeField] float maxChargeTime = 0.6f;
        [SerializeField] float minChargeToFire = 0.08f;
        [SerializeField] float repelBeamThickness = 2.2f;
        [SerializeField] float repelEndRadius = 4f;
        [SerializeField] float repelMinImpulse = 10f;
        [SerializeField] float repelMaxImpulse = 38f;
        [SerializeField, Range(0f, 1f)] float bigAsteroidPushScale = 0.35f;

        [Header("Refs")]
        [SerializeField] Camera cam;
        [SerializeField] ChargeArrowVisual chargeVisual;
        [SerializeField] ChargeAuraVisual auraVisual;

        readonly Collider[] buffer = new Collider[64];
        readonly Collider[] endBuffer = new Collider[64];
        readonly (Collider col, float sqrDist)[] sortBuf = new (Collider, float)[64];
        Plane groundPlane;

        float charge;
        bool charging;
        bool prevHold;

        public bool IsCharging => charging;
        public float Charge01 => Mathf.Clamp01(charge / maxChargeTime);

        public event System.Action<Vector3, float> OnRepelFired;
        public bool TryGetGroundCursor(out Vector3 worldPos)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) { worldPos = default; return false; }
            return TryGetCursorWorld(mouse.position.ReadValue(), out worldPos);
        }

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (chargeVisual == null) chargeVisual = GetComponentInChildren<ChargeArrowVisual>();
            if (auraVisual == null) auraVisual = GetComponentInChildren<ChargeAuraVisual>();
            if (auraVisual == null)
            {
                var go = new GameObject("ChargeAura");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                auraVisual = go.AddComponent<ChargeAuraVisual>();
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            bool holdState = mouse.leftButton.isPressed;

            if (holdState && !prevHold)
            {
                charging = true;
                charge = 0f;
            }

            if (charging)
            {
                charge = Mathf.Min(maxChargeTime, charge + Time.deltaTime);
                if (TryGetCursorWorld(mouse.position.ReadValue(), out var cursor))
                {
                    if (chargeVisual != null) chargeVisual.Show(transform.position, cursor, Charge01);
                    if (auraVisual != null) auraVisual.Show(transform.position, cursor, Charge01);
                }
            }

            if (!holdState && prevHold && charging)
            {
                FireRepel(mouse.position.ReadValue());
                charging = false;
                charge = 0f;
                if (chargeVisual != null) chargeVisual.Hide();
                if (auraVisual != null) auraVisual.Hide();
            }

            prevHold = holdState;
        }

        void FixedUpdate()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (!kb.spaceKey.isPressed) return;
            if (!TryGetCursorWorld(mouse.position.ReadValue(), out Vector3 cursor)) return;

            int n = Physics.OverlapSphereNonAlloc(cursor, attractRadius, buffer);
            int kept = 0;
            for (int i = 0; i < n; i++)
            {
                var arb = buffer[i].attachedRigidbody;
                if (arb == null) continue;
                if (arb.GetComponent<Asteroid>() == null) continue;
                Vector3 d = cursor - arb.position; d.y = 0f;
                sortBuf[kept++] = (buffer[i], d.sqrMagnitude);
                if (kept >= sortBuf.Length) break;
            }

            int take = Mathf.Min(kept, maxAttractTargets);
            // частичная сортировка выбором: достаём только top-N ближайших,
            // для малых N это дешевле полной сортировки
            for (int i = 0; i < take; i++)
            {
                int min = i;
                for (int j = i + 1; j < kept; j++)
                    if (sortBuf[j].sqrDist < sortBuf[min].sqrDist) min = j;
                if (min != i) (sortBuf[i], sortBuf[min]) = (sortBuf[min], sortBuf[i]);
            }

            for (int i = 0; i < take; i++)
            {
                var arb = sortBuf[i].col.attachedRigidbody;
                if (arb == null) continue;

                Vector3 to = cursor - arb.position; to.y = 0f;
                float dist = to.magnitude;
                if (dist < 0.001f) continue;
                Vector3 dir = to / dist;

                float speedScale = Mathf.Clamp01(dist / pullStopDistance);
                Vector3 desiredVel = dir * (pullSpeed * speedScale);
                Vector3 currVel = arb.linearVelocity; currVel.y = 0f;
                Vector3 force = (desiredVel - currVel) * pullGain;
                force.y = 0f;
                arb.AddForce(force, ForceMode.Acceleration);

                var ast = arb.GetComponent<Asteroid>();
                if (ast != null) ast.RegisterMagnetHighlight();
            }
        }

        void FireRepel(Vector2 mouseScreenPos)
        {
            float c01 = Charge01;
            if (c01 < minChargeToFire / maxChargeTime) return;
            if (!TryGetCursorWorld(mouseScreenPos, out Vector3 cursor)) return;

            Vector3 origin = transform.position;
            Vector3 beam = cursor - origin; beam.y = 0f;
            if (beam.sqrMagnitude < 0.001f) return;
            Vector3 beamDir = beam.normalized;
            float impulse = Mathf.Lerp(repelMinImpulse, repelMaxImpulse, c01);

            Color burstColor = Color.Lerp(new Color(0.7f, 0.85f, 1f), new Color(1f, 0.45f, 0.1f), c01);
            int burstCount = Mathf.RoundToInt(Mathf.Lerp(14, 36, c01));
            float burstSpeed = Mathf.Lerp(5f, 12f, c01);
            SimpleBurst.Spawn(cursor, burstColor, speed: burstSpeed, count: burstCount, size: 0.32f, life: 0.5f);

            CameraShakeService.Pulse(transform.position, 0.05f + c01 * 0.08f);

            const float planeY = 1f;
            Vector3 p0 = new Vector3(origin.x, planeY, origin.z);
            Vector3 p1 = new Vector3(cursor.x, planeY, cursor.z);

            int beamHits = Physics.OverlapCapsuleNonAlloc(p0, p1, repelBeamThickness, buffer);
            int endHits = Physics.OverlapSphereNonAlloc(cursor, repelEndRadius, endBuffer);

            var hitColor = new Color(1f, 0.75f, 0.35f);
            var processed = new System.Collections.Generic.HashSet<EntityId>();

            for (int i = 0; i < beamHits; i++)
                ApplyRepelHit(buffer[i], beamDir, impulse, c01, hitColor, processed);
            for (int i = 0; i < endHits; i++)
                ApplyRepelHit(endBuffer[i], beamDir, impulse, c01, hitColor, processed);

            OnRepelFired?.Invoke(cursor, c01);
        }

        void ApplyRepelHit(Collider col, Vector3 beamDir, float impulse, float c01, Color hitColor, System.Collections.Generic.HashSet<EntityId> processed)
        {
            if (col == null) return;
            var arb = col.attachedRigidbody;
            if (arb == null) return;
            if (!processed.Add(arb.GetEntityId())) return;
            var ast = arb.GetComponent<Asteroid>();
            if (ast == null) return;

            float pushScale = ast.IsSealed ? 0.6f : (ast.Size > 1 ? bigAsteroidPushScale : 1f);
            Vector3 push = beamDir * (impulse * pushScale);
            push.y = 0f;
            arb.AddForce(push, ForceMode.Impulse);

            if (!ast.IsSealed && ast.Size > 1)
                ast.TakeRepelHit(c01, beamDir, c01);

            SimpleBurst.Spawn(arb.position, hitColor, speed: 4.5f, count: 10, size: 0.22f, life: 0.35f);
        }

        bool TryGetCursorWorld(Vector2 screenPos, out Vector3 worldPos)
        {
            worldPos = default;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (groundPlane.Raycast(ray, out float enter))
            {
                worldPos = ray.GetPoint(enter);
                return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane p = new Plane(Vector3.up, Vector3.zero);
            if (p.Raycast(ray, out float e))
            {
                Gizmos.color = charging ? Color.red : Color.cyan;
                Gizmos.DrawWireSphere(ray.GetPoint(e), attractRadius);
            }
        }
    }
}
