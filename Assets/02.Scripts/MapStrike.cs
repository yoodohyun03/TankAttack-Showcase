using System.Collections;
using UnityEngine;

// 지도 포격 (나만의 기능 2)
// M키로 전술 지도를 열고 목표 지점을 클릭한 뒤 [포격]을 누르면,
// 포탑이 목표 쪽으로 돌고 탄도를 자동 계산해서 여러 발을 연달아 쏜다. 쿨타임이 있다.
// 플레이어 탱크(FireCannon이 붙은 오브젝트)에 붙여서 사용
[RequireComponent(typeof(FireCannon))]
public class MapStrike : MonoBehaviour
{
    [Header("포격")]
    public int salvoCount = 4;          // 연발 수
    public float salvoInterval = 0.4f;  // 연발 간격 (초)
    public float scatterRadius = 8.0f;  // 목표 주변 흩어짐 반경 (m)
    public float maxRange = 450.0f;     // 최대 사거리 (m)
    public float cooldown = 25.0f;      // 재사용 대기 (초)
    public float aimTime = 1.2f;        // 포탑이 목표 쪽으로 도는 시간

    [Header("지도")]
    public KeyCode mapKey = KeyCode.M;
    public Rect mapViewport = new Rect(0.2f, 0.14f, 0.6f, 0.78f); // 화면에서 지도가 차지하는 영역 (0~1)
    public float minMapSize = 60.0f;   // 최대로 확대했을 때 지도 세로 절반 크기 (m)
    public float zoomSpeed = 2.0f;     // 휠 한 칸당 확대 정도
    public float dragThreshold = 8.0f; // 이 픽셀 이상 움직이면 클릭이 아니라 끌기로 판단

    // 다른 스크립트가 확인하는 상태 (지도 열림 / 포격 진행 중)
    public static bool IsMapOpen { get; private set; }
    public static bool IsBusy { get; private set; }
    public static int ClosedFrame { get; private set; } = -1; // 지도를 닫은 프레임 (같은 프레임의 Esc를 카메라가 다시 처리하지 않도록)

    Camera mapCam;
    Vector3 mapCenter;      // 맵 전체가 보이는 기본 카메라 위치
    float fullMapSize;      // 맵 전체가 보이는 orthographicSize (가장 많이 축소한 상태)
    bool pressing;          // 지도 위에서 왼쪽 버튼을 누르고 있는 중
    bool dragging;          // 누른 채로 움직여서 지도를 끌고 있는 중
    Vector3 pressPos;       // 누르기 시작한 화면 위치
    Vector3 lastMousePos;   // 직전 프레임 마우스 위치
    Terrain terrain;
    FireCannon fire;
    TankCamera tankCam;
    TankHealth health;
    Vector3? selected;      // 지도에서 고른 목표 지점
    float cooldownLeft;
    public float CooldownLeft => cooldownLeft;     // UI 표시용
    public bool showDebugStatus = true;            // 오른쪽 아래 임시 상태 표시 (직접 만든 UI가 생기면 끄기)
    string message = "";
    float messageUntil;

    void Awake()
    {
        // 씬을 다시 불러와도 이전 상태가 남지 않도록 초기화 (static 변수)
        IsMapOpen = false;
        IsBusy = false;
    }

    void Start()
    {
        fire = GetComponent<FireCannon>();
        health = GetComponent<TankHealth>();
        tankCam = FindAnyObjectByType<TankCamera>();
        terrain = Terrain.activeTerrain;
        CreateMapCamera();
    }

    void OnDestroy()
    {
        IsMapOpen = false;
        IsBusy = false;
    }

    // 맵 전체를 위에서 내려다보는 직교(Orthographic) 카메라를 코드로 생성
    void CreateMapCamera()
    {
        GameObject go = new GameObject("MapCamera");
        mapCam = go.AddComponent<Camera>();
        mapCam.orthographic = true;
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        mapCam.rect = mapViewport;
        mapCam.depth = 10;              // 메인 카메라 위에 그림
        mapCam.enabled = false;
        go.AddComponent<MapCameraNoFog>(); // 위에서 멀리 보면 안개 때문에 하얗게 보이므로 지도에서만 안개 끔

        Vector3 center = transform.position;
        float height = 500.0f;
        if (terrain != null)
        {
            Vector3 size = terrain.terrainData.size;
            center = terrain.transform.position + new Vector3(size.x * 0.5f, 0.0f, size.z * 0.5f);
            height = size.y + 300.0f;
        }
        mapCenter = center + Vector3.up * height;
        go.transform.SetPositionAndRotation(mapCenter, Quaternion.Euler(90.0f, 0.0f, 0.0f));
        mapCam.farClipPlane = height + 500.0f;
    }

