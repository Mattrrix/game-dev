using UnityEngine;

namespace Game.Core
{
    public class ScorePopup : MonoBehaviour
    {
        public string text = "+10";
        public Color color = Color.white;
        public float lifetime = 1.0f;
        public float floatSpeed = 2.5f;

        Vector3 startPos;
        float age;
        Camera cam;
        GUIStyle style;

        public static GameObject Spawn(Vector3 worldPos, string text, Color color)
        {
            var go = new GameObject("ScorePopup");
            go.transform.position = worldPos;
            var p = go.AddComponent<ScorePopup>();
            p.text = text;
            p.color = color;
            return go;
        }

        void Start()
        {
            cam = Camera.main;
            startPos = transform.position;
            Destroy(gameObject, lifetime);
        }

        void OnGUI()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / lifetime);

            Vector3 worldPos = startPos + Vector3.up * (floatSpeed * age);
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z < 0f) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 30,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            var c = color;
            c.a = Mathf.Pow(1f - k, 1.4f);
            style.normal.textColor = c;

            float w = 160f, h = 50f;
            GUI.Label(new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h * 0.5f, w, h), text, style);
        }
    }
}
