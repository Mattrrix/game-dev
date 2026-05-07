using UnityEngine;
using UnityEngine.Rendering;
using Game.Asteroids;

namespace Game.Sectors
{
    [RequireComponent(typeof(SortingSector))]
    [RequireComponent(typeof(BoxCollider))]
    public class SectorProcessingFx : MonoBehaviour
    {
        [Header("Corner posts")]
        [SerializeField] float postRadius = 0.18f;
        [SerializeField] float postCapMul = 1.5f;
        [SerializeField] float postExtraHeight = 0.4f;

        [Header("Floor pad")]
        [SerializeField] float padThickness = 0.06f;
        [SerializeField] float padInset = 0.5f;
        [SerializeField] float padEmissionMul = 0.75f;
        [SerializeField] float padBaseDarken = 0.25f;

        [Header("Suction particles")]
        [SerializeField] int particleRate = 45;
        [SerializeField] float particleFallSpeed = 3.6f;
        [SerializeField] float particleSize = 0.18f;
        [SerializeField] float particleSizeVariance = 0.4f;
        [SerializeField] int particleTextureSize = 64;

        Texture2D softCircleTex;

        void Awake()
        {
            var sector = GetComponent<SortingSector>();
            var col = GetComponent<BoxCollider>();
            if (sector == null || col == null) return;

            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            Color sectorColor = Asteroid.SectorColor(sector.AcceptedType);

            // корневой узел с обратным масштабом — позволяет задавать позиции/размеры
            // детей в мировых единицах независимо от scale сектора
            var fxRoot = new GameObject("SectorBay");
            fxRoot.transform.SetParent(transform, false);
            fxRoot.transform.localPosition = col.center;
            fxRoot.transform.localRotation = Quaternion.identity;
            var s = transform.localScale;
            fxRoot.transform.localScale = new Vector3(
                Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
                Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
                Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);

            Vector3 worldHalfSize = Vector3.Scale(col.size * 0.5f, transform.localScale);

            softCircleTex = MakeSoftCircle(particleTextureSize);

            BuildCornerPosts(fxRoot.transform, worldHalfSize, sectorColor);
            BuildFloorPad(fxRoot.transform, worldHalfSize, sectorColor);
            BuildParticles(fxRoot.transform, worldHalfSize, sectorColor);
        }

        void BuildCornerPosts(Transform root, Vector3 halfSize, Color sectorColor)
        {
            float postHeight = halfSize.y * 2f + postExtraHeight;
            var darkMat = MakeLit(new Color(0.09f, 0.10f, 0.12f), 0.65f, 0.5f);
            var capMat  = MakeLit(new Color(0.04f, 0.05f, 0.06f), 0.3f, 0.5f, sectorColor * 1.7f);

            Vector3[] corners = {
                new Vector3(-halfSize.x, 0f, -halfSize.z),
                new Vector3( halfSize.x, 0f, -halfSize.z),
                new Vector3(-halfSize.x, 0f,  halfSize.z),
                new Vector3( halfSize.x, 0f,  halfSize.z)
            };

            for (int i = 0; i < 4; i++)
            {
                var c = corners[i];
                var post = new GameObject($"Post_{i}");
                post.transform.SetParent(root, false);
                post.transform.localPosition = new Vector3(c.x, 0f, c.z);

                var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shaft.name = "Shaft";
                var sc = shaft.GetComponent<Collider>(); if (sc) Destroy(sc);
                shaft.transform.SetParent(post.transform, false);
                shaft.transform.localPosition = Vector3.zero;
                shaft.transform.localScale = new Vector3(postRadius * 2f, postHeight * 0.5f, postRadius * 2f);
                ApplyMr(shaft.GetComponent<MeshRenderer>(), darkMat, false);

                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Cap";
                var cc = cap.GetComponent<Collider>(); if (cc) Destroy(cc);
                cap.transform.SetParent(post.transform, false);
                cap.transform.localPosition = new Vector3(0f, postHeight * 0.5f + postRadius * 0.6f, 0f);
                cap.transform.localScale = Vector3.one * postRadius * 2f * postCapMul;
                ApplyMr(cap.GetComponent<MeshRenderer>(), capMat, false);
            }
        }

