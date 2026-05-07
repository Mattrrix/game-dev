using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Magnet
{
    public class ChargeArrowVisual : MonoBehaviour
    {
        [SerializeField] int chevronCount = 3;
        [SerializeField] float chevronBaseSize = 0.55f;
        [SerializeField] float chevronAngleDeg = 32f;
        [SerializeField] float lineWidthBase = 0.05f;

        [Header("Length-driven scaling")]
        [SerializeField] float beamLenRef = 6f;
        [SerializeField] float beamLenMin = 0.6f;
        [SerializeField] float beamLenMax = 2.6f;

        [Header("Colors (HDR additive — match ChargeAuraVisual)")]
        [SerializeField] Color coreCold = new Color(1.6f, 2.0f, 2.4f, 1f);
        [SerializeField] Color coreHot = new Color(2.6f, 2.0f, 0.8f, 1f);

        [SerializeField] float yOffset = 0.18f;

        LineRenderer[] lines;
        Material additiveMat;

        void Awake()
        {
            additiveMat = MakeAdditiveMaterial();
            BuildChevrons();
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

        void BuildChevrons()
        {
            lines = new LineRenderer[chevronCount];
            for (int i = 0; i < chevronCount; i++)
            {
                var go = new GameObject($"Chevron_{i}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = additiveMat;
                lr.useWorldSpace = true;
                lr.positionCount = 3;
                lr.numCornerVertices = 4;
                lr.numCapVertices = 4;
                lr.alignment = LineAlignment.View;
                lr.startWidth = lineWidthBase;
                lr.endWidth = lineWidthBase;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.lightProbeUsage = LightProbeUsage.Off;
                lines[i] = lr;
            }
        }

        public void Show(Vector3 origin, Vector3 target, float charge01)
        {
            if (lines == null) return;
            Vector3 beam = target - origin;
            beam.y = 0f;
            float beamLen = beam.magnitude;
            if (beamLen < 0.01f) { Hide(); return; }

            Vector3 dir = beam / beamLen;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            float ang = chevronAngleDeg * Mathf.Deg2Rad;
            Vector3 back = -dir;

            float charge = Mathf.Clamp01(charge01);
            float ease = charge * charge * (3f - 2f * charge);

            float lengthScale = Mathf.Clamp(beamLen / beamLenRef, beamLenMin, beamLenMax);
            float size = chevronBaseSize * lengthScale * (0.6f + 0.6f * charge);
            float width = lineWidthBase * lengthScale * (0.7f + 1.4f * charge);
            Color col = Color.Lerp(coreCold, coreHot, ease);

            for (int i = 0; i < chevronCount; i++)
            {
                float threshold = (i + 1f) / (chevronCount + 1f);
                bool visible = charge >= threshold * 0.55f;
                lines[i].enabled = visible;
                if (!visible) continue;

                float along = (i + 1f) / chevronCount;
                Vector3 apex = origin + dir * (beamLen * along) + Vector3.up * yOffset;
                Vector3 leftPt = apex + (Mathf.Cos(ang) * back + Mathf.Sin(ang) * right) * size;
                Vector3 rightPt = apex + (Mathf.Cos(ang) * back - Mathf.Sin(ang) * right) * size;

                lines[i].SetPosition(0, leftPt);
                lines[i].SetPosition(1, apex);
                lines[i].SetPosition(2, rightPt);
                lines[i].startColor = col;
                lines[i].endColor = col;
                lines[i].startWidth = width;
                lines[i].endWidth = width;
            }
        }

        public void Hide()
        {
            if (lines == null) return;
            foreach (var l in lines)
                if (l) l.enabled = false;
        }
    }
}
