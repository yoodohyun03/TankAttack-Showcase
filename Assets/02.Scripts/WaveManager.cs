using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 웨이브 진행과 승리/패배 판정을 담당
// 1) 웨이브마다 정해진 적을 플레이어 주변(원형)에 순서대로 소환
// 2) 소환한 적이 모두 처치되면 잠시 쉬고 다음 웨이브
// 3) 마지막 웨이브까지 버티면 승리, 플레이어 HP가 0이 되면 패배
public class WaveManager : MonoBehaviour
{
    [Serializable]
    public class Wave
    {
        public int scout;
        public int heavy;
        public int artillery;
        public float spawnInterval = 1.5f; // 한 마리씩 소환하는 간격
    }

    [Header("웨이브 구성 (Inspector에서 수정 가능)")]
    public List<Wave> waves = new List<Wave>
    {
        new Wave { scout = 3 },
        new Wave { scout = 4, heavy = 1 },
        new Wave { scout = 3, heavy = 2, artillery = 1 },
        new Wave { scout = 4, heavy = 2, artillery = 2 },
        new Wave { scout = 5, heavy = 3, artillery = 3, spawnInterval = 1.0f },
    };

    [Header("진행")]
    public float firstWaveDelay = 3.0f;  // 게임 시작 후 첫 웨이브까지 대기
    public float breakTime = 5.0f;       // 웨이브 사이 휴식 시간
    public float endSceneDelay = 2.0f;   // 승리/패배 후 엔딩 씬으로 넘어가기 전 대기

    [Header("난이도 조절")]
    public float playerMaxHp = 300.0f;             // 시작 시 플레이어 최대 HP로 적용
    [Range(0.0f, 1.0f)] public float healOnWaveClear = 0.3f; // 웨이브 클리어 시 최대 HP의 몇 %를 회복할지
    public float enemyDamageScale = 1.0f;          // 적 공격력 배율 (프리팹 수치에 곱함)
    public float enemyHpScale = 1.0f;              // 적 체력 배율

    [Header("소환 위치 (플레이어 기준 원형)")]
    public float spawnRadiusMin = 180.0f;
    public float spawnRadiusMax = 240.0f;
    public float maxSpawnSlope = 20.0f;       // 이보다 가파른 비탈에는 소환하지 않음 (도)
    public float maxSpawnHeightDiff = 25.0f;  // 플레이어와 높이 차가 이보다 크면 소환하지 않음 (산 위 방지)

    [Header("엔딩 씬 이름 (Build Settings에 등록되어 있어야 이동)")]
    public string winSceneName = "scWin";
    public string loseSceneName = "scLose";

    [Header("테스트용 화면 표시 (UI 완성 후 끄기)")]
    public bool showDebugHud = true;
    public bool showEnemyMarkers = true; // 적 위치·거리 표시 (게임 플레이용이라 따로 끌 수 있게 분리)

    // UI에서 바로 쓸 수 있는 현재 상태 문구 ("WAVE 2 / 5", "WAVE 2 클리어!" 등)
    public string StatusText => statusText;

    // UI가 구독해서 쓰는 이벤트
    public event Action<int, int> OnWaveStarted;     // (현재 웨이브 번호, 전체 웨이브 수)
    public event Action<int> OnEnemiesLeftChanged;   // 남은 적 수
    public event Action<float> OnBreakStarted;       // 휴식 시간(초)
    public event Action<bool> OnGameEnded;           // true: 승리, false: 패배

    public int CurrentWave { get; private set; }     // 1부터 시작, 0이면 시작 전
    public int EnemiesLeft { get; private set; }
    public bool IsGameOver { get; private set; }

    Transform player;
    TankHealth playerHealth;
    Terrain terrain;
    readonly Dictionary<EnemyTank.EnemyType, GameObject> prefabs = new Dictionary<EnemyTank.EnemyType, GameObject>();
    string statusText = "";
    readonly List<EnemyTank> aliveEnemies = new List<EnemyTank>();

    void Awake()
    {
        prefabs[EnemyTank.EnemyType.Scout] = Resources.Load<GameObject>("Enemies/Enemy_Scout");
        prefabs[EnemyTank.EnemyType.Heavy] = Resources.Load<GameObject>("Enemies/Enemy_Heavy");
        prefabs[EnemyTank.EnemyType.Artillery] = Resources.Load<GameObject>("Enemies/Enemy_Artillery");
        terrain = Terrain.activeTerrain;
    }

    void OnEnable()
    {
        EnemyTank.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        EnemyTank.OnEnemyDied -= HandleEnemyDied;
        if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDied;
    }

