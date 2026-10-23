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

    [Header("보스 · 아군")]
    public bool bossOnLastWave = true;   // 마지막 웨이브 끝에 보스 등장
    public int allyCount = 2;            // 함께 싸우는 아군 AI 수
    public bool reinforceAllies = true;  // 웨이브 클리어 때마다 파괴된 아군을 다시 채움(증원)

    [Header("채점용 단축키 (F10: 즉시 승리, F9: 즉시 패배)")]
    public bool enableCheatKeys = true;

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
    public float maxPathRise = 15.0f;         // 플레이어까지 가는 길에 이보다 높은 산등성이가 있으면 소환하지 않음 (m)
    public float maxPathSlope = 30.0f;        // 플레이어까지 가는 길에 이보다 가파른 곳이 있으면 소환하지 않음 (도)

    [Header("자주포: 산 중턱 고정 포대 (지도 포격 대상)")]
    public bool artilleryOnHills = true;
    public float hillRadiusMin = 150.0f;      // 플레이어와의 거리 (자주포 사거리 260m, 지도 포격 사거리 450m 안)
    public float hillRadiusMax = 240.0f;
    public float hillMinHeight = 15.0f;       // 플레이어보다 이만큼 이상 높은 곳 (산 중턱)
    public float hillMaxHeight = 70.0f;
    public float hillMinSlope = 8.0f;         // 비탈 (너무 평평하면 산이 아님)
    public float hillMaxSlope = 30.0f;        // 너무 가파르면 서 있을 수 없음

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

    // Intro의 "보스전 바로가기" 버튼이 켬 → 이번 판은 마지막(보스) 웨이브부터 시작 (한 번 쓰면 자동으로 꺼짐)
    public static bool SkipToLastWave = false;

    public int CurrentWave { get; private set; }     // 1부터 시작, 0이면 시작 전
    public int EnemiesLeft { get; private set; }
    public bool IsGameOver { get; private set; }

    Transform player;
    TankHealth playerHealth;
    Terrain terrain;
    readonly Dictionary<EnemyTank.EnemyType, GameObject> prefabs = new Dictionary<EnemyTank.EnemyType, GameObject>();
    string statusText = "";
    readonly List<EnemyTank> aliveEnemies = new List<EnemyTank>();
    readonly List<EnemyTank> allies = new List<EnemyTank>();
    GameObject allyPrefab;

    void Awake()
    {
        prefabs[EnemyTank.EnemyType.Scout] = Resources.Load<GameObject>("Enemies/Enemy_Scout");
        prefabs[EnemyTank.EnemyType.Heavy] = Resources.Load<GameObject>("Enemies/Enemy_Heavy");
        prefabs[EnemyTank.EnemyType.Artillery] = Resources.Load<GameObject>("Enemies/Enemy_Artillery");
        prefabs[EnemyTank.EnemyType.Boss] = Resources.Load<GameObject>("Enemies/Enemy_Boss");
        allyPrefab = Resources.Load<GameObject>("Allies/Ally_Tank");
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

        SpawnAllies();
        StartCoroutine(RunWaves());
    }

    // 아군 AI를 플레이어 근처에 allyCount만큼 채움 (이미 살아 있는 아군은 그대로)
    void SpawnAllies()
    {
        if (allyPrefab == null) return;
        allies.RemoveAll(a => a == null || a.Health.IsDead);
        int need = allyCount - allies.Count;
        for (int n = 0; n < need; n++)
        {
            // 플레이어 양옆 30m 근처, 건물과 겹치면 플레이어 주위를 돌면서 빈자리를 찾음
            float side = (n % 2 == 0) ? 1.0f : -1.0f;
            Vector3 baseOffset = (player.right * side * 30.0f) - player.forward * (10.0f + n * 10.0f);
            Vector3 pos = player.position + baseOffset;
            for (int t = 0; t < 12; t++)
            {
                Vector3 offset = Quaternion.Euler(0.0f, t * 30.0f * side, 0.0f) * baseOffset;
                pos = player.position + offset;
                if (terrain != null) pos = GroundAt(pos);
                if (IsFreeOfBuildings(pos)) break;
            }
            GameObject obj = Instantiate(allyPrefab, pos, player.rotation);
            allies.Add(obj.GetComponent<EnemyTank>());
        }
        if (need > 0 && CurrentWave > 0) Debug.Log($"아군 증원 {need}대");
    }

    IEnumerator RunWaves()
    {
        int startIndex = 0;
        if (SkipToLastWave)
        {
            // 보스전 바로가기: 마지막 웨이브부터 (다음 판에는 처음부터 하도록 바로 끔)
            SkipToLastWave = false;
            startIndex = waves.Count - 1;
            statusText = "보스전 바로 시작!";
        }
        else
        {
            statusText = "곧 적이 몰려옵니다!";
        }
        yield return new WaitForSeconds(firstWaveDelay);

        for (int i = startIndex; i < waves.Count; i++)
        {
            CurrentWave = i + 1;
            Wave wave = waves[i];
            bool bossWave = bossOnLastWave && i == waves.Count - 1 && prefabs[EnemyTank.EnemyType.Boss] != null;
            EnemiesLeft = wave.scout + wave.heavy + wave.artillery + (bossWave ? 1 : 0);
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

            // 마지막 웨이브: 일반 적을 다 소환한 뒤 보스 등장
            if (bossWave && !IsGameOver)
            {
                yield return new WaitForSeconds(2.0f);
                statusText = "⚠ 보스 전차 등장! ⚠";
                Spawn(EnemyTank.EnemyType.Boss);
            }

            // 이번 웨이브의 적이 모두 처치될 때까지 대기
            yield return new WaitUntil(() => EnemiesLeft <= 0 || IsGameOver);
            if (IsGameOver) yield break;

            if (i < waves.Count - 1)
            {
                // 웨이브 클리어 보상: 체력 일부 회복
                if (playerHealth != null) playerHealth.Heal(playerHealth.maxHp * healOnWaveClear);
                if (reinforceAllies) SpawnAllies();
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

        // 자주포는 산 중턱 고정 포대로 (지도 포격으로 처리하기 좋은 대상), 못 찾으면 평소처럼
        bool hillArtillery = type == EnemyTank.EnemyType.Artillery && artilleryOnHills && terrain != null;
        Vector3 pos = Vector3.zero;
        bool onHill = hillArtillery && TryGetHillSpawnPosition(out pos);
        if (!onHill) pos = GetSpawnPosition();

        Vector3 toPlayer = player.position - pos;
        toPlayer.y = 0.0f;
        GameObject obj = Instantiate(prefab, pos, Quaternion.LookRotation(toPlayer));
        EnemyTank enemy = obj.GetComponent<EnemyTank>();
        enemy.damage *= enemyDamageScale;
        if (hillArtillery) enemy.stationary = true; // 그 자리에 고정, 차체와 포탑만 돌려서 포격
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
        if (!IsFreeOfBuildings(ground)) return false;
        if (!HasClearPathToPlayer(ground)) return false;
        return true;
    }

    // 자주포용: 플레이어 주변 150~240m 중 "산 중턱"인 자리를 찾음
    // - 플레이어보다 15~70m 높고, 경사가 8~30°(서 있을 수 있는 비탈)인 곳
    // - 플레이어와 그 자리 사이에 더 높은 산등성이가 없는 곳 (= 전장 쪽을 바라보는 안쪽 비탈, 산 바깥쪽 비탈 제외)
    // - 맵 가장자리와 건물은 피함
    bool TryGetHillSpawnPosition(out Vector3 result)
    {
        result = Vector3.zero;
        Vector3 tPos = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        const float margin = 30.0f;

        for (int t = 0; t < 80; t++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            float radius = UnityEngine.Random.Range(hillRadiusMin, hillRadiusMax);
            Vector3 pos = player.position + new Vector3(dir.x, 0.0f, dir.y) * radius;
            if (pos.x < tPos.x + margin || pos.x > tPos.x + size.x - margin) continue;
            if (pos.z < tPos.z + margin || pos.z > tPos.z + size.z - margin) continue;

            Vector3 ground = GroundAt(pos);
            float rise = ground.y - player.position.y;
            if (rise < hillMinHeight || rise > hillMaxHeight) continue;

            float steep = terrain.terrainData.GetSteepness((pos.x - tPos.x) / size.x, (pos.z - tPos.z) / size.z);
            if (steep < hillMinSlope || steep > hillMaxSlope) continue;

            if (HasHigherRidgeBetween(ground)) continue;
            if (!IsFreeOfBuildings(ground)) continue;

            result = ground;
            return true;
        }
        Debug.Log("WaveManager: 자주포를 둘 산 중턱을 찾지 못해 평지에 고정 배치합니다.");
        return false;
    }

    // 소환 자리와 플레이어 사이에 소환 자리보다 높은 산등성이가 있으면 true (산 바깥쪽 비탈)
    bool HasHigherRidgeBetween(Vector3 from)
    {
        Vector3 to = player.position;
        float dist = Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z));
        int steps = Mathf.CeilToInt(dist / 5.0f);
        for (int i = 1; i < steps; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
            float h = terrain.SampleHeight(p) + terrain.transform.position.y;
            if (h > from.y + 5.0f) return true;
        }
        return false;
    }

    // 소환 위치에서 플레이어까지 직선으로 가는 길에 넘을 수 없는 산등성이나 급경사가 없는지 확인
    // (적 AI는 길 찾기 없이 거의 직진하므로, 전장을 둘러싼 산 바깥에 생기면 안으로 들어오지 못함)
    bool HasClearPathToPlayer(Vector3 from)
    {
        Vector3 to = player.position;
        float baseY = Mathf.Max(from.y, to.y);
        float dist = Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z));
        int steps = Mathf.CeilToInt(dist / 5.0f); // 5m 간격으로 확인
        Vector3 tPos = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        for (int i = 1; i < steps; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
            float h = terrain.SampleHeight(p) + tPos.y;
            if (h - baseY > maxPathRise) return false; // 중간에 높은 산등성이

            float steep = terrain.terrainData.GetSteepness((p.x - tPos.x) / size.x, (p.z - tPos.z) / size.z);
            if (steep > maxPathSlope) return false;    // 전차가 오를 수 없는 급경사
        }
        return true;
    }

    // 소환 위치 주변에 건물 같은 장애물이 없는지 (겹쳐서 소환되면 물리 엔진이 강하게 밀어내 튕겨 나감)
    bool IsFreeOfBuildings(Vector3 pos)
    {
        Collider[] cols = Physics.OverlapSphere(pos + Vector3.up * 3.0f, 12.0f, ~(1 << 8), QueryTriggerInteraction.Ignore);
        foreach (Collider c in cols)
        {
            if (c.GetComponentInParent<TankHealth>() == null) return false;
        }
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

    // 채점·확인용 단축키: 플레이 도중 바로 엔딩으로 (Intro에는 표시하지 않고 기획서·README에만 안내)
    //   F10: 즉시 승리 (5웨이브와 보스를 모두 깬 것으로 처리)   F9: 즉시 패배
    void Update()
    {
        if (!enableCheatKeys || IsGameOver || PauseState.IsPaused) return;

        if (Input.GetKeyDown(KeyCode.F10))
        {
            Debug.Log("채점용 단축키: 즉시 승리");
            KillAllEnemies();
            EndGame(true);
        }
        else if (Input.GetKeyDown(KeyCode.F9))
        {
            Debug.Log("채점용 단축키: 즉시 패배");
            EndGame(false);
        }
    }

    // 승리 연출: 남은 적을 모두 파괴 (폭발 이펙트가 터지면서 엔딩으로 넘어감)
    void KillAllEnemies()
    {
        foreach (EnemyTank enemy in aliveEnemies)
        {
            if (enemy != null && !enemy.Health.IsDead) enemy.Health.TakeDamage(enemy.Health.maxHp * 10.0f);
        }
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
    // 아군은 화면 안에 있을 때만 파란색으로 표시
    void DrawEnemyMarkers()
    {
        Camera cam = Camera.main;
        if (cam == null || player == null) return;

        foreach (EnemyTank enemy in aliveEnemies)
        {
            if (enemy == null) continue;
            bool boss = enemy.type == EnemyTank.EnemyType.Boss;
            DrawMarker(cam, enemy.transform, boss ? new Color(1.0f, 0.15f, 0.6f) : new Color(1.0f, 0.3f, 0.25f), boss ? "BOSS" : "", true, boss ? 20 : 16);
        }
        foreach (EnemyTank ally in allies)
        {
            if (ally == null || ally.Health.IsDead) continue;
            DrawMarker(cam, ally.transform, new Color(0.4f, 0.75f, 1.0f), "아군", false, 14);
        }
    }

    void DrawMarker(Camera cam, Transform t, Color color, string tag, bool showOffScreen, int fontSize)
    {
        const float edge = 40.0f;
        Vector3 sp = cam.WorldToScreenPoint(t.position + Vector3.up * 6.0f);
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 p = new Vector2(sp.x, sp.y);
        bool behind = sp.z < 0.0f;
        if (behind) p = center - (p - center); // 카메라 뒤에 있으면 좌표가 뒤집혀 나오므로 되돌림
        bool onScreen = !behind && p.x > 0 && p.x < Screen.width && p.y > 0 && p.y < Screen.height;
        if (!onScreen)
        {
            if (!showOffScreen) return;
            // 화면 밖: 화면 중앙에서 대상 방향으로 쭉 뻗어 가장자리에 붙임
            Vector2 dir = (p - center).normalized;
            if (behind && dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            p = center + dir * Mathf.Max(Screen.width, Screen.height);
        }
        float x = Mathf.Clamp(p.x, edge, Screen.width - edge);
        float y = Mathf.Clamp(Screen.height - p.y, edge, Screen.height - edge); // GUI는 y축이 위에서 아래로
        float dist = Vector3.Distance(player.position, t.position);

        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = color;
        string head = string.IsNullOrEmpty(tag) ? (onScreen ? "▼" : "◆") : (onScreen ? $"▼ {tag}" : $"◆ {tag}");
        GUI.Label(new Rect(x - 60, y - 22, 120, 44), $"{head}\n{dist:0}m", style);
    }
}