    // 마우스 휠로 확대·축소. 마우스가 가리키는 지점이 화면에서 그대로 유지되도록 카메라 위치를 보정
    void ZoomMap(float scroll)
    {
        Vector3 before = mapCam.ScreenToWorldPoint(Input.mousePosition);
        mapCam.orthographicSize = Mathf.Clamp(mapCam.orthographicSize * (1.0f - scroll * zoomSpeed), minMapSize, fullMapSize);
        Vector3 after = mapCam.ScreenToWorldPoint(Input.mousePosition);

        Vector3 pos = mapCam.transform.position + (before - after);
        mapCam.transform.position = ClampToTerrain(pos);
    }

    // 확대한 상태에서 지도가 맵 밖을 비추지 않도록 카메라 위치 제한
    Vector3 ClampToTerrain(Vector3 pos)
    {
        if (terrain == null) return pos;
        Vector3 tMin = terrain.transform.position;
        Vector3 tMax = tMin + terrain.terrainData.size;
        float halfH = mapCam.orthographicSize;
        float halfW = halfH * mapCam.aspect;
        pos.x = halfW * 2.0f < tMax.x - tMin.x ? Mathf.Clamp(pos.x, tMin.x + halfW, tMax.x - halfW) : mapCenter.x;
        pos.z = halfH * 2.0f < tMax.z - tMin.z ? Mathf.Clamp(pos.z, tMin.z + halfH, tMax.z - halfH) : mapCenter.z;
        return pos;
    }

    void Update()
    {
        cooldownLeft = Mathf.Max(0.0f, cooldownLeft - Time.deltaTime);
        if (health != null && health.IsDead)
        {
            if (IsMapOpen) SetMapOpen(false);
            return;
        }

        if (Input.GetKeyDown(mapKey) && !IsBusy) SetMapOpen(!IsMapOpen);
        if (!IsMapOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape)) SetMapOpen(false);