    void Start()
    {
        TankMove move = FindAnyObjectByType<TankMove>();
        if (move == null)
        {
            Debug.LogError("WaveManager: 플레이어 탱크(TankMove)를 찾지 못했습니다.");
            return;
        }
        player = move.transform;
        playerHealth = player.GetComponent<TankHealth>();
        if (playerHealth != null)
        {
            playerHealth.SetMaxHp(playerMaxHp);
            playerHealth.OnDeath += HandlePlayerDied;
        }

        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        statusText = "곧 적이 몰려옵니다!";
        yield return new WaitForSeconds(firstWaveDelay);

        for (int i = 0; i < waves.Count; i++)
        {
            CurrentWave = i + 1;
            Wave wave = waves[i];
            EnemiesLeft = wave.scout + wave.heavy + wave.artillery;
            statusText = $"WAVE {CurrentWave} / {waves.Count}";
            OnWaveStarted?.Invoke(CurrentWave, waves.Count);
            OnEnemiesLeftChanged?.Invoke(EnemiesLeft);
            Debug.Log($"웨이브 {CurrentWave} 시작: 정찰형 {wave.scout}, 중전차 {wave.heavy}, 자주포 {wave.artillery}");

            // 종류를 섞어서 한 마리씩 소환
            foreach (EnemyTank.EnemyType type in BuildSpawnOrder(wave))
            {
                if (IsGameOver) yield break;
                Spawn(type);
                yield return new WaitForSeconds(wave.spawnInterval);
            }

            // 이번 웨이브의 적이 모두 처치될 때까지 대기
            yield return new WaitUntil(() => EnemiesLeft <= 0 || IsGameOver);
            if (IsGameOver) yield break;

            if (i < waves.Count - 1)
            {
                // 웨이브 클리어 보상: 체력 일부 회복
                if (playerHealth != null) playerHealth.Heal(playerHealth.maxHp * healOnWaveClear);
                statusText = $"WAVE {CurrentWave} 클리어! HP 회복, 다음 웨이브 준비";
                OnBreakStarted?.Invoke(breakTime);
                yield return new WaitForSeconds(breakTime);
            }
        }

        EndGame(true);
    }

    List<EnemyTank.EnemyType> BuildSpawnOrder(Wave wave)
    {
        List<EnemyTank.EnemyType> order = new List<EnemyTank.EnemyType>();
        for (int n = 0; n < wave.scout; n++) order.Add(EnemyTank.EnemyType.Scout);
        for (int n = 0; n < wave.heavy; n++) order.Add(EnemyTank.EnemyType.Heavy);
        for (int n = 0; n < wave.artillery; n++) order.Add(EnemyTank.EnemyType.Artillery);
        // 섞기 (Fisher–Yates)
        for (int n = order.Count - 1; n > 0; n--)
        {
            int k = UnityEngine.Random.Range(0, n + 1);
            (order[n], order[k]) = (order[k], order[n]);
        }
        return order;
    }

    void Spawn(EnemyTank.EnemyType type)
    {
        GameObject prefab = prefabs[type];
        if (prefab == null)
        {
            Debug.LogError($"WaveManager: {type} 프리팹이 없습니다. Tools > TankAttack > Build Enemy Prefabs를 먼저 실행하세요.");
            EnemiesLeft--;
            return;
        }

        Vector3 pos = GetSpawnPosition();
        Vector3 toPlayer = player.position - pos;
        toPlayer.y = 0.0f;
        GameObject obj = Instantiate(prefab, pos, Quaternion.LookRotation(toPlayer));
        EnemyTank enemy = obj.GetComponent<EnemyTank>();
        enemy.damage *= enemyDamageScale;
        TankHealth enemyHealth = obj.GetComponent<TankHealth>();
        enemyHealth.SetMaxHp(enemyHealth.maxHp * enemyHpScale);
        aliveEnemies.Add(enemy);
        Debug.Log($"{type} 소환: {pos} (플레이어와 거리 {Vector3.Distance(pos, player.position):0}m)");
    }

