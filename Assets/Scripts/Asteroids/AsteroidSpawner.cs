using System.Collections.Generic;
using UnityEngine;

namespace Game.Asteroids
{
    public class AsteroidSpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] asteroidPrefabs;

        [Header("Per-level spawn cadence")]
        [SerializeField] float[] levelSpawnIntervals = { 0.45f, 0.32f, 0.22f };
        [SerializeField] int[] levelMaxConcurrent = { 28, 48, 70 };
        [SerializeField] int[] levelWaveSize = { 50, 80, 120 };
        [SerializeField] float[] levelSealedChance = { 0f, 0.35f, 0.6f };
        [SerializeField, Range(0f, 1f)] float twoStageChanceL3 = 0.45f;

        // мягкий старт уровня: первые warmupTime секунд используется длинный
        // интервал и сниженный cap — даёт игроку освоиться, дальше идёт основной режим
        [Header("Per-level warm-up (мягкий старт)")]
        [SerializeField] float[] levelWarmupTime = { 15f, 0f, 0f };
        [SerializeField] float[] levelWarmupIntervals = { 1.6f, 0f, 0f };
        [SerializeField] int[] levelWarmupCap = { 8, 0, 0 };

        [Header("Spawn shape")]
        [SerializeField] float spawnRadius = 26f;
        [SerializeField] float spawnHeight = 1f;
        [SerializeField] float impulseToCenter = 5f;

        float timer;
        float levelElapsed;
        int level = 1;
        int spawnedThisLevel;
        bool waitingForClear;
        bool completed;

        readonly List<GameObject> active = new List<GameObject>();

        public int CurrentLevel => level;
        public int MaxLevel => MaxLevelCount();
        public int SpawnedThisLevel => spawnedThisLevel;
        public int CurrentWaveSize => At(levelWaveSize, level - 1);
        public int RemainingActive => active.Count;
        public bool WaitingForClear => waitingForClear;
        public bool IsCompleted => completed;

        public float SealedChance => At(levelSealedChance, level - 1);

        // обратная совместимость: HUD читает DifficultyT
        public float DifficultyT
        {
            get
            {
                int wave = CurrentWaveSize;
                if (wave <= 0) return 1f;
                return Mathf.Clamp01((float)spawnedThisLevel / wave);
            }
        }

        void Update()
        {
            if (completed) return;

            active.RemoveAll(g => g == null);

            if (waitingForClear)
            {
                if (active.Count == 0)
                {
                    if (level < MaxLevelCount())
                    {
                        level++;
                        spawnedThisLevel = 0;
                        timer = 0f;
                        levelElapsed = 0f;
                        waitingForClear = false;
                    }
                    else
                    {
                        completed = true;
                    }
                }
                return;
            }

            levelElapsed += Time.deltaTime;

            int wave = CurrentWaveSize;
            if (wave > 0 && spawnedThisLevel >= wave)
            {
                waitingForClear = true;
                return;
            }

            bool inWarmup = levelElapsed < At(levelWarmupTime, level - 1);
            int cap = inWarmup ? At(levelWarmupCap, level - 1) : At(levelMaxConcurrent, level - 1);
            if (active.Count >= cap) return;

            float interval = inWarmup ? At(levelWarmupIntervals, level - 1) : At(levelSpawnIntervals, level - 1);
            timer += Time.deltaTime;
            if (timer < interval) return;
            timer = 0f;
            Spawn();
            spawnedThisLevel++;
        }

        void Spawn()
        {
            if (asteroidPrefabs == null || asteroidPrefabs.Length == 0) return;
            var prefab = asteroidPrefabs[Random.Range(0, asteroidPrefabs.Length)];
            if (prefab == null) return;

            Vector2 c = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 pos = new Vector3(c.x, spawnHeight, c.y);
            var obj = Instantiate(prefab, pos, Random.rotation);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 toCenter = (Vector3.zero - pos);
                toCenter.y = 0f;
                toCenter = toCenter.sqrMagnitude > 0.01f ? toCenter.normalized : Vector3.right;
                Vector3 jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                rb.linearVelocity = toCenter * impulseToCenter + jitter;
            }

            var ast = obj.GetComponent<Asteroid>();
            if (ast != null && SealedChance > 0f && Random.value < SealedChance)
            {
                int stages = (level >= 3 && Random.value < twoStageChanceL3) ? 2 : 1;
                var visits = new List<AsteroidType>(stages);
                for (int i = 0; i < stages; i++)
                {
                    AsteroidType v;
                    int guard = 0;
                    do { v = (AsteroidType)Random.Range(0, 3); guard++; }
                    while (guard < 6 && visits.Count > 0 && visits[visits.Count - 1] == v);
                    visits.Add(v);
                }
                ast.SetSealed(visits);
            }

            active.Add(obj);
        }

        T At<T>(T[] arr, int idx)
        {
            if (arr == null || arr.Length == 0) return default;
            if (idx < 0) idx = 0;
            if (idx >= arr.Length) idx = arr.Length - 1;
            return arr[idx];
        }

        int MaxLevelCount()
        {
            int n = levelWaveSize?.Length ?? 0;
            return n > 0 ? n : 1;
        }
    }
}