        // 지도 위에서 마우스 휠: 확대·축소
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0.0f && mapCam.pixelRect.Contains(Input.mousePosition)) ZoomMap(scroll);

        // 왼쪽 버튼: 짧게 클릭하면 목표 지정, 누른 채 끌면 지도 이동 (배틀그라운드 지도 방식)
        Vector3 mouse = Input.mousePosition;
        if (Input.GetMouseButtonDown(0) && mapCam.pixelRect.Contains(mouse))
        {
            pressing = true;
            dragging = false;
            pressPos = mouse;
            lastMousePos = mouse;
        }
        if (pressing && Input.GetMouseButton(0))
        {
            // 일정 거리 이상 움직이면 클릭이 아니라 끌기로 판단
            if (!dragging && Vector3.Distance(mouse, pressPos) > dragThreshold) dragging = true;
            if (dragging) PanMap(lastMousePos, mouse);
            lastMousePos = mouse;
        }
        if (pressing && Input.GetMouseButtonUp(0))
        {
            pressing = false;
            if (!dragging) SelectTarget(mouse);
        }
    }

    // 지도 클릭 위치에서 지도 카메라 기준으로 Ray를 쏴서 지형(8번 레이어) 위치를 목표로 저장
    void SelectTarget(Vector3 screenPos)
    {
        Ray ray = mapCam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, 1 << 8))
        {
            selected = hit.point;
        }
    }

    // 마우스를 끈 만큼 지도를 반대로 이동 → 지도를 손으로 잡아 끄는 느낌
    void PanMap(Vector3 fromScreen, Vector3 toScreen)
    {
        Vector3 from = mapCam.ScreenToWorldPoint(fromScreen);
        Vector3 to = mapCam.ScreenToWorldPoint(toScreen);
        mapCam.transform.position = ClampToTerrain(mapCam.transform.position + (from - to));
    }

    void SetMapOpen(bool open)
    {
        IsMapOpen = open;
        if (!open) ClosedFrame = Time.frameCount;
        mapCam.enabled = open;
        selected = null;
        pressing = false;
        dragging = false;
        if (tankCam != null) tankCam.SetCursorLock(!open); // 지도에서는 커서가 보여야 클릭 가능

        if (open && terrain != null)
        {
            // 지도 영역의 가로세로 비율에 맞춰 맵 전체가 들어오도록 확대 정도 계산
            Vector3 size = terrain.terrainData.size;
            float aspect = mapCam.pixelRect.width / mapCam.pixelRect.height;
            fullMapSize = Mathf.Max(size.z * 0.5f, size.x * 0.5f / aspect);
            // 열 때마다 맵 전체가 보이는 상태로 초기화
            mapCam.orthographicSize = fullMapSize;
            mapCam.transform.position = mapCenter;
        }
    }

    bool InRange(Vector3 target)
    {
        return Vector3.Distance(transform.position, target) <= maxRange;
    }

    IEnumerator Strike(Vector3 target)
    {
        IsBusy = true;
        SetMapOpen(false);
        ShowMessage($"지도 포격 개시! ({salvoCount}발)");

        // 포탑과 포신이 목표 쪽을 향하도록 조준점을 목표로 고정 (CannonCtrl이 탄도 계산으로 포신 각도 조절)
        tankCam.AimOverride = target;
        yield return new WaitForSeconds(aimTime);

        for (int i = 0; i < salvoCount; i++)
        {
            // 목표 주변으로 살짝 흩어지게 → 일정 범위를 덮는 연속 포격
            Vector2 r = Random.insideUnitCircle * scatterRadius;
            Vector3 p = target + new Vector3(r.x, 0.0f, r.y);
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;

            if (!fire.FireAt(p))
            {
                ShowMessage("사거리 밖입니다");
                break;
            }
            yield return new WaitForSeconds(salvoInterval);
        }

        tankCam.AimOverride = null;
        IsBusy = false;
        cooldownLeft = cooldown;
    }

    void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.time + 2.5f;
    }

    // 임시 UI (직접 만든 UI가 생기면 교체)
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;

        if (Time.time < messageUntil)
        {
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 40), message, style);
        }

        if (!IsMapOpen)
        {
            if (!showDebugStatus) return;
            // 평소: 오른쪽 아래에 지도 포격 상태 표시
            style.alignment = TextAnchor.MiddleRight;
            string state = IsBusy ? "포격 중..." : cooldownLeft > 0.0f ? $"재사용 대기 {cooldownLeft:0}s" : "준비 완료";
            GUI.Label(new Rect(Screen.width - 420, Screen.height - 50, 400, 30), $"[{mapKey}] 지도 포격: {state}", style);
            return;
        }

        Rect px = mapCam.pixelRect;
        float top = Screen.height - px.yMax; // GUI 좌표는 위가 0
        GUI.Label(new Rect(px.x, top - 40, px.width, 36), "전술 지도  -  목표 지점을 클릭하고 [포격]을 누르세요  (M / Esc: 닫기)", style);

        // 내 탱크와 적 위치 표시
        DrawMarker(transform.position, "▲\n나", new Color(0.4f, 0.8f, 1.0f));
        foreach (EnemyTank enemy in FindObjectsByType<EnemyTank>(FindObjectsSortMode.None))
        {
            DrawMarker(enemy.transform.position, "◆", new Color(1.0f, 0.3f, 0.25f));
        }

        // 아래쪽: 목표 정보와 포격 버튼
        float by = Screen.height - px.y + 10;
        string info;
        bool canFire = false;
        if (!selected.HasValue)
        {
            info = "목표 미지정";
        }
        else
        {
            DrawMarker(selected.Value, "✕", Color.yellow);
            float dist = Vector3.Distance(transform.position, selected.Value);
            bool inRange = InRange(selected.Value);
            info = inRange ? $"목표까지 {dist:0}m" : $"사거리 밖 ({dist:0}m / 최대 {maxRange:0}m)";
            canFire = inRange && cooldownLeft <= 0.0f;
        }
        if (cooldownLeft > 0.0f) info += $"   |   재사용 대기 {cooldownLeft:0}s";

        style.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(px.x, by, px.width - 240, 50), info, style);

        GUI.enabled = canFire;
        GUIStyle btn = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        if (GUI.Button(new Rect(px.xMax - 220, by, 220, 50), $"포격 ({salvoCount}발)", btn))
        {
            StartCoroutine(Strike(selected.Value));
        }
        GUI.enabled = true;
    }

    void DrawMarker(Vector3 world, string label, Color color)
    {
        Vector3 sp = mapCam.WorldToScreenPoint(world);
        if (!mapCam.pixelRect.Contains(sp)) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = color;
        GUI.Label(new Rect(sp.x - 30, Screen.height - sp.y - 20, 60, 50), label, style);
    }
}

// 지도 카메라가 그릴 때만 안개를 끄고, 다 그리면 원래대로 돌려놓음
public class MapCameraNoFog : MonoBehaviour
{
    bool fogWas;
    void OnPreRender() { fogWas = RenderSettings.fog; RenderSettings.fog = false; }
    void OnPostRender() { RenderSettings.fog = fogWas; }
}
