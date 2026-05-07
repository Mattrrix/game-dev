using UnityEngine;

namespace Game.Magnet
{
    public class SimpleBurst : MonoBehaviour
    {
        public float lifetime = 0.7f;
        public int particleCount = 24;
        public float speed = 7f;
        public float size = 0.3f;
        public Color color = Color.white;
        public bool worldSpace = true;

        ParticleSystem ps;
        bool configured;

        public static GameObject Spawn(Vector3 pos, Color color, float speed = 7f, int count = 24, float size = 0.3f, float life = 0.7f)
        {
            var go = new GameObject("VFX_Burst");
            go.transform.position = pos;
            var b = go.AddComponent<SimpleBurst>();
            b.color = color;
            b.speed = speed;
            b.particleCount = count;
            b.size = size;
            b.lifetime = life;
            return go;
        }

        void Start()
        {
            if (configured) return;
            configured = true;
            Configure();
        }

        void Configure()
        {
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = Mathf.Max(64, particleCount * 2);
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, particleCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.5f;

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                psr.material = new Material(sh);
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            ps.Play();
        }
    }
}
