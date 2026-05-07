using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Magnet
{
    public class ChargeAuraVisual : MonoBehaviour
    {
        [SerializeField] int ringSegments = 64;
        [SerializeField] int dotSegments = 14;

        [Header("Cursor reticle")]
        [SerializeField] float cursorRingMin = 0.55f;
        [SerializeField] float cursorRingMax = 1.4f;
        [SerializeField] float cursorRingWidth = 0.045f;
        [SerializeField] float cursorHaloWidth = 0.16f;
        [SerializeField] float tickInner = 0.18f;
        [SerializeField] float tickLength = 0.32f;
        [SerializeField] float tickWidth = 0.05f;
        [SerializeField] float dotRadius = 0.07f;
        [SerializeField] float reticleSpinDeg = 22f;

        [Header("Colors (HDR-friendly, additive blend)")]
        [SerializeField] Color coreCold = new Color(1.6f, 2.0f, 2.4f, 1f);
        [SerializeField] Color coreHot = new Color(2.6f, 2.0f, 0.8f, 1f);
        [SerializeField] Color haloCold = new Color(0.4f, 0.85f, 1.4f, 0.4f);
        [SerializeField] Color haloHot = new Color(1.6f, 1.0f, 0.3f, 0.55f);

        [SerializeField] float yOffset = 0.12f;

        LineRenderer cursorRing;
        LineRenderer cursorHalo;
        LineRenderer cursorDot;
        LineRenderer[] ticks;
        Material additiveMat;

        void Awake()
        {
            additiveMat = MakeAdditiveMaterial();
            cursorRing = BuildLine("CursorRing", true, ringSegments);
            cursorHalo = BuildLine("CursorHalo", true, ringSegments);
            cursorDot = BuildLine("CursorDot", true, dotSegments);
            ticks = new LineRenderer[4];
            for (int i = 0; i < 4; i++) ticks[i] = BuildLine($"Tick_{i}", false, 2);
            Hide();
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

        LineRenderer BuildLine(string name, bool loop, int positions)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = additiveMat;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = positions;
            lr.numCornerVertices = 6;
            lr.numCapVertices = 4;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off;
            return lr;
        }

        public void Show(Vector3 droneOrigin, Vector3 cursor, float charge01)
        {
            if (cursorRing == null) return;
            float t = Time.time;
            float c = Mathf.Clamp01(charge01);
            float ease = c * c * (3f - 2f * c);

            Color core = Color.Lerp(coreCold, coreHot, ease);
            Color halo = Color.Lerp(haloCold, haloHot, ease);

            // основное кольцо прицела — сжимается по мере роста заряда (target lock)
            float cursorR = Mathf.Lerp(cursorRingMax, cursorRingMin, ease);
            float cursorPulse = Mathf.Sin(t * 9f + Mathf.PI) * 0.04f;
            float spin = t * reticleSpinDeg * Mathf.Deg2Rad;
            Vector3 cCenter = cursor + Vector3.up * yOffset;

            DrawCircle(cursorRing, cCenter, cursorR + cursorPulse, spin);
            ApplyStyle(cursorRing, core, cursorRingWidth);

            DrawCircle(cursorHalo, cCenter, cursorR + cursorPulse + 0.05f, spin);
            ApplyStyle(cursorHalo, halo, cursorHaloWidth);

            // центральная точка — маленькое яркое кольцо в позиции курсора
            DrawCircle(cursorDot, cCenter, dotRadius * (0.8f + 0.4f * Mathf.Sin(t * 13f)), 0f);
            ApplyStyle(cursorDot, core, cursorRingWidth * 1.2f);

            // 4 штриха-тика по сторонам света за пределами кольца, вращаются вместе с прицелом
            float tickIn = cursorR + cursorPulse + tickInner;
            float tickOut = tickIn + tickLength * (0.5f + 0.7f * c);
            for (int i = 0; i < 4; i++)
            {
                float baseAngle = i * Mathf.PI * 0.5f + spin;
                Vector3 dir = new Vector3(Mathf.Cos(baseAngle), 0f, Mathf.Sin(baseAngle));
                ticks[i].SetPosition(0, cCenter + dir * tickIn);
                ticks[i].SetPosition(1, cCenter + dir * tickOut);
                ApplyStyle(ticks[i], core, tickWidth);
            }
        }

        void ApplyStyle(LineRenderer lr, Color col, float width)
        {
            lr.startColor = col;
            lr.endColor = col;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.enabled = true;
        }

        void DrawCircle(LineRenderer lr, Vector3 center, float radius, float rotationRad)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + rotationRad;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        public void Hide()
        {
            if (cursorRing != null) cursorRing.enabled = false;
            if (cursorHalo != null) cursorHalo.enabled = false;
            if (cursorDot != null) cursorDot.enabled = false;
            if (ticks != null)
                for (int i = 0; i < ticks.Length; i++)
                    if (ticks[i] != null) ticks[i].enabled = false;
        }
    }
}
