using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class DroneThruster : MonoBehaviour
    {
        [SerializeField] DroneController droneCtrl;
        [SerializeField] Rigidbody droneRb;

        [Header("Emission")]
        [SerializeField] float minEmitSpeed = 1.5f;
        [SerializeField] float speedRef = 17f;
        [SerializeField] int cruiseMaxRate = 60;
        [SerializeField] int boostExtraRate = 90;
        [SerializeField] float trailOffset = 0.45f;
        [SerializeField] float trailDriftFactor = 0.35f;

        [Header("Particle look")]
        [SerializeField] float lifetimeMin = 0.18f;
        [SerializeField] float lifetimeMax = 0.55f;
        [SerializeField] float startSizeMin = 0.18f;
        [SerializeField] float startSizeMax = 0.45f;

        [Header("Colors (HDR — additive)")]
        [SerializeField] Color cruiseColor = new Color(1.0f, 1.6f, 2.4f, 1f);
        [SerializeField] Color boostColor = new Color(2.6f, 1.6f, 0.55f, 1f);

        ParticleSystem ps;
        ParticleSystem.MainModule main;
        ParticleSystem.EmissionModule emission;
        ParticleSystem.VelocityOverLifetimeModule velOverLife;

        Transform thrusterTr;

        void Awake()
        {
            if (droneRb == null) droneRb = GetComponent<Rigidbody>();
            if (droneCtrl == null) droneCtrl = GetComponent<DroneController>();
            BuildThruster();
        }

        void BuildThruster()
        {
            var go = new GameObject("Thruster");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            thrusterTr = go.transform;
            ps = go.AddComponent<ParticleSystem>();

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.material = MakeAdditiveMaterial();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortMode = ParticleSystemSortMode.YoungestInFront;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = LightProbeUsage.Off;

            main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetimeMin;
            main.startSpeed = 0f;
            main.startSize = startSizeMin;
            main.startColor = cruiseColor;
            main.maxParticles = 256;
            main.gravityModifier = 0f;
            main.startRotation = 0f;
            main.scalingMode = ParticleSystemScalingMode.Local;

            emission = ps.emission;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            shape.radiusThickness = 0f;

            velOverLife = ps.velocityOverLifetime;
            velOverLife.enabled = true;
            velOverLife.space = ParticleSystemSimulationSpace.World;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.6f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.7f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLife.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(0.4f, 0.85f);
            sizeCurve.AddKey(1f, 0.15f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ps.Play();
        }

        Material MakeAdditiveMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        void LateUpdate()
        {
            if (droneRb == null || ps == null) return;
            Vector3 v = droneRb.linearVelocity;
            v.y = 0f;
            float speed = v.magnitude;

            bool moving = speed > minEmitSpeed;
            bool boosting = droneCtrl != null && droneCtrl.IsBoosting && moving;

            float speedT = Mathf.Clamp01(speed / speedRef);

            float rate = moving ? cruiseMaxRate * speedT : 0f;
            if (boosting) rate += boostExtraRate * speedT;
            emission.rateOverTime = rate;

            Color baseCol = boosting
                ? boostColor
                : Color.Lerp(cruiseColor * 0.55f, cruiseColor, speedT);
            main.startColor = baseCol;

            float sz = Mathf.Lerp(startSizeMin, startSizeMax, speedT) * (boosting ? 1.25f : 1f);
            main.startSize = sz;
            main.startLifetime = Mathf.Lerp(lifetimeMin, lifetimeMax, speedT);

            if (moving)
            {
                Vector3 backDir = -v / speed;
                thrusterTr.position = transform.position + backDir * trailOffset;
                Vector3 drift = backDir * (speed * trailDriftFactor);
                velOverLife.x = drift.x;
                velOverLife.y = 0f;
                velOverLife.z = drift.z;
            }
            else
            {
                thrusterTr.position = transform.position;
                velOverLife.x = 0f;
                velOverLife.y = 0f;
                velOverLife.z = 0f;
            }
        }
    }
}
