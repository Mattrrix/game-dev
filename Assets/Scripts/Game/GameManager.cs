using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Game.Asteroids;
using Game.Magnet;

namespace Game.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        const string HighScoreKey = "asteroid_recycle_high_score";

        public enum GameState { Start, Playing, Paused, Victory }

        [SerializeField] MagneticPulse magnet;
        [SerializeField] AsteroidSpawner spawner;

        [Header("Combo")]
        [SerializeField] float comboWindow = 2.5f;

        int score;
        int correctCount;
        int wrongCount;
        int highScore;
        bool newRecord;
        int combo = 1;
        int bestCombo = 1;
        float lastSortTime = -1000f;
        GameState state = GameState.Start;

        Texture2D pixelTex;

        public int Score => score;
        public int Combo => combo;
        public float ComboTimeRemaining => Mathf.Max(0f, comboWindow - (Time.time - lastSortTime));
        public GameState State => state;

        static readonly Color PanelBg = new Color(0.07f, 0.08f, 0.11f, 0.96f);
        static readonly Color PanelBorder = new Color(1f, 0.85f, 0.4f, 0.55f);
        static readonly Color Accent = new Color(1f, 0.85f, 0.4f);
        static readonly Color TextMain = new Color(0.95f, 0.95f, 0.97f);
        static readonly Color TextDim = new Color(0.72f, 0.74f, 0.82f);
        static readonly Color KeyColor = new Color(0.55f, 0.92f, 1f);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            pixelTex = new Texture2D(1, 1);
            pixelTex.SetPixel(0, 0, Color.white);
            pixelTex.Apply();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int OnAsteroidSorted(Vector3 worldPos, int baseDelta, bool correct, AsteroidType type)
        {
            if (correct)
            {
                if (Time.time - lastSortTime <= comboWindow) combo++;
                else combo = 1;
                if (combo > bestCombo) bestCombo = combo;
                lastSortTime = Time.time;
                int delta = baseDelta * combo;
                score += delta;
                correctCount++;

                if (score > highScore)
                {
                    highScore = score;
                    newRecord = true;
                    PlayerPrefs.SetInt(HighScoreKey, highScore);
                }
                return delta;
            }
            else
            {
                wrongCount++;
                return 0;
            }
        }

        void Start()
        {
            if (magnet == null) magnet = FindAnyObjectByType<MagneticPulse>();
            if (spawner == null) spawner = FindAnyObjectByType<AsteroidSpawner>();
            Time.timeScale = 0f;
            state = GameState.Start;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

#if UNITY_EDITOR
            if (kb.f1Key.wasPressedThisFrame && state != GameState.Victory)
            {
                score = Mathf.Max(score, 1230);
                correctCount = Mathf.Max(correctCount, 42);
                bestCombo = Mathf.Max(bestCombo, 7);
                EnterVictory();
                return;
            }
#endif

            if (state == GameState.Start)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                    BeginPlay();
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (state == GameState.Playing) Pause();
                else if (state == GameState.Paused) Resume();
                return;
            }

            if (state == GameState.Paused) return;

            if (kb.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (state == GameState.Playing && spawner != null && spawner.IsCompleted)
            {
                EnterVictory();
                return;
            }

            if (combo > 1 && Time.time - lastSortTime > comboWindow)
                combo = 1;
        }

        void EnterVictory()
        {
            state = GameState.Victory;
            Time.timeScale = 0f;
        }

        void BeginPlay()
        {
            state = GameState.Playing;
            Time.timeScale = 1f;
        }

        void Pause()
        {
            state = GameState.Paused;
            Time.timeScale = 0f;
        }

        void Resume()
        {
            state = GameState.Playing;
            Time.timeScale = 1f;
        }

        void OnGUI()
        {
            if (state == GameState.Start) { DrawStartMenu(); return; }
            DrawHUD();
            if (state == GameState.Paused) DrawPauseOverlay();
            if (state == GameState.Victory) DrawVictoryOverlay();
        }

        void DrawHUD()
        {
            DrawTextWithShadow(new Rect(28, 22, 400, 50), $"{score}", 44, FontStyle.Bold, TextAnchor.UpperLeft, TextMain);

            var bestColor = newRecord ? Accent : TextDim;
            string bestLabel = newRecord ? $"★ BEST  {highScore}" : $"BEST  {highScore}";
            DrawTextWithShadow(new Rect(28, 76, 400, 26), bestLabel, 18, FontStyle.Normal, TextAnchor.UpperLeft, bestColor);

            if (spawner != null)
            {
                int lvl = spawner.CurrentLevel;
                int max = spawner.MaxLevel;
                Color lvlCol = spawner.WaitingForClear ? Accent : TextMain;
                DrawTextWithShadow(new Rect(Screen.width - 308, 24, 280, 32), $"LEVEL  {lvl}/{max}", 22, FontStyle.Bold, TextAnchor.UpperRight, lvlCol);
                if (spawner.WaitingForClear)
                    DrawTextWithShadow(new Rect(Screen.width - 308, 56, 280, 24), $"осталось  {spawner.RemainingActive}", 16, FontStyle.Normal, TextAnchor.UpperRight, Accent);
            }

            if (combo > 1)
            {
                float t = ComboTimeRemaining / comboWindow;
                Color comboCol = Color.Lerp(new Color(1f, 0.5f, 0.2f), new Color(1f, 0.95f, 0.3f), t);
                DrawTextWithShadow(new Rect(Screen.width / 2 - 200, 24, 400, 56), $"x{combo} COMBO", 40, FontStyle.Bold, TextAnchor.MiddleCenter, comboCol);

                float barW = 240f;
                float barX = (Screen.width - barW) * 0.5f;
                FillRect(new Rect(barX, 84, barW, 4), new Color(0f, 0f, 0f, 0.55f));
                FillRect(new Rect(barX, 84, barW * t, 4), comboCol);
            }

            if (magnet != null && magnet.IsCharging)
            {
                float c = magnet.Charge01;
                float barW = 320f;
                float barH = 14f;
                float barX = (Screen.width - barW) * 0.5f;
                float barY = Screen.height - 56f;
                FillRect(new Rect(barX - 2, barY - 2, barW + 4, barH + 4), new Color(0f, 0f, 0f, 0.55f));
                FillRect(new Rect(barX, barY, barW, barH), new Color(0.1f, 0.12f, 0.16f, 0.95f));
                Color fillCol = Color.Lerp(new Color(0.4f, 0.85f, 1f), new Color(1f, 0.4f, 0.15f), c);
                FillRect(new Rect(barX, barY, barW * c, barH), fillCol);
                DrawTextWithShadow(new Rect(0, barY - 28f, Screen.width, 24f), $"CHARGE  {(int)(c * 100)}%", 18, FontStyle.Bold, TextAnchor.MiddleCenter, fillCol);
            }
        }

        void DrawStartMenu()
        {
            float w = 920f;
            float h = 700f;
            float x, y;
            DrawCenteredPanel(w, h, out x, out y);

            DrawTextWithShadow(new Rect(x, y + 48, w, 70), "ASTEROID  RECYCLER", 56, FontStyle.Bold, TextAnchor.MiddleCenter, Accent);
            FillRect(new Rect(x + w * 0.5f - 90, y + 122, 180, 2), Accent);

            DrawTextCentered(new Rect(x, y + 138, w, 28), "Сортируй обломки по секторам соответствующего цвета", 19, FontStyle.Normal, TextDim);

            DrawControls(x + 56, y + 186, w - 112);

            DrawTextCentered(new Rect(x, y + h - 70, w, 28), "Space / Enter — начать       Esc — пауза в игре", 20, FontStyle.Bold, Accent);
        }

        void DrawVictoryOverlay()
        {
            float w = 760f;
            float h = 560f;
            float x, y;
            DrawCenteredPanel(w, h, out x, out y);

            DrawTextWithShadow(new Rect(x, y + 50, w, 70), "MISSION COMPLETE", 48, FontStyle.Bold, TextAnchor.MiddleCenter, Accent);
            FillRect(new Rect(x + w * 0.5f - 130, y + 124, 260, 2), Accent);

            DrawTextCentered(new Rect(x, y + 144, w, 28), "Все три уровня пройдены", 19, FontStyle.Normal, TextDim);

            DrawTextCentered(new Rect(x, y + 196, w, 60), $"{score}", 64, FontStyle.Bold, TextMain);
            DrawTextCentered(new Rect(x, y + 264, w, 26), "ИТОГОВЫЙ СЧЁТ", 16, FontStyle.Bold, TextDim);

            if (newRecord)
                DrawTextCentered(new Rect(x, y + 296, w, 28), $"★ НОВЫЙ РЕКОРД",  20, FontStyle.Bold, Accent);
            else
                DrawTextCentered(new Rect(x, y + 296, w, 28), $"Лучший результат:  {highScore}", 18, FontStyle.Normal, TextDim);

            float statsY = y + 348f;
            float colW = w * 0.5f;
            DrawTextCentered(new Rect(x, statsY, colW, 26), $"Лучшее комбо", 16, FontStyle.Normal, TextDim);
            DrawTextCentered(new Rect(x + colW, statsY, colW, 26), $"Сортировок", 16, FontStyle.Normal, TextDim);
            DrawTextCentered(new Rect(x, statsY + 24, colW, 36), $"x{bestCombo}", 28, FontStyle.Bold, KeyColor);
            DrawTextCentered(new Rect(x + colW, statsY + 24, colW, 36), $"{correctCount}", 28, FontStyle.Bold, KeyColor);

            DrawTextCentered(new Rect(x, y + h - 60, w, 28), "R — играть заново", 20, FontStyle.Bold, Accent);
        }

        void DrawPauseOverlay()
        {
            float w = 920f;
            float h = 660f;
            float x, y;
            DrawCenteredPanel(w, h, out x, out y);

            DrawTextWithShadow(new Rect(x, y + 44, w, 64), "ПАУЗА", 52, FontStyle.Bold, TextAnchor.MiddleCenter, Accent);
            FillRect(new Rect(x + w * 0.5f - 70, y + 118, 140, 2), Accent);

            DrawControls(x + 56, y + 150, w - 112);

            DrawTextCentered(new Rect(x, y + h - 64, w, 28), "Esc — продолжить       R — рестарт", 20, FontStyle.Bold, Accent);
        }

        void DrawControls(float x, float y, float w)
        {
            DrawTextWithShadow(new Rect(x, y, w, 30), "УПРАВЛЕНИЕ", 20, FontStyle.Bold, TextAnchor.UpperLeft, Accent);
            FillRect(new Rect(x, y + 32, 110, 2), Accent);

            string[] keys = {
                "WASD",
                "L Shift",
                "Space",
                "ЛКМ",
                "R",
                "Esc"
            };
            string[] desc = {
                "движение дрона",
                "ускорение",
                "магнитное притяжение к курсору",
                "зажать → отпустить — выпустить заряженный импульс",
                "рестарт уровня",
                "пауза / возврат в игру"
            };

            float keyColW = 150f;
            float gap = 24f;
            float rowH = 32f;
            float rowsTop = y + 52f;
            for (int i = 0; i < keys.Length; i++)
            {
                float ry = rowsTop + i * rowH;
                DrawTextWithShadow(new Rect(x, ry, keyColW, rowH), keys[i], 19, FontStyle.Bold, TextAnchor.MiddleLeft, KeyColor);
                DrawTextWithShadow(new Rect(x + keyColW + gap, ry, w - keyColW - gap, rowH), desc[i], 18, FontStyle.Normal, TextAnchor.MiddleLeft, TextMain, true);
            }

            float tipsY = rowsTop + keys.Length * rowH + 24f;
            DrawTextWithShadow(new Rect(x, tipsY, w, 30), "ПОДСКАЗКИ", 20, FontStyle.Bold, TextAnchor.UpperLeft, Accent);
            FillRect(new Rect(x, tipsY + 32, 110, 2), Accent);

            string[] tips = {
                "Голубые льдины — слабые, жёлтый металл — крепче, красный камень — самый прочный.",
                "Серые опечатанные астероиды нужно завезти в подсвеченный сектор, а потом разбить.",
                "За серию подряд правильных сортировок растёт комбо-множитель к очкам."
            };
            float tipY = tipsY + 50f;
            for (int i = 0; i < tips.Length; i++)
            {
                float lh = 26f;
                DrawTextWithShadow(new Rect(x + 16, tipY, w - 16, lh * 2), "•  " + tips[i], 18, FontStyle.Normal, TextAnchor.UpperLeft, TextDim, true);
                tipY += lh + 6f;
            }
        }

        void DrawCenteredPanel(float w, float h, out float x, out float y)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.7f));
            x = (Screen.width - w) * 0.5f;
            y = (Screen.height - h) * 0.5f;
            FillRect(new Rect(x - 4, y - 4, w + 8, h + 8), PanelBorder);
            FillRect(new Rect(x, y, w, h), PanelBg);
            FillRect(new Rect(x, y, w, 3), Accent);
        }

        void FillRect(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, pixelTex);
            GUI.color = prev;
        }

        void DrawTextWithShadow(Rect r, string text, int size, FontStyle weight, TextAnchor anchor, Color color, bool wrap = false)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = weight,
                alignment = anchor,
                wordWrap = wrap,
                richText = false
            };
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onNormal.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            style.onFocused.background = null;

            var shadowCol = new Color(0f, 0f, 0f, 0.7f);
            style.normal.textColor = shadowCol;
            style.hover.textColor = shadowCol;
            style.active.textColor = shadowCol;
            style.focused.textColor = shadowCol;
            style.onNormal.textColor = shadowCol;
            style.onHover.textColor = shadowCol;
            style.onActive.textColor = shadowCol;
            style.onFocused.textColor = shadowCol;
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);

            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
            GUI.Label(r, text, style);
        }

        void DrawTextCentered(Rect r, string text, int size, FontStyle weight, Color color)
        {
            DrawTextWithShadow(r, text, size, weight, TextAnchor.MiddleCenter, color);
        }
    }
}
