using System;
using UnityEngine;

public class FireCannon : MonoBehaviour
{
    // 포탄 종류별 성능 (Inspector에서 수정 가능)
    [Serializable]
    public class ShellType
    {
        public string name;
        public float damage;          // 직격 데미지 (범위 피해 포탄은 폭심 데미지)
        public float splashRadius;    // 0보다 크면 범위 피해
        public float launchSpeed;     // 발사 속도 (m/s)
        public float reloadTime;      // 재장전 시간 (초)
        public Color trailColor = Color.white;
        public float trailWidth = 1.0f;
    }

    public ShellType[] shellTypes =
    {
        new ShellType { name = "일반탄", damage = 20, splashRadius = 0,  launchSpeed = 120, reloadTime = 0.8f, trailColor = new Color(1.0f, 0.9f, 0.6f), trailWidth = 1.0f },
        new ShellType { name = "고폭탄", damage = 25, splashRadius = 12, launchSpeed = 100, reloadTime = 1.6f, trailColor = new Color(1.0f, 0.45f, 0.1f), trailWidth = 1.6f },
        new ShellType { name = "철갑탄", damage = 50, splashRadius = 0,  launchSpeed = 170, reloadTime = 2.5f, trailColor = new Color(0.4f, 0.9f, 1.0f), trailWidth = 0.6f },
    };

    public GameObject cannon = null;
    public Transform firePos;
    public float damageMultiplier = 1.0f; // 공격력 아이템용
    public float normalSpread = 1.5f;     // 일반 조준 명중 오차 (도)
    public float precisionSpread = 0.2f;  // 정밀 조준 명중 오차 (도)
    AudioClip fireSfx = null;
    AudioSource sfx = null;
    TankCamera tankCam;

    public int CurrentIndex { get; private set; }
    public ShellType Current => shellTypes[CurrentIndex];
    public float ReloadRemaining { get; private set; } // 0이면 발사 가능

    // UI가 구독해서 쓰는 이벤트
    public event Action<ShellType> OnShellChanged;

    private void Awake()
    {
        cannon = (GameObject)Resources.Load("Cannon");
        fireSfx = Resources.Load<AudioClip>("CannonFire");
        sfx = GetComponent<AudioSource>();
    }

    void Start()
    {
        tankCam = FindAnyObjectByType<TankCamera>();
    }

    // Update is called once per frame
    void Update()
    {
        ReloadRemaining = Mathf.Max(0.0f, ReloadRemaining - Time.deltaTime);

        // 숫자키 1, 2, 3으로 포탄 교체
        for (int i = 0; i < shellTypes.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SelectShell(i);
        }

        // 커서가 풀려 있을 때(Esc)의 클릭은 커서를 다시 잠그는 용도라 발사하지 않음
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked && ReloadRemaining <= 0.0f && !MapStrike.IsBusy)
        {
            Fire();
        }
    }

    public void SelectShell(int index)
    {
        if (index == CurrentIndex) return;
        CurrentIndex = index;
        // 포탄을 바꾸면 새 포탄을 장전해야 함
        ReloadRemaining = Current.reloadTime;
        OnShellChanged?.Invoke(Current);
    }

    void Fire()
    {
        // 명중 오차: 일반 조준은 크게, 정밀 조준은 작게
        float spread = (tankCam != null && tankCam.IsPrecision) ? precisionSpread : normalSpread;
        Quaternion offset = Quaternion.Euler(UnityEngine.Random.Range(-spread, spread), UnityEngine.Random.Range(-spread, spread), 0.0f);
        SpawnShell(firePos.rotation * offset, 3.0f);
        ReloadRemaining = Current.reloadTime;
    }

    // 지도 포격용: 목표 지점에 떨어지도록 탄도를 계산해서 바로 발사 (재장전과 무관)
    // 사거리 밖이면 false
    public bool FireAt(Vector3 target)
    {
        float speed = Current.launchSpeed;
        if (!Ballistics.TryGetLaunchAngle(firePos.position, target, speed, false, out float angle)) return false;
        Vector3 dir = Ballistics.GetLaunchDirection(firePos.position, target, angle);
        SpawnShell(Quaternion.LookRotation(dir), 10.0f);
        return true;
    }

    // 현재 포탄 종류로 포탄 하나를 생성
    void SpawnShell(Quaternion rotation, float lifeTime)
    {
        ShellType type = Current;
        sfx.PlayOneShot(fireSfx, 1.0f);
        GameObject obj = Instantiate(cannon, firePos.position, rotation);

        Cannon shell = obj.GetComponent<Cannon>();
        shell.owner = transform.root; // 자기 포탄에 맞지 않도록 발사한 탱크를 기록
        shell.damage = type.damage * damageMultiplier;
        shell.splashRadius = type.splashRadius;
        shell.launchSpeed = type.launchSpeed;
        shell.lifeTime = lifeTime;

        // 포탄 종류별로 궤적 색과 굵기를 다르게
        TrailRenderer trail = obj.GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.startColor = type.trailColor;
            trail.endColor = new Color(type.trailColor.r, type.trailColor.g, type.trailColor.b, 0.0f);
            trail.widthMultiplier *= type.trailWidth;
        }
    }

    // 임시 화면 표시 (직접 만든 UI가 생기면 지워도 됨)
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        float y = Screen.height - 50;
        for (int i = 0; i < shellTypes.Length; i++)
        {
            bool selected = i == CurrentIndex;
            style.normal.textColor = selected ? shellTypes[i].trailColor : new Color(1, 1, 1, 0.5f);
            string label = $"[{i + 1}] {shellTypes[i].name}";
            GUI.Label(new Rect(20 + i * 150, y, 150, 30), selected ? $"▶{label}" : label, style);
        }
        style.normal.textColor = Color.white;
        string reload = ReloadRemaining > 0.0f ? $"장전 중 {ReloadRemaining:0.0}s" : "발사 가능";
        GUI.Label(new Rect(20, y - 32, 300, 30), reload, style);
    }
}
