using System;
using System.Collections;
using UnityEngine;

// AI 탱크: 가장 가까운 상대 진영 탱크를 추적하고, 사거리 안에 들어오면 탄도를 계산해서 포격한다.
// - 적(isAlly = false): 플레이어와 아군 AI를 공격
// - 아군(isAlly = true): 적을 공격하고, 적이 없으면 플레이어를 따라다님
// - 보스: 체력이 절반 아래로 떨어지면 광폭화 (연사, 속도 증가)
// 스펙(체력/속도/사거리/공격력 등)을 다르게 준 프리팹으로 종류를 나눈다.
[RequireComponent(typeof(TankHealth))]
public class EnemyTank : MonoBehaviour
{
    public enum EnemyType { Scout, Heavy, Artillery, Boss, Ally }
    public EnemyType type = EnemyType.Scout;
    public bool isAlly = false;          // true면 플레이어 편

    [Header("이동")]
    public float moveSpeed = 10.0f;
    public float turnSpeed = 50.0f;      // 차체 회전 속도 (도/초)
    public float stopDistance = 40.0f;   // 이 거리 안이면 멈춰서 사격
    public float retreatDistance = 0.0f; // 이 거리보다 가까우면 후진 (0이면 사용 안 함)
    public float followDistance = 30.0f; // 아군: 싸울 적이 없을 때 플레이어와 유지할 거리

    [Header("공격")]
    public float attackRange = 120.0f;
    public float fireInterval = 3.0f;
    public float damage = 10.0f;
    public float launchSpeed = 120.0f;   // 포탄 발사 속도 (탄도 계산에 사용)
    public float shellLifeTime = 3.0f;
    public bool highArc = false;         // 높은 탄도(곡사) 사용 여부
    public float spread = 2.0f;          // 명중 오차 (도)
    public float turretTurnSpeed = 60.0f;
    public float minPitch = -45.0f;      // 포신 위쪽 한계 (음수가 위쪽)
    public float maxPitch = 5.0f;        // 포신 아래쪽 한계

    [Header("보스 광폭화")]
    [Range(0.0f, 1.0f)] public float enrageHpRatio = 0.5f; // 이 비율 아래로 체력이 떨어지면 광폭화
    public int enragedBurst = 3;                           // 광폭화 후 한 번에 쏘는 발 수

    [Header("참조 (비워두면 이름으로 자동 검색)")]
    public Transform turret;
    public Transform cannon;
    public Transform firePos;

    // 웨이브 매니저 등이 적 처치를 집계할 때 사용 (아군이 죽을 때는 발생하지 않음)
    public static event Action<EnemyTank> OnEnemyDied;
    public static EnemyTank CurrentBoss { get; private set; } // 보스 HP바 표시용

    public TankHealth Health => health;
    public bool IsEnraged { get; private set; }

    Transform tr;
    Transform target;        // 현재 공격 목표 (없으면 null)
    Transform player;        // 아군이 따라다닐 플레이어
    TankHealth health;
    GameObject shellPrefab;
    AudioSource sfx;
    AudioClip fireSfx;
    float fireTimer;
    float stuckTimer;
    float retargetTimer;

    // 물리 이동 (Update에서 정하고 FixedUpdate에서 적용)
    public float lookAhead = 25.0f;      // 장애물 감지 거리
    public float gravityMultiplier = 2.5f; // 큰 탱크 모델이 천천히 떨어져 보이지 않도록 탱크에만 중력 추가 (TankMove와 같음)
    Rigidbody rb;
    float desiredSpeed;                  // 이번 프레임에 원하는 전진(+)/후진(-) 속도
    Quaternion desiredRotation;          // 이번 프레임에 향하고 싶은 방향
    Vector3 lastCheckPos;

    // 아군 자리 나누기: 생성되는 순서대로 오른쪽(1), 왼쪽(-1)을 번갈아 배정
    static int allySpawnCount;
    float followSide = 1.0f;
    float blockedCheckTimer, blockedTime, escapeTimer, escapeTurn;
    bool isDead = false;

    void Awake()
    {
        tr = transform;
        health = GetComponent<TankHealth>();
        health.team = isAlly ? TankHealth.Team.Player : TankHealth.Team.Enemy;
        if (isAlly) followSide = (allySpawnCount++ % 2 == 0) ? 1.0f : -1.0f;
        sfx = GetComponent<AudioSource>();
        shellPrefab = Resources.Load<GameObject>("Cannon");
        fireSfx = Resources.Load<AudioClip>("CannonFire");

        if (turret == null) turret = FindChild(tr, "Turret");
        if (cannon == null) cannon = FindChild(tr, "Cannon");
        if (firePos == null) firePos = FindChild(tr, "FirePos");
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0.0f, -0.5f, 0.0f);
        rb.maxDepenetrationVelocity = 2.0f; // 겹쳤을 때 하늘로 튀어 오르지 않게
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠르게 부딪혀도 얇은 벽을 뚫지 않게
        desiredRotation = rb.rotation;
        lastCheckPos = tr.position;
        TankMove p = FindAnyObjectByType<TankMove>();
        if (p != null) player = p.transform;
        if (type == EnemyType.Boss) CurrentBoss = this;

