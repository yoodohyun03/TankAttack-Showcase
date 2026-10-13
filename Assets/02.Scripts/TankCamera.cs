using UnityEngine;

// 3인칭 마우스 시점 카메라 (월드 오브 탱크 방식)
// - 마우스를 움직이면 카메라가 탱크 주위를 회전 (탱크 차체 방향과는 독립)
// - 포탑은 TurretCtrl이 화면 중앙(조준점)을 따라 회전
// - 마우스 휠은 그대로 포신 상하 조절(CannonCtrl)
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

    float yaw;
    float pitch = 12.0f;
    Vector3 pivot;
    Transform tr;

    void Awake()
    {
        tr = transform;
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
        }
        SetCursorLock(lockCursor);
    }

    void Update()
    {
        // Esc로 커서 해제, 게임 화면 클릭하면 다시 잠금
        if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLock(false);
        else if (lockCursor && Input.GetMouseButtonDown(0)) SetCursorLock(true);
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // 회전 중심은 탱크 위치를 부드럽게 따라감
        Vector3 desiredPivot = target.position + Vector3.up * pivotHeight;
        pivot = Vector3.Lerp(pivot, desiredPivot, followSmooth * Time.deltaTime);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0.0f);
        Vector3 camPos = pivot - rot * Vector3.forward * distance;

        // 카메라가 언덕 속으로 들어가지 않도록 지형(8번 레이어)에 막히면 앞으로 당김
        Vector3 dir = camPos - pivot;
        if (Physics.Raycast(pivot, dir.normalized, out RaycastHit hit, dir.magnitude, 1 << 8))
        {
            camPos = hit.point - dir.normalized * 1.0f;
        }

        tr.SetPositionAndRotation(camPos, rot);
    }

    public void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // 임시 조준점 (직접 만든 UI가 생기면 지워도 됨)
    void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(Screen.width / 2 - 20, Screen.height / 2 - 20, 40, 40), "+", style);
    }
}
