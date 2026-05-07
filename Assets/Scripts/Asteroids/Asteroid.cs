using System.Collections.Generic;
using UnityEngine;
using Game.Magnet;
using Game.Core;

namespace Game.Asteroids
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Asteroid : MonoBehaviour
    {
        public AsteroidType type = AsteroidType.Rock;
        [SerializeField] int size = 2;
        [SerializeField] int fragmentCount = 3;
        [SerializeField] float fragmentScaleMul = 0.6f;
        [SerializeField] float fragmentSpread = 9f;
        [SerializeField] float explosionRadius = 5.5f;
        [SerializeField] float explosionImpulse = 18f;
        [SerializeField] GameObject splitVfxPrefab;

        [Header("Durability vs charged repel (0..1, where 1 = full charge)")]
        [SerializeField] float splitDamageThreshold = 0.7f;
        [SerializeField] float damageRecoveryPerSecond = 0.15f;

        [Header("Drift — keeps asteroids rolling")]
        [SerializeField] float minDriftSpeed = 2.5f;
        [SerializeField] float driftKick = 3f;
        [SerializeField] float driftCheckIntervalMin = 1.2f;
        [SerializeField] float driftCheckIntervalMax = 2.6f;

        static readonly Color SealedTint = new Color(0.45f, 0.46f, 0.52f);

        Rigidbody rb;
        MeshRenderer mr;
        MaterialPropertyBlock mpb;
        TrailRenderer trail;
        Color baseColor = Color.white;
        Color typeColor = Color.white;
        bool consumed;
        float damage;

        readonly List<AsteroidType> requiredVisits = new List<AsteroidType>();
        int visitIndex;
        GameObject sealedIndicator;
        float driftTimer;
        float highlightTimer;

        public Color BaseColor => baseColor;

        public int Size => size;
        public bool IsConsumed => consumed;
        public float DamageRatio => splitDamageThreshold > 0f ? Mathf.Clamp01(damage / splitDamageThreshold) : 0f;
        public bool IsSealed => requiredVisits.Count > 0 && visitIndex < requiredVisits.Count;
        public AsteroidType? NextRequiredVisit => IsSealed ? (AsteroidType?)requiredVisits[visitIndex] : null;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.3f;
            rb.constraints = RigidbodyConstraints.FreezePositionY;
            driftTimer = Random.Range(driftCheckIntervalMin, driftCheckIntervalMax);

            mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mpb = new MaterialPropertyBlock();
                if (mr.sharedMaterial != null) typeColor = mr.sharedMaterial.color;
                baseColor = typeColor;
            }

            SetupTrail();
        }

        void SetupTrail()
        {
            trail = GetComponent<TrailRenderer>();
            if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.startWidth = transform.localScale.x * 0.55f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.06f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.emitting = false;

            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = baseColor;
            trail.material = mat;
            trail.startColor = baseColor;
            trail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }

        void Update()
        {
            if (consumed) return;
            if (damage > 0f)
            {
                damage = Mathf.Max(0f, damage - damageRecoveryPerSecond * Time.deltaTime);
                ApplyVisual();
            }
            if (highlightTimer > 0f)
            {
                highlightTimer -= Time.deltaTime;
                if (highlightTimer <= 0f) ApplyVisual();
            }
            if (rb != null)
            {
                Vector3 v = rb.linearVelocity; v.y = 0f;
                if (trail != null) trail.emitting = v.sqrMagnitude > 9f;

                driftTimer -= Time.deltaTime;
                if (driftTimer <= 0f)
                {
                    driftTimer = Random.Range(driftCheckIntervalMin, driftCheckIntervalMax);
                    if (v.magnitude < minDriftSpeed)
                    {
                        Vector2 r = Random.insideUnitCircle.normalized;
                        Vector3 dir = new Vector3(r.x, 0f, r.y);
                        rb.AddForce(dir * driftKick, ForceMode.Impulse);
                    }
                }
            }
            if (sealedIndicator != null)
            {
                sealedIndicator.transform.position = transform.position + Vector3.up * 1.4f;
                sealedIndicator.transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            }
        }

        public void RegisterMagnetHighlight()
        {
            highlightTimer = 0.15f;
            ApplyVisual();
        }

        public void TakeRepelHit(float chargeAmount)
        {
            TakeRepelHit(chargeAmount, Vector3.zero, 0f);
        }

        public void TakeRepelHit(float chargeAmount, Vector3 blastDir, float blastStrength01)
        {
            if (consumed) return;
            if (IsSealed) return;
            if (size <= 1) return;
            damage += chargeAmount;
            ApplyVisual();
            if (damage >= splitDamageThreshold) Split(blastDir, blastStrength01);
        }

        public void SetSealed(IList<AsteroidType> visitOrder)
        {
            if (visitOrder == null || visitOrder.Count == 0) return;
            requiredVisits.Clear();
            for (int i = 0; i < visitOrder.Count; i++) requiredVisits.Add(visitOrder[i]);
            visitIndex = 0;
            baseColor = SealedTint;
            ApplyVisual();
            BuildIndicator();
        }

        public void OnSectorVisit(AsteroidType sectorType)
        {
            if (!IsSealed) return;
            if (requiredVisits[visitIndex] != sectorType) return;
            visitIndex++;
            if (IsSealed)
            {
                UpdateIndicator();
            }
            else
            {
                baseColor = typeColor;
                ApplyVisual();
                if (sealedIndicator != null) Destroy(sealedIndicator);
            }
        }

        void ApplyVisual()
        {
            if (mr == null || mpb == null) return;
            mr.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", baseColor);
            mpb.SetColor("_Color", baseColor);
            Color damageEmis = new Color(1f, 0.3f, 0.05f) * (DamageRatio * 1.5f);
            float h = highlightTimer > 0f ? 1f : 0f;
            Color highlightEmis = baseColor * (h * 1.5f);
            mpb.SetColor("_EmissionColor", damageEmis + highlightEmis);
            mr.SetPropertyBlock(mpb);
        }

        void BuildIndicator()
        {
            if (sealedIndicator != null) Destroy(sealedIndicator);
            sealedIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sealedIndicator.name = "SealedIndicator";
            var col = sealedIndicator.GetComponent<Collider>();
            if (col) Destroy(col);
            sealedIndicator.transform.localScale = Vector3.one * 0.45f;
            sealedIndicator.transform.position = transform.position + Vector3.up * 1.4f;
            UpdateIndicator();
        }

        void UpdateIndicator()
        {
            if (sealedIndicator == null) return;
            var rend = sealedIndicator.GetComponent<MeshRenderer>();
            if (rend == null) return;
            Color c = SectorColor(NextRequiredVisit ?? AsteroidType.Rock);
            var mat = rend.material;
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.55f);
            }
        }

        static readonly Collider[] explosionBuffer = new Collider[64];

        void ApplyExplosionForce()
        {
            if (explosionRadius <= 0f || explosionImpulse <= 0f) return;
            int n = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, explosionBuffer);
            for (int i = 0; i < n; i++)
            {
                var arb = explosionBuffer[i].attachedRigidbody;
                if (arb == null || arb == rb) continue;
                var other = arb.GetComponent<Asteroid>();
                if (other == null || other.IsConsumed) continue;
                Vector3 d = arb.position - transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.001f)
                {
                    Vector2 r = Random.insideUnitCircle.normalized;
                    d = new Vector3(r.x, 0f, r.y);
                    dist = 1f;
                }
                Vector3 dir = d / dist;
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                float scale = other.IsSealed ? 0.5f : (other.Size > 1 ? 0.45f : 1f);
                arb.AddForce(dir * (explosionImpulse * falloff * scale), ForceMode.Impulse);
            }
        }

        public static Color SectorColor(AsteroidType t)
        {
            switch (t)
            {
                case AsteroidType.Metal: return new Color(1f, 0.82f, 0.12f);
                case AsteroidType.Ice: return new Color(0.2f, 0.85f, 1f);
                case AsteroidType.Rock: return new Color(1f, 0.18f, 0.2f);
                default: return Color.white;
            }
        }

        public void Split() => Split(Vector3.zero, 0f);

        public void Split(Vector3 blastDir, float blastStrength01)
        {
            if (consumed) return;
            if (IsSealed) return;
            consumed = true;

            if (splitVfxPrefab != null)
                Instantiate(splitVfxPrefab, transform.position, Quaternion.identity);
            else
                SimpleBurst.Spawn(transform.position, baseColor, speed: 12f, count: 32, size: 0.4f, life: 0.7f);

            ApplyExplosionForce();
            CameraShakeService.Pulse(transform.position, size > 1 ? 0.4f : 0.12f);

            if (sealedIndicator != null) Destroy(sealedIndicator);

            if (size > 1)
            {
                Vector3 fwd = blastDir; fwd.y = 0f;
                bool hasDir = fwd.sqrMagnitude > 0.01f;
                if (hasDir) fwd.Normalize();
                float strength = Mathf.Clamp01(blastStrength01);
                float fwdSpeed = fragmentSpread * (1f + 2.2f * strength);
                float inheritFactor = hasDir ? 0.25f : 0.4f;

                for (int i = 0; i < fragmentCount; i++)
                {
                    var frag = Instantiate(gameObject, transform.position + Random.insideUnitSphere * 0.4f, Random.rotation);
                    var fa = frag.GetComponent<Asteroid>();
                    fa.size = size - 1;
                    fa.consumed = false;
                    fa.damage = 0f;
                    fa.type = type;
                    fa.requiredVisits.Clear();
                    fa.visitIndex = 0;
                    frag.transform.localScale = transform.localScale * fragmentScaleMul;

                    var frb = frag.GetComponent<Rigidbody>();
                    if (frb != null)
                    {
                        Vector3 kick;
                        if (hasDir)
                        {
                            float angle = Random.Range(-45f, 45f);
                            kick = Quaternion.AngleAxis(angle, Vector3.up) * fwd;
                        }
                        else
                        {
                            Vector3 r = Random.onUnitSphere; r.y = 0f;
                            if (r.sqrMagnitude < 0.01f) r = Vector3.right;
                            kick = r.normalized;
                        }
                        frb.linearVelocity = rb.linearVelocity * inheritFactor + kick * fwdSpeed;
                    }
                }
            }

            Destroy(gameObject);
        }

        public void Consume()
        {
            if (consumed) return;
            consumed = true;
            if (sealedIndicator != null) Destroy(sealedIndicator);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (sealedIndicator != null) Destroy(sealedIndicator);
        }
    }
}
