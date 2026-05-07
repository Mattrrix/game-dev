using UnityEngine;

namespace Game.Asteroids
{
    [RequireComponent(typeof(MeshFilter))]
    public class AsteroidMeshDeformer : MonoBehaviour
    {
        [Header("Displacement")]
        [SerializeField] float deformAmount = 0.5f;
        [SerializeField] float baseFreq = 1.4f;
        [SerializeField] int octaves = 5;
        [SerializeField] float lacunarity = 2.1f;
        [SerializeField] float persistence = 0.55f;
        [SerializeField] float warpStrength = 0.55f;

        [Header("Shape")]
        [SerializeField] float axisJitter = 0.22f;

        [Header("Look")]
        [SerializeField] bool flatShading = true;
        [SerializeField, Range(0f, 1f)] float craterChance = 0.35f;
        [SerializeField] float craterDepth = 0.22f;

        const string DeformedMarker = "_Deformed";
        static Mesh pristineSphere;

        void Awake()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Mesh source = mf.sharedMesh;
            if (source.name.Contains(DeformedMarker))
                source = GetPristineSphere();

            Mesh deformed = MakeDeformedCopy(source);
            if (flatShading) MakeFlatShaded(deformed);
            mf.mesh = deformed;
        }

        static Mesh GetPristineSphere()
        {
            if (pristineSphere != null) return pristineSphere;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pristineSphere = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp.GetComponent<Collider>());
            Destroy(temp);
            return pristineSphere;
        }

        Mesh MakeDeformedCopy(Mesh source)
        {
            Mesh m = Instantiate(source);
            m.name = source.name + DeformedMarker;

            Vector3[] verts = m.vertices;
            Vector3 seed = new Vector3(
                Random.Range(-500f, 500f),
                Random.Range(-500f, 500f),
                Random.Range(-500f, 500f));

            Vector3 axisScale = new Vector3(
                1f + Random.Range(-axisJitter, axisJitter),
                1f + Random.Range(-axisJitter, axisJitter),
                1f + Random.Range(-axisJitter, axisJitter));

            int craterCount = (Random.value < craterChance) ? Random.Range(1, 4) : 0;
            Vector3[] craterDirs = new Vector3[craterCount];
            float[] craterRadii = new float[craterCount];
            float[] craterDepths = new float[craterCount];
            for (int c = 0; c < craterCount; c++)
            {
                craterDirs[c] = Random.onUnitSphere;
                craterRadii[c] = Random.Range(0.25f, 0.55f);
                craterDepths[c] = craterDepth * Random.Range(0.6f, 1.4f);
            }

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                Vector3 n = v.normalized;

                Vector3 warp = new Vector3(
                    Sample(n * 1.3f + seed + new Vector3(11f, 0f, 0f)) - 0.5f,
                    Sample(n * 1.3f + seed + new Vector3(0f, 17f, 0f)) - 0.5f,
                    Sample(n * 1.3f + seed + new Vector3(0f, 0f, 23f)) - 0.5f) * warpStrength;

                Vector3 sp = n * baseFreq + seed + warp;

                float disp = 0f;
                float amp = 1f;
                float freq = 1f;
                float ampSum = 0f;
                for (int o = 0; o < octaves; o++)
                {
                    disp += amp * (Sample(sp * freq) - 0.5f);
                    ampSum += amp;
                    freq *= lacunarity;
                    amp *= persistence;
                }
                disp /= ampSum;

                float craterDisp = 0f;
                for (int c = 0; c < craterCount; c++)
                {
                    float dot = Vector3.Dot(n, craterDirs[c]);
                    if (dot > 1f - craterRadii[c])
                    {
                        float t = (dot - (1f - craterRadii[c])) / craterRadii[c];
                        craterDisp -= craterDepths[c] * Mathf.SmoothStep(0f, 1f, t);
                    }
                }

                Vector3 stretched = Vector3.Scale(v, axisScale);
                verts[i] = stretched + n * (disp * deformAmount + craterDisp);
            }

            m.vertices = verts;
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        void MakeFlatShaded(Mesh m)
        {
            int[] tris = m.triangles;
            Vector3[] origVerts = m.vertices;

            Vector3[] flatVerts = new Vector3[tris.Length];
            int[] flatTris = new int[tris.Length];
            for (int i = 0; i < tris.Length; i++)
            {
                flatVerts[i] = origVerts[tris[i]];
                flatTris[i] = i;
            }

            m.Clear();
            m.vertices = flatVerts;
            m.triangles = flatTris;
            m.RecalculateNormals();
            m.RecalculateBounds();
        }

        static float Sample(Vector3 p)
        {
            float xy = Mathf.PerlinNoise(p.x, p.y);
            float yz = Mathf.PerlinNoise(p.y + 31.7f, p.z + 5.3f);
            float xz = Mathf.PerlinNoise(p.x + 17.2f, p.z + 41.1f);
            return (xy + yz + xz) / 3f;
        }
    }
}