    // 플레이어 주변 원 위의 무작위 지점 중, 탱크가 서 있을 수 있는 곳을 고름
    // - 맵 밖, 급경사(산비탈), 플레이어와 높이 차가 큰 곳(산 위)은 제외하고 다시 뽑음
    // - 계속 실패하면 반지름을 줄여서 플레이어 가까운 평지에서 찾음
    Vector3 GetSpawnPosition()
    {
        const int maxTries = 40;
        for (int t = 0; t < maxTries; t++)
        {
            float shrink = t < maxTries / 2 ? 1.0f : 0.5f; // 절반 넘게 실패하면 반경 절반으로
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            float radius = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax) * shrink;
            Vector3 pos = player.position + new Vector3(dir.x, 0.0f, dir.y) * radius;

            if (terrain == null) return pos;
            if (IsValidSpawn(pos, out Vector3 ground)) return ground;
        }
        Debug.LogWarning("WaveManager: 적당한 소환 위치를 찾지 못해 플레이어 근처에 소환합니다.");
        Vector3 fallback = player.position + player.forward * 60.0f;
        return terrain != null ? GroundAt(fallback) : fallback;
    }

    bool IsValidSpawn(Vector3 pos, out Vector3 ground)
    {
        ground = pos;
        Vector3 tPos = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        const float margin = 30.0f;
        if (pos.x < tPos.x + margin || pos.x > tPos.x + size.x - margin) return false;
        if (pos.z < tPos.z + margin || pos.z > tPos.z + size.z - margin) return false;

        float nx = (pos.x - tPos.x) / size.x;
        float nz = (pos.z - tPos.z) / size.z;
        if (terrain.terrainData.GetSteepness(nx, nz) > maxSpawnSlope) return false;

        ground = GroundAt(pos);
        if (Mathf.Abs(ground.y - player.position.y) > maxSpawnHeightDiff) return false;
        return true;
    }

    Vector3 GroundAt(Vector3 pos)
    {
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 1.5f;
        return pos;
    }

    void HandleEnemyDied(EnemyTank enemy)
    {
        aliveEnemies.Remove(enemy);
        if (IsGameOver) return;
        EnemiesLeft = Mathf.Max(0, EnemiesLeft - 1);
        OnEnemiesLeftChanged?.Invoke(EnemiesLeft);
    }

    void HandlePlayerDied()
    {
        EndGame(false);
    }

    void EndGame(bool win)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        statusText = win ? "승리!" : "패배...";
        Debug.Log(win ? "모든 웨이브 클리어: 승리" : "플레이어 파괴: 패배");
        OnGameEnded?.Invoke(win);
        StartCoroutine(LoadEndScene(win ? winSceneName : loseSceneName));
    }

    IEnumerator LoadEndScene(string sceneName)
    {
        yield return new WaitForSeconds(endSceneDelay);
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning($"WaveManager: '{sceneName}' 씬이 Build Settings에 없어서 이동하지 않습니다. (엔딩 씬 제작 후 등록 필요)");
        }
    }

    // 임시 화면 표시. 직접 만든 UI가 완성되면 showDebugHud를 끄면 됨
    void OnGUI()
    {
        // 지도가 열려 있으면 지도 쪽 표시와 헷갈리지 않도록 화면 적 표시는 숨김
        if (showEnemyMarkers && !MapStrike.IsMapOpen) DrawEnemyMarkers();
        if (!showDebugHud) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        string hp = playerHealth != null ? $"   HP {playerHealth.CurrHp:0}/{playerHealth.maxHp:0}" : "";
        string left = CurrentWave > 0 && !IsGameOver ? $"   남은 적 {EnemiesLeft}" : "";
        GUI.Label(new Rect(20, 20, 900, 40), statusText + left + hp, style);
    }

    // 적 위치 표시: 화면 안이면 적 위에, 화면 밖이면 화면 가장자리에 방향과 거리를 표시
    void DrawEnemyMarkers()
    {
        Camera cam = Camera.main;
        if (cam == null || player == null) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1.0f, 0.3f, 0.25f);
        const float edge = 40.0f;

        foreach (EnemyTank enemy in aliveEnemies)
        {
            if (enemy == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * 6.0f);
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 p = new Vector2(sp.x, sp.y);
            bool behind = sp.z < 0.0f;
            if (behind) p = center - (p - center); // 카메라 뒤에 있으면 좌표가 뒤집혀 나오므로 되돌림
            bool onScreen = !behind && p.x > 0 && p.x < Screen.width && p.y > 0 && p.y < Screen.height;
            if (!onScreen)
            {
                // 화면 밖: 화면 중앙에서 적 방향으로 쭉 뻗어 가장자리에 붙임
                Vector2 dir = (p - center).normalized;
                if (behind && dir.sqrMagnitude < 0.01f) dir = Vector2.down;
                p = center + dir * Mathf.Max(Screen.width, Screen.height);
            }
            float x = Mathf.Clamp(p.x, edge, Screen.width - edge);
            float y = Mathf.Clamp(Screen.height - p.y, edge, Screen.height - edge); // GUI는 y축이 위에서 아래로
            float dist = Vector3.Distance(player.position, enemy.transform.position);
            string label = (onScreen ? "▼" : "◆") + $"\n{dist:0}m";
            GUI.Label(new Rect(x - 40, y - 20, 80, 40), label, style);
        }
    }
}