        health.OnDeath += Die;
        health.OnHpChanged += CheckEnrage;
        // 등장하자마자 일제히 쏘지 않도록 첫 발사 타이밍을 흩어 놓음
        fireTimer = UnityEngine.Random.Range(fireInterval * 0.5f, fireInterval);
        FindTarget();
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnDeath -= Die;
            health.OnHpChanged -= CheckEnrage;
        }
        if (CurrentBoss == this) CurrentBoss = null;
    }

    void Update()
    {
        if (isDead) return;

        CheckStuck();

        // 이번 프레임의 이동 명령 초기화 (아래 Move/MoveToward가 정함, 없으면 제자리)
        desiredSpeed = 0.0f;
        desiredRotation = rb.rotation;

        // 1초마다 가장 가까운 상대 진영 탱크로 목표를 다시 고름
        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0.0f || target == null || IsDeadTarget(target))
        {
            FindTarget();
            retargetTimer = 1.0f;
        }

        if (target == null)
        {
            // 싸울 상대가 없을 때: 아군은 플레이어를 따라다님, 적은 대기
            // 아군끼리 같은 자리로 모이지 않도록 각자 플레이어의 오른쪽/왼쪽 뒤 자리를 따라다님
            if (isAlly && player != null)
            {
                Vector3 slot = player.position + player.right * followSide * followDistance - player.forward * 10.0f;
                MoveToward(slot, 8.0f);
            }
            return;
        }

        float dist = Vector3.Distance(tr.position, target.position);
        Move(dist);
        bool aimed = Aim(out bool inRange);

        fireTimer -= Time.deltaTime;
        if (aimed && inRange && dist <= attackRange && fireTimer <= 0.0f)
        {
            if (IsEnraged && enragedBurst > 1) StartCoroutine(Burst(enragedBurst));
            else Fire();
            fireTimer = fireInterval;
        }
    }

    // 살아 있는 상대 진영 탱크 중 가장 가까운 것을 목표로
    void FindTarget()
    {
        target = null;
        float best = float.MaxValue;
        foreach (TankHealth h in FindObjectsByType<TankHealth>(FindObjectsSortMode.None))
        {
            if (h.IsDead || h.team == health.team) continue;
            float d = (h.transform.position - tr.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                target = h.transform;
            }
        }
    }

    static bool IsDeadTarget(Transform t)
    {
        TankHealth h = t.GetComponent<TankHealth>();
        return h == null || h.IsDead;
    }

    // 보스: 체력이 기준 아래로 떨어지면 한 번만 광폭화
    void CheckEnrage(float curr, float max)
    {
        if (type != EnemyType.Boss || IsEnraged || curr > max * enrageHpRatio) return;
        IsEnraged = true;
        fireInterval *= 0.6f;
        moveSpeed *= 1.4f;
        turretTurnSpeed *= 1.5f;
        Debug.Log($"{name}: 광폭화!");
    }

    IEnumerator Burst(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Fire();
            yield return new WaitForSeconds(0.25f);
        }
    }

    // 뒤집히거나 지형 아래로 빠진 상태가 2초 넘게 지속되면 바로 세워서 땅 위에 다시 올려놓음
    void CheckStuck()
    {
        Terrain terrain = Terrain.activeTerrain;
        float groundY = terrain != null ? terrain.SampleHeight(tr.position) + terrain.transform.position.y : tr.position.y;
        bool flipped = Vector3.Dot(tr.up, Vector3.up) < 0.3f;
        bool underGround = tr.position.y < groundY - 3.0f;

        stuckTimer = (flipped || underGround) ? stuckTimer + Time.deltaTime : 0.0f;
        if (stuckTimer > 2.0f)
        {
            stuckTimer = 0.0f;
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            tr.SetPositionAndRotation(new Vector3(tr.position.x, groundY + 1.5f, tr.position.z),
                                      Quaternion.Euler(0.0f, tr.eulerAngles.y, 0.0f));
            Debug.Log($"{name}: 뒤집힘/빠짐 감지 → 위치 복구");
        }
    }

    // 차체: 목표 쪽으로 돌면서 전진, 가까우면 정지(또는 후진)
    void Move(float dist)
    {
        MoveToward(target.position, stopDistance);
        if (retreatDistance > 0.0f && dist < retreatDistance)
        {
            desiredSpeed = -moveSpeed * 0.6f;
        }
    }

    // 지점 쪽으로 차체를 돌리고, stop 거리보다 멀면 전진 (실제 이동은 FixedUpdate에서 물리로 처리)
    void MoveToward(Vector3 point, float stop)
    {
        Vector3 to = point - tr.position;
        to.y = 0.0f;
        if (to.sqrMagnitude < 0.01f) return;

        // 가는 길에 건물이 있으면 트인 쪽으로 방향을 틀어서 돌아감
        Vector3 dir = to.magnitude > stop ? AvoidObstacles(to.normalized) : to.normalized;
        // 같은 편 탱크와 너무 가까우면 서로 밀어내는 방향으로 틀어서 부딪히지 않게
        dir = AddSeparation(dir, out bool friendAhead);
        desiredRotation = Quaternion.LookRotation(dir);

        float facing = Vector3.Angle(tr.forward, dir);
        if (to.magnitude > stop && facing < 45.0f && !friendAhead)
        {
            desiredSpeed = moveSpeed;
        }
    }

    // 같은 진영 탱크(플레이어 포함)와 거리 두기
    // - 가까울수록 반대 방향으로 미는 힘을 더함
    // - 바로 앞(30° 안, 12m 안)에 있으면 전진을 멈춤
    Vector3 AddSeparation(Vector3 dir, out bool friendAhead)
    {
        const float range = 18.0f;
        friendAhead = false;
        Vector3 push = Vector3.zero;
        foreach (TankHealth h in FindObjectsByType<TankHealth>(FindObjectsSortMode.None))
        {
            if (h == health || h.IsDead || h.team != health.team) continue;
            Vector3 away = tr.position - h.transform.position;
            away.y = 0.0f;
            float d = away.magnitude;
            if (d > range || d < 0.01f) continue;
            push += away.normalized * (1.0f - d / range);
            if (d < 12.0f && Vector3.Angle(tr.forward, -away) < 30.0f) friendAhead = true;
        }
        Vector3 result = dir + push * 1.5f;
        result.y = 0.0f;
        return result.sqrMagnitude > 0.01f ? result.normalized : dir;
    }

    // 간단한 장애물 회피: 앞쪽으로 구(Sphere)를 쏴서 건물이 감지되면, 좌우로 각도를 넓혀 가며 트인 방향을 찾음
    // (지형과 탱크는 장애물로 보지 않음: 지형은 넘어가고, 탱크는 공격 대상이거나 금방 비켜 가므로)
    Vector3 AvoidObstacles(Vector3 dir)
    {
        if (IsClear(dir)) return dir;
        float[] angles = { 30.0f, -30.0f, 60.0f, -60.0f, 90.0f, -90.0f };
        foreach (float a in angles)
        {
            Vector3 d = Quaternion.Euler(0.0f, a, 0.0f) * dir;
            if (IsClear(d)) return d;
        }
        return Quaternion.Euler(0.0f, 120.0f, 0.0f) * dir; // 사방이 막혔으면 크게 돌아감
    }

    bool IsClear(Vector3 dir)
    {
        Vector3 origin = tr.position + Vector3.up * 3.0f;
        RaycastHit[] hits = Physics.SphereCastAll(origin, 4.0f, dir, lookAhead, ~(1 << 8), QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(tr)) continue;                  // 자기 자신
            if (h.collider.GetComponentInParent<TankHealth>() != null) continue; // 다른 탱크
            return false;                                                      // 건물 등 장애물
        }
        return true;
    }

    // 움직이려는데 제자리에 머물러 있으면(건물에 끼임) 잠깐 후진하면서 방향을 틀어 빠져나옴
    void CheckBlocked()
    {
        if (escapeTimer > 0.0f) return;
        blockedCheckTimer += Time.deltaTime;
        if (blockedCheckTimer < 1.0f) return;

        bool tryingToMove = Mathf.Abs(desiredSpeed) > 0.1f;
        float moved = Vector3.Distance(tr.position, lastCheckPos);
        blockedTime = (tryingToMove && moved < 1.0f) ? blockedTime + blockedCheckTimer : 0.0f;
        blockedCheckTimer = 0.0f;
        lastCheckPos = tr.position;

        if (blockedTime >= 2.0f)
        {
            blockedTime = 0.0f;
            escapeTimer = 1.5f;
            escapeTurn = UnityEngine.Random.value < 0.5f ? -1.0f : 1.0f;
        }
    }

    // 물리 이동: Translate(순간이동) 대신 Rigidbody 속도로 움직여서 건물에 막히게 함
    void FixedUpdate()
    {
        if (isDead) return;
        CheckBlocked();
        if (escapeTimer > 0.0f)
        {
            escapeTimer -= Time.fixedDeltaTime;
            desiredSpeed = -moveSpeed * 0.6f;
            desiredRotation = rb.rotation * Quaternion.Euler(0.0f, escapeTurn * 90.0f, 0.0f);
        }

        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, desiredRotation, turnSpeed * Time.fixedDeltaTime));
        Vector3 velocity = tr.forward * desiredSpeed;
        velocity.y = rb.linearVelocity.y; // 중력은 물리 엔진에 맡김
        velocity.y = Mathf.Min(velocity.y, 2.0f); // 충돌로 위로 솟구치는 속도 제한 (떨어지는 건 그대로)
        rb.linearVelocity = velocity;

        // 추가 중력 (기본 1배는 물리 엔진이 줌)
        rb.AddForce(Physics.gravity * (gravityMultiplier - 1.0f), ForceMode.Acceleration);

        // 충돌로 생긴 앞뒤·좌우로 넘어가는 회전을 매 프레임 줄임 (언덕에서 기우는 건 지형이 계속 받쳐 줘서 유지됨)
        Vector3 av = rb.angularVelocity;
        av.x *= 0.8f;
        av.z *= 0.8f;
        rb.angularVelocity = av;
    }

    // 포탑과 포신을 탄도 계산 결과에 맞춰 회전. 조준이 거의 맞으면 true
    bool Aim(out bool inRange)
    {
        Vector3 aimPoint = target.position + Vector3.up * 2.0f; // 탱크 차체 중앙 근처
        inRange = Ballistics.TryGetLaunchAngle(firePos.position, aimPoint, launchSpeed, highArc, out float angle);
        if (!inRange) angle = 45.0f; // 사거리 밖이면 일단 최대 사거리 각도로 겨눔

        Vector3 dir = Ballistics.GetLaunchDirection(firePos.position, aimPoint, angle);

        // 포탑(좌우): 포탑 부모 기준으로 목표 방향의 좌우 각도를 구함 (TurretCtrl과 같은 원리)
        Vector3 localDir = turret.parent.InverseTransformDirection(dir);
        float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        Quaternion yawRot = Quaternion.Euler(0.0f, yaw, 0.0f);
        turret.localRotation = Quaternion.RotateTowards(turret.localRotation, yawRot, turretTurnSpeed * Time.deltaTime);

        // 포신(상하): 포탑 기준 높이 각도. 차체가 기울어져 있어도 보정됨
        Vector3 turretLocal = turret.InverseTransformDirection(dir);
        float pitch = -Mathf.Atan2(turretLocal.y, new Vector2(turretLocal.x, turretLocal.z).magnitude) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cannon.localRotation = Quaternion.RotateTowards(cannon.localRotation, Quaternion.Euler(pitch, 0.0f, 0.0f), turretTurnSpeed * Time.deltaTime);

        return Vector3.Angle(firePos.forward, dir) < 3.0f;
    }

    void Fire()
    {
        if (isDead) return;
        // 명중 오차: 발사 방향을 spread 범위 안에서 무작위로 살짝 비틈
        Quaternion offset = Quaternion.Euler(UnityEngine.Random.Range(-spread, spread), UnityEngine.Random.Range(-spread, spread), 0.0f);
        GameObject obj = Instantiate(shellPrefab, firePos.position, firePos.rotation * offset);
        Cannon shell = obj.GetComponent<Cannon>();
        shell.owner = tr;
        shell.damage = damage;
        shell.launchSpeed = launchSpeed;
        shell.lifeTime = shellLifeTime;

        if (sfx != null && fireSfx != null) sfx.PlayOneShot(fireSfx, 1.0f);
    }

    void Die()
    {
        isDead = true;
        if (rb != null) rb.linearVelocity = new Vector3(0.0f, rb.linearVelocity.y, 0.0f); // 파괴된 뒤 미끄러지지 않게
        if (!isAlly) OnEnemyDied?.Invoke(this);
        if (CurrentBoss == this) CurrentBoss = null;

        // 파괴 연출: 검게 그을리게 하고 잠시 뒤 제거
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.material.color = Color.Lerp(r.material.color, Color.black, 0.7f);
        }
        Destroy(gameObject, 3.0f);
    }

    static Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        Debug.LogWarning($"{parent.name}: '{childName}' 오브젝트를 찾지 못했습니다.");
        return null;
    }
}
