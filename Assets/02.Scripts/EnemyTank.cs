using System;
using UnityEngine;

// 적 AI 탱크: 플레이어를 추적하고, 사거리 안에 들어오면 탄도를 계산해서 포격한다.
// 스펙(체력/속도/사거리/공격력 등)을 다르게 준 프리팹으로 적 종류를 나눈다.
[RequireComponent(typeof(TankHealth))]
public class EnemyTank : MonoBehaviour
{
    public enum EnemyType { Scout, Heavy, Artillery }
    public EnemyType type = EnemyType.Scout;

    [Header("이동")]
    public float moveSpeed = 10.0f;
    public float turnSpeed = 50.0f;      // 차체 회전 속도 (도/초)
    public float stopDistance = 40.0f;   // 이 거리 안이면 멈춰서 사격
    public float retreatDistance = 0.0f; // 이 거리보다 가까우면 후진 (0이면 사용 안 함)

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

    [Header("참조 (비워두면 이름으로 자동 검색)")]
    public Transform turret;
    public Transform cannon;
    public Transform firePos;

    // 웨이브 매니저 등이 적 처치를 집계할 때 사용
    public static event Action<EnemyTank> OnEnemyDied;

    Transform tr;
    Transform target;
    TankHealth health;
    GameObject shellPrefab;
    AudioSource sfx;
    AudioClip fireSfx;
    float fireTimer;
    bool isDead = false;

    void Awake()
    {
        tr = transform;
        health = GetComponent<TankHealth>();
        sfx = GetComponent<AudioSource>();
        shellPrefab = Resources.Load<GameObject>("Cannon");
        fireSfx = Resources.Load<AudioClip>("CannonFire");

        if (turret == null) turret = FindChild(tr, "Turret");
        if (cannon == null) cannon = FindChild(tr, "Cannon");
        if (firePos == null) firePos = FindChild(tr, "FirePos");
    }

    void Start()
    {
        GetComponent<Rigidbody>().centerOfMass = new Vector3(0.0f, -0.5f, 0.0f);
        // 플레이어 탱크 = TankMove가 붙은 탱크
        TankMove player = FindAnyObjectByType<TankMove>();
        if (player != null) target = player.transform;

        health.OnDeath += Die;
        // 등장하자마자 일제히 쏘지 않도록 첫 발사 타이밍을 흩어 놓음
        fireTimer = UnityEngine.Random.Range(fireInterval * 0.5f, fireInterval);
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= Die;
    }

    void Update()
    {
        if (isDead || target == null) return;

        float dist = Vector3.Distance(tr.position, target.position);
        Move(dist);
        bool aimed = Aim(out bool inRange);

        fireTimer -= Time.deltaTime;
        if (aimed && inRange && dist <= attackRange && fireTimer <= 0.0f)
        {
            Fire();
            fireTimer = fireInterval;
        }
    }

    // 차체: 플레이어 쪽으로 돌면서 전진, 가까우면 정지(또는 후진)
    void Move(float dist)
    {
        Vector3 toTarget = target.position - tr.position;
        toTarget.y = 0.0f;
        Quaternion look = Quaternion.LookRotation(toTarget);
        tr.rotation = Quaternion.RotateTowards(tr.rotation, look, turnSpeed * Time.deltaTime);

        float facing = Vector3.Angle(tr.forward, toTarget);
        if (dist > stopDistance && facing < 45.0f)
        {
            tr.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
        }
        else if (retreatDistance > 0.0f && dist < retreatDistance)
        {
            tr.Translate(Vector3.back * moveSpeed * 0.6f * Time.deltaTime);
        }
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
        OnEnemyDied?.Invoke(this);

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