        void BuildFloorPad(Transform root, Vector3 halfSize, Color sectorColor)
        {
            Color padBase = new Color(sectorColor.r * padBaseDarken, sectorColor.g * padBaseDarken, sectorColor.b * padBaseDarken);
            var mat = MakeLit(padBase, 0.4f, 0.7f, sectorColor * padEmissionMul);

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "FloorPad";
            var pc = pad.GetComponent<Collider>(); if (pc) Destroy(pc);
            pad.transform.SetParent(root, false);
            float padBottom = -halfSize.y;
            pad.transform.localPosition = new Vector3(0f, padBottom + padThickness * 0.5f + 0.02f, 0f);
            pad.transform.localScale = new Vector3(
                Mathf.Max(0.1f, (halfSize.x - padInset) * 2f),
                padThickness,
                Mathf.Max(0.1f, (halfSize.z - padInset) * 2f));
            ApplyMr(pad.GetComponent<MeshRenderer>(), mat, false);

            Color trimEmis = sectorColor * 1.5f;
            var trimMat = MakeLit(padBase, 0.4f, 0.85f, trimEmis);
            float trimY = pad.transform.localPosition.y + padThickness * 0.5f + 0.001f;
            float trimW = (halfSize.x - padInset) * 2f - 0.05f;
            float trimH = (halfSize.z - padInset) * 2f - 0.05f;
            float bw = 0.08f;
            BuildPadEdge(root, new Vector3(0f, trimY, (trimH * 0.5f)),  new Vector3(trimW, 0.02f, bw), trimMat);
            BuildPadEdge(root, new Vector3(0f, trimY, -(trimH * 0.5f)), new Vector3(trimW, 0.02f, bw), trimMat);
            BuildPadEdge(root, new Vector3((trimW * 0.5f),  trimY, 0f), new Vector3(bw, 0.02f, trimH), trimMat);
            BuildPadEdge(root, new Vector3(-(trimW * 0.5f), trimY, 0f), new Vector3(bw, 0.02f, trimH), trimMat);
        }

        void BuildPadEdge(Transform root, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "PadEdge";
            var ec = edge.GetComponent<Collider>(); if (ec) Destroy(ec);
            edge.transform.SetParent(root, false);
            edge.transform.localPosition = localPos;
            edge.transform.localScale = localScale;
            ApplyMr(edge.GetComponent<MeshRenderer>(), mat, false);
        }

        void BuildParticles(Transform root, Vector3 halfSize, Color sectorColor)
        {
            var partMat = MakeAdditiveParticleMat(softCircleTex);

            var go = new GameObject("ProcessingMotes");
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            var ps = go.AddComponent<ParticleSystem>();
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.material = partMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortMode = ParticleSystemSortMode.YoungestInFront;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = LightProbeUsage.Off;

            float lifetime = (halfSize.y * 2f) / Mathf.Max(0.5f, particleFallSpeed);

            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * (1f - particleSizeVariance), particleSize * (1f + particleSizeVariance));
            main.startColor = new Color(sectorColor.r * 1.6f, sectorColor.g * 1.6f, sectorColor.b * 1.6f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.gravityModifier = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = particleRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, halfSize.y * 0.85f, 0f);
            shape.scale = new Vector3((halfSize.x - padInset) * 2f, 0.06f, (halfSize.z - padInset) * 2f);

            var velOverLife = ps.velocityOverLifetime;
            velOverLife.enabled = true;
            velOverLife.space = ParticleSystemSimulationSpace.World;
            velOverLife.x = 0f;
            velOverLife.y = -particleFallSpeed;
            velOverLife.z = 0f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var sc = new AnimationCurve();
            sc.AddKey(0f, 0.4f);
            sc.AddKey(0.3f, 1f);
            sc.AddKey(1f, 0.2f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sc);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = grad;

            ps.Play();
        }

        void ApplyMr(MeshRenderer mr, Material mat, bool castShadow)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = castShadow;
            mr.lightProbeUsage = LightProbeUsage.Off;
        }

        Material MakeLit(Color baseCol, float metallic, float smoothness)
        {
            return MakeLit(baseCol, metallic, smoothness, Color.black);
        }

        Material MakeLit(Color baseCol, float metallic, float smoothness, Color emission)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);
            mat.SetColor("_BaseColor", baseCol);
            mat.color = baseCol;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if ((emission.r + emission.g + emission.b) > 0.001f)
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return mat;
        }

        Material MakeAdditiveParticleMat(Texture2D tex)
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
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                mat.mainTexture = tex;
            }
            return mat;
        }

        Texture2D MakeSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }
    }
}
