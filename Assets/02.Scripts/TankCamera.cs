using UnityEngine;

// 3인칭 마우스 시점 카메라 (월드 오브 탱크 방식) + 우클릭 정밀 조준 모드
// - 마우스를 움직이면 카메라가 탱크 주위를 회전 (탱크 차체 방향과는 독립)
// - 화면 중앙(조준점)이 가리키는 지점을 AimPoint로 계산 → 포탑(TurretCtrl)과 포신(CannonCtrl)이 사용
// - 우클릭을 누르고 있으면 포탑 위 조준경 시점으로 바뀌고 줌인 (감도도 줌에 맞춰 낮아짐)
// Main Camera에 붙이면 기존 SmoothFollow는 자동으로 꺼짐
public class TankCamera : MonoBehaviour
{
    public Transform target;              // 비워두면 플레이어 탱크(TankMove)를 자동으로 찾음
    public float pivotHeight = 6.0f;      // 탱크 기준 회전 중심 높이
    public float distance = 23.0f;        // 탱크와의 거리
    public float mouseSensitivity = 3.0f;
    public float minPitch = -10.0f;       // 위를 올려다보는 한계
    public float maxPitch = 40.0f;        // 아래를 내려다보는 한계
    public float followSmooth = 15.0f;    // 탱크 위치를 따라가는 부드러움
    public bool lockCursor = true;        // 마우스 커서 숨기기 (Esc로 해제, 클릭하면 다시 잠금)
    public bool showDebugCrosshair = true; // 임시 조준점 표시 (직접 만든 조준선 UI가 생기면 끄기)

    [Header("정밀 조준 (우클릭 유지)")]
    public float normalFov = 60.0f;
    public float precisionFov = 15.0f;    // 작을수록 많이 확대
    public float sightHeight = 5.0f;      // 포탑 위 조준경 높이
    public float zoomSpeed = 12.0f;

    public bool IsPrecision { get; private set; }
    public Vector3 AimPoint { get; private set; }   // 조준점이 가리키는 월드 좌표
    public Vector3? AimOverride { get; set; }       // 지도 포격 중에는 이 지점을 조준 (null이면 화면 중앙)
    public float AimDistance { get; private set; }  // 탱크에서 조준 지점까지 거리

    float yaw;
    float pitch = 12.0f;
    Vector3 pivot;
    Transform tr;
    Transform turret;
    Camera cam;

    void Awake()
    {
        tr = transform;
        cam = GetComponent<Camera>();
        UnityStandardAssets.Utility.SmoothFollow old = GetComponent<UnityStandardAssets.Utility.SmoothFollow>();
        if (old != null) old.enabled = false;
    }

    void Start()
    {
        if (target == null)
        {
            TankMove player = FindAnyObjectByType<TankMove>();
            if (player != null) target = player.transform;
        }
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            pivot = target.position + Vector3.up * pivotHeight;
            foreach (Transform t in target.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Turret") turret = t;
            }
        }
        SetCursorLock(lockCursor);
    }

    void Update()
    {
        // Esc로 커서 해제, 게임 화면 클릭하면 다시 잠금
        // 지도가 열려 있는 동안에는 클릭이 지도 조작용이므로 커서를 잠그지 않음
        if (MapStrike.IsMapOpen || MapStrike.ClosedFrame == Time.frameCount) return;
        // 일시정지 메뉴가 Esc와 클릭을 처리하는 동안에는 카메라가 커서를 건드리지 않음
        if (PauseState.IsPaused || PauseState.ChangedFrame == Time.frameCount) return;
        if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLock(false);
        else if (lockCursor && Input.GetMouseButtonDown(0)) SetCursorLock(true);
    }

    void LateUpdate()
    {
        if (target == null) return;

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        IsPrecision = locked && Input.GetMouseButton(1);

        if (locked)
        {
            // 줌인했을 때는 확대 비율만큼 감도를 낮춰 정밀하게
            float sens = mouseSensitivity * (cam.fieldOfView / normalFov);
            yaw += Input.GetAxis("Mouse X") * sens;
            pitch -= Input.GetAxis("Mouse Y") * sens;
            pitch = Mathf.Clamp(pitch, IsPrecision ? -30.0f : minPitch, maxPitch);
        }

        // 회전 중심은 탱크 위치를 부드럽게 따라감
        Vector3 desiredPivot = target.position + Vector3.up * pivotHeight;
        pivot = Vector3.Lerp(pivot, desiredPivot, followSmooth * Time.deltaTime);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0.0f);
        Vector3 camPos;
        if (IsPrecision && turret != null)
        {
            // 정밀 조준: 포탑 위 조준경 위치에서 바라봄
            camPos = turret.position + Vector3.up * sightHeight;
        }
        else
        {
            camPos = pivot - rot * Vector3.forward * distance;
            // 카메라가 언덕 속으로 들어가지 않도록 지형(8번 레이어)에 막히면 앞으로 당김
            Vector3 dir = camPos - pivot;
            if (Physics.Raycast(pivot, dir.normalized, out RaycastHit hit, dir.magnitude, 1 << 8))
            {
                camPos = hit.point - dir.normalized * 1.0f;
            }
        }
        tr.SetPositionAndRotation(camPos, rot);

        // 줌 (시야각을 부드럽게 변경)
        float fov = IsPrecision ? precisionFov : normalFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, zoomSpeed * Time.deltaTime);

        UpdateAimPoint();
    }

    // 화면 중앙에서 Ray를 쏴서 조준 지점을 구함. 내 탱크와 포탄(Trigger)은 무시
    void UpdateAimPoint()
    {
        if (AimOverride.HasValue)
        {
            AimPoint = AimOverride.Value;
            AimDistance = Vector3.Distance(target.position, AimPoint);
            return;
        }

        Ray ray = new Ray(tr.position, tr.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, 3000.0f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        Vector3 point = ray.GetPoint(1000.0f); // 하늘을 보고 있으면 먼 지점
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(target)) continue;
            if (h.distance < nearest)
            {
                nearest = h.distance;
                point = h.point;
            }
        }
        AimPoint = point;
        AimDistance = Vector3.Distance(target.position, point);
    }

    public void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // 임시 조준점 (직접 만든 UI가 생기면 지워도 됨)
    void OnGUI()
    {
        if (!showDebugCrosshair || Cursor.lockState != CursorLockMode.Locked) return;
        float cx = Screen.width / 2.0f;
        float cy = Screen.height / 2.0f;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = IsPrecision ? new Color(1.0f, 0.3f, 0.3f) : Color.white;

        if (IsPrecision)
        {
            // 정밀 조준: 십자선 + 거리 표시
            GUI.Label(new Rect(cx - 200, cy - 20, 160, 40), "─────", style);
            GUI.Label(new Rect(cx + 40, cy - 20, 160, 40), "─────", style);
            GUI.Label(new Rect(cx - 20, cy - 20, 40, 40), "+", style);
            style.fontSize = 18;
            GUI.Label(new Rect(cx - 100, cy + 25, 200, 30), $"{AimDistance:0}m  [탄도 보정]", style);
        }
        else
        {
            GUI.Label(new Rect(cx - 20, cy - 20, 40, 40), "+", style);
        }
    }
}
