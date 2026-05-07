using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Magnet;

namespace Game.Player
{
    public class DroneVisualBuilder : MonoBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] bool freezeBodyRotation = true;
        [SerializeField] bool faceVelocity = true;
        [SerializeField] float chassisTurnSpeed = 5f;
        [SerializeField] float minFaceSpeed = 1.0f;
        [SerializeField] bool inverseScale = true;

        [Header("Wheels")]
        [SerializeField] float wheelRadius = 0.18f;

        [Header("Turret")]
        [SerializeField] float turretTrackSpeed = 12f;

        [Header("Shot beam")]
        [SerializeField] float beamLifetime = 0.22f;
        [SerializeField] float beamWidthMin = 0.1f;
        [SerializeField] float beamWidthMax = 0.32f;
        [SerializeField] Color beamColor = new Color(0.5f, 1.7f, 2.5f);
        [SerializeField] Color muzzleColor = new Color(1.4f, 1.9f, 2.6f);

        Rigidbody rb;
        DroneController controller;
        MagneticPulse magnet;
        Camera cam;
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        Transform visualRoot;
        Transform turretPivot;
        Transform barrelTip;
        readonly List<Transform> wheels = new List<Transform>();

        LineRenderer beamLR;
        Material beamMat;
        float beamElapsed;
        bool beamActive;
        Vector3 beamEndPos;

        MeshRenderer warningMr;
        Material warningMat;
        readonly Color warningEmissionOn = new Color(2.6f, 0.18f, 0.05f);

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            controller = GetComponent<DroneController>();
            magnet = GetComponent<MagneticPulse>();
            if (rb != null)
            {
                if (freezeBodyRotation) rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            BuildRover();
            BuildBeam();
        }

        void Start()
        {
            cam = Camera.main;
            if (magnet != null) magnet.OnRepelFired += HandleRepelFired;
        }

        void OnDestroy()
        {
            if (magnet != null) magnet.OnRepelFired -= HandleRepelFired;
        }

        void BuildRover()
        {
            var rootGo = new GameObject("RoverVisual");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = Vector3.zero;
            rootGo.transform.localRotation = Quaternion.identity;
            if (inverseScale)
            {
                var s = transform.localScale;
                rootGo.transform.localScale = new Vector3(
                    Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
                    Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
                    Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);
            }
            visualRoot = rootGo.transform;

            var hullMat        = MakeLit(new Color(0.55f, 0.50f, 0.44f), 0.55f, 0.45f);
            var detailMat      = MakeLit(new Color(0.32f, 0.30f, 0.27f), 0.7f, 0.55f);
            var darkMat        = MakeLit(new Color(0.10f, 0.11f, 0.13f), 0.75f, 0.6f);
            var rubberMat      = MakeLit(new Color(0.07f, 0.07f, 0.08f), 0.05f, 0.25f);
            var hubMat         = MakeLit(new Color(0.7f, 0.7f, 0.75f), 0.95f, 0.7f);
            var panelCol       = new Color(0.05f, 0.08f, 0.18f);
            var panelMat       = MakeLit(panelCol, 0.4f, 0.95f, panelCol * 0.4f);
            var headlightMat   = MakeLit(new Color(0.05f, 0.05f, 0.05f), 0.3f, 0.5f, new Color(2.4f, 2.2f, 1.8f));
            var lensMat        = MakeLit(new Color(0.05f, 0.1f, 0.15f), 0.6f, 0.95f, new Color(0.4f, 1.4f, 2.0f) * 0.7f);
            var turretBaseMat  = MakeLit(new Color(0.20f, 0.21f, 0.23f), 0.7f, 0.55f);
            var barrelMat      = MakeLit(new Color(0.13f, 0.14f, 0.16f), 0.85f, 0.55f);
            var barrelGlowMat  = MakeLit(new Color(0.04f, 0.07f, 0.12f), 0.2f, 0.6f, new Color(0.4f, 1.3f, 2.0f));
            warningMat         = MakeLit(new Color(0.3f, 0.04f, 0.04f), 0.3f, 0.4f, warningEmissionOn);

            Vector3 chassisSize = new Vector3(0.85f, 0.32f, 1.6f);
            MakeChild(visualRoot, PrimitiveType.Cube, "Chassis",
                Vector3.zero, chassisSize, Quaternion.identity, hullMat, true);

            MakeChild(visualRoot, PrimitiveType.Cube, "Nose",
                new Vector3(0f, -0.04f, chassisSize.z * 0.55f),
                new Vector3(chassisSize.x * 0.85f, chassisSize.y * 0.7f, 0.18f),
                Quaternion.Euler(20f, 0f, 0f), hullMat, false);

            MakeChild(visualRoot, PrimitiveType.Cube, "Bumper",
                new Vector3(0f, -0.05f, -chassisSize.z * 0.5f - 0.05f),
                new Vector3(chassisSize.x * 0.95f, chassisSize.y * 0.55f, 0.12f),
                Quaternion.identity, detailMat, false);

            float wheelX = chassisSize.x * 0.5f + 0.18f;
            float wheelY = -chassisSize.y * 0.55f;
            float[] wheelZs = { -0.6f, 0f, 0.6f };
            foreach (var z in wheelZs)
            {
                BuildWheel(new Vector3(-wheelX, wheelY, z), rubberMat, hubMat);
                BuildWheel(new Vector3( wheelX, wheelY, z), rubberMat, hubMat);
            }

            MakeChild(visualRoot, PrimitiveType.Cube, "SolarPanel",
                new Vector3(0f, chassisSize.y * 0.55f + 0.05f, -chassisSize.z * 0.25f),
                new Vector3(chassisSize.x * 1.05f, 0.04f, chassisSize.z * 0.45f),
                Quaternion.Euler(-8f, 0f, 0f), panelMat, false);
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                float t = i / 2.5f;
                MakeChild(visualRoot, PrimitiveType.Cube, $"PanelGrid_{i}",
                    new Vector3(t * chassisSize.x * 0.45f, chassisSize.y * 0.55f + 0.07f, -chassisSize.z * 0.25f),
                    new Vector3(0.015f, 0.005f, chassisSize.z * 0.43f),
                    Quaternion.Euler(-8f, 0f, 0f), darkMat, false);
            }

            MakeChild(visualRoot, PrimitiveType.Sphere, "HeadlightL",
                new Vector3(-chassisSize.x * 0.32f, 0.0f, chassisSize.z * 0.55f + 0.04f),
                new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, headlightMat, false);
            MakeChild(visualRoot, PrimitiveType.Sphere, "HeadlightR",
                new Vector3( chassisSize.x * 0.32f, 0.0f, chassisSize.z * 0.55f + 0.04f),
                new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, headlightMat, false);

            MakeChild(visualRoot, PrimitiveType.Cylinder, "MastPole",
                new Vector3(0f, chassisSize.y * 0.5f + 0.18f, chassisSize.z * 0.4f),
                new Vector3(0.05f, 0.18f, 0.05f),
                Quaternion.identity, detailMat, false);
            MakeChild(visualRoot, PrimitiveType.Cube, "SensorHead",
                new Vector3(0f, chassisSize.y * 0.5f + 0.42f, chassisSize.z * 0.4f),
                new Vector3(0.18f, 0.12f, 0.22f),
                Quaternion.identity, detailMat, false);
            MakeChild(visualRoot, PrimitiveType.Sphere, "SensorLens",
                new Vector3(0f, chassisSize.y * 0.5f + 0.42f, chassisSize.z * 0.4f + 0.13f),
                new Vector3(0.07f, 0.07f, 0.07f),
                Quaternion.identity, lensMat, false);

            MakeChild(visualRoot, PrimitiveType.Cylinder, "Antenna",
                new Vector3(-chassisSize.x * 0.28f, chassisSize.y * 0.5f + 0.22f, -chassisSize.z * 0.42f),
                new Vector3(0.04f, 0.22f, 0.04f),
                Quaternion.identity, detailMat, false);
            var warningGo = MakeChild(visualRoot, PrimitiveType.Sphere, "WarningLight",
                new Vector3(-chassisSize.x * 0.28f, chassisSize.y * 0.5f + 0.5f, -chassisSize.z * 0.42f),
                new Vector3(0.09f, 0.09f, 0.09f),
                Quaternion.identity, warningMat, false);
            warningMr = warningGo.GetComponent<MeshRenderer>();

            MakeChild(visualRoot, PrimitiveType.Cube, "RTG",
                new Vector3(chassisSize.x * 0.3f, chassisSize.y * 0.5f + 0.1f, -chassisSize.z * 0.42f),
                new Vector3(0.2f, 0.16f, 0.18f),
                Quaternion.identity, darkMat, false);
            MakeChild(visualRoot, PrimitiveType.Cube, "RTGFins",
                new Vector3(chassisSize.x * 0.3f, chassisSize.y * 0.5f + 0.21f, -chassisSize.z * 0.42f),
                new Vector3(0.18f, 0.05f, 0.16f),
                Quaternion.identity, hubMat, false);

            var pivotGo = new GameObject("TurretPivot");
            pivotGo.transform.SetParent(visualRoot, false);
            pivotGo.transform.localPosition = new Vector3(0f, chassisSize.y * 0.5f + 0.05f, -0.05f);
            turretPivot = pivotGo.transform;

            MakeChild(turretPivot, PrimitiveType.Cylinder, "TurretBase",
                Vector3.zero, new Vector3(0.42f, 0.08f, 0.42f),
                Quaternion.identity, turretBaseMat, false);
            MakeChild(turretPivot, PrimitiveType.Cube, "TurretBlock",
                new Vector3(0f, 0.1f, 0.05f), new Vector3(0.32f, 0.16f, 0.34f),
                Quaternion.identity, turretBaseMat, false);

            float barrelLen = 0.55f;
            MakeChild(turretPivot, PrimitiveType.Cylinder, "Barrel",
                new Vector3(0f, 0.1f, 0.22f + barrelLen * 0.5f),
                new Vector3(0.11f, barrelLen * 0.5f, 0.11f),
                Quaternion.Euler(90f, 0f, 0f), barrelMat, false);
            MakeChild(turretPivot, PrimitiveType.Cylinder, "Muzzle",
                new Vector3(0f, 0.1f, 0.22f + barrelLen + 0.02f),
                new Vector3(0.14f, 0.025f, 0.14f),
                Quaternion.Euler(90f, 0f, 0f), turretBaseMat, false);
            MakeChild(turretPivot, PrimitiveType.Cylinder, "BarrelCore",
                new Vector3(0f, 0.1f, 0.22f + barrelLen + 0.04f),
                new Vector3(0.09f, 0.02f, 0.09f),
                Quaternion.Euler(90f, 0f, 0f), barrelGlowMat, false);

            var tipGo = new GameObject("BarrelTip");
            tipGo.transform.SetParent(turretPivot, false);
            tipGo.transform.localPosition = new Vector3(0f, 0.1f, 0.22f + barrelLen + 0.07f);
            barrelTip = tipGo.transform;
        }

        void BuildWheel(Vector3 localPos, Material rubberMat, Material hubMat)
        {
            var tire = MakeChild(visualRoot, PrimitiveType.Cylinder, "Wheel",
                localPos,
                new Vector3(0.36f, 0.085f, 0.36f),
                Quaternion.Euler(0f, 0f, 90f), rubberMat, true);
            MakeChild(tire.transform, PrimitiveType.Cylinder, "Hub",
                Vector3.zero,
                new Vector3(0.42f, 1.04f, 0.42f),
                Quaternion.identity, hubMat, false);
            wheels.Add(tire.transform);
        }

        void BuildBeam()
        {
            var bgo = new GameObject("ChargeBeam");
            bgo.transform.SetParent(transform, false);
            bgo.transform.localPosition = Vector3.zero;
            beamLR = bgo.AddComponent<LineRenderer>();
            beamMat = MakeAdditiveMaterial();
            beamLR.material = beamMat;
            beamLR.useWorldSpace = true;
            beamLR.positionCount = 2;
            beamLR.numCornerVertices = 4;
            beamLR.numCapVertices = 4;
            beamLR.alignment = LineAlignment.View;
            beamLR.shadowCastingMode = ShadowCastingMode.Off;
            beamLR.receiveShadows = false;
            beamLR.lightProbeUsage = LightProbeUsage.Off;
            beamLR.enabled = false;
        }

        void HandleRepelFired(Vector3 cursor, float charge01)
        {
            if (barrelTip == null || beamLR == null) return;
            beamEndPos = cursor;
            beamLR.SetPosition(0, barrelTip.position);
            beamLR.SetPosition(1, cursor);
            beamLR.enabled = true;
            beamElapsed = 0f;
            beamActive = true;

            float intensity = 0.7f + 0.6f * charge01;
            SimpleBurst.Spawn(barrelTip.position, muzzleColor, speed: 7f * intensity, count: 16, size: 0.18f, life: 0.22f);
            SimpleBurst.Spawn(barrelTip.position + (cursor - barrelTip.position).normalized * 0.3f, beamColor, speed: 4f, count: 8, size: 0.14f, life: 0.18f);
        }

        void LateUpdate()
        {
            float speed = 0f;
            if (rb != null)
            {
                Vector3 v = rb.linearVelocity;
                v.y = 0f;
                speed = v.magnitude;
            }

            if (faceVelocity && controller != null && speed > minFaceSpeed)
            {
                Vector3 dir = controller.LastInputDir;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * chassisTurnSpeed);
                }
            }

            if (wheels.Count > 0 && wheelRadius > 0.001f)
            {
                float deltaDeg = (speed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;
                for (int i = 0; i < wheels.Count; i++)
                {
                    if (wheels[i] != null) wheels[i].Rotate(Vector3.up, deltaDeg, Space.Self);
                }
            }

            if (turretPivot != null && TryGetCursorWorld(out Vector3 cursor))
            {
                Vector3 dir = cursor - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    turretPivot.rotation = Quaternion.Slerp(turretPivot.rotation, target, Time.deltaTime * turretTrackSpeed);
                }
            }

            if (warningMr != null && warningMat != null)
            {
                bool on = (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2) == 0;
                warningMat.SetColor("_EmissionColor", on ? warningEmissionOn : warningEmissionOn * 0.05f);
            }

            if (beamActive && beamLR != null)
            {
                beamElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(beamElapsed / beamLifetime);
                float eased = 1f - t;
                float width = Mathf.Lerp(beamWidthMin, beamWidthMax, eased);
                beamLR.SetPosition(0, barrelTip.position);
                beamLR.SetPosition(1, beamEndPos);
                Color cs = beamColor; cs.a = eased;
                Color ce = beamColor; ce.a = eased * 0.5f;
                beamLR.startWidth = width;
                beamLR.endWidth = width * 0.6f;
                beamLR.startColor = cs;
                beamLR.endColor = ce;
                if (beamElapsed >= beamLifetime)
                {
                    beamLR.enabled = false;
                    beamActive = false;
                }
            }
        }

        bool TryGetCursorWorld(out Vector3 worldPos)
        {
            if (magnet != null && magnet.TryGetGroundCursor(out worldPos)) return true;
            worldPos = default;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (groundPlane.Raycast(ray, out float enter))
            {
                worldPos = ray.GetPoint(enter);
                return true;
            }
            return false;
        }

        GameObject MakeChild(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat, bool castShadow)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = castShadow;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            return go;
        }

        Material MakeLit(Color baseColor, float metallic, float smoothness)
        {
            return MakeLit(baseColor, metallic, smoothness, Color.black);
        }

        Material MakeLit(Color baseColor, float metallic, float smoothness, Color emission)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);
            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if ((emission.r + emission.g + emission.b) > 0.001f)
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }
            return mat;
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
    }
}
