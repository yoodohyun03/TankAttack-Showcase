using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Intro·엔딩 씬에 "실제 게임 화면"을 넣는 에디터 도구
// 1) Build Intro Backdrop      : 전투 씬의 전장(지형, 기지, 조명, 안개, 눈)을 Intro로 복사 + 카메라가 천천히 한 바퀴 돎
// 2) Add Tank Previews          : 전차 선택 카드마다 실제 3D 전차가 제자리에서 도는 미리보기
// 3) Add Ending Battle Background: 승리·패배 씬 배경에 전투가 끝난 순간의 실제 화면
public static class MenuSceneBuilder
{
    const string BattleScenePath = "Assets/01.Scenes/scBattleField.unity";
    const string TankModelPath = "Assets/05.Models/Abrams_Tank/Prefab/Tank.prefab";

    // ───────────────────────── 1) Intro 배경 전장 ─────────────────────────
    [MenuItem("Tools/TankAttack/Build Intro Backdrop")]
    public static void BuildIntroBackdrop()
    {
        Scene intro = SceneManager.GetActiveScene();
        if (intro.name != "scIntro")
        {
            Debug.LogError("scIntro를 열고 실행하세요.");
            return;
        }

        Scene battle = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);

        // 조명·안개·하늘 설정은 씬마다 따로라서 전투 씬 값을 읽어 Intro에 그대로 넣음
        SceneManager.SetActiveScene(battle);
        bool fog = RenderSettings.fog; Color fogColor = RenderSettings.fogColor; FogMode fogMode = RenderSettings.fogMode;
        float fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
        Material skybox = RenderSettings.skybox;
        UnityEngine.Rendering.AmbientMode ambientMode = RenderSettings.ambientMode;
        Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
        float ambientIntensity = RenderSettings.ambientIntensity;

        SceneManager.SetActiveScene(intro);
        RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
        RenderSettings.ambientIntensity = ambientIntensity;

        // 이전에 만든 배경은 지우고 다시
        GameObject old = FindRoot(intro, "IntroBackdrop");
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject root = new GameObject("IntroBackdrop");
        SceneManager.MoveGameObjectToScene(root, intro);
        Undo.RegisterCreatedObjectUndo(root, "Build Intro Backdrop");

        // 전장 복사: 지형(나무 포함), 겨울 기지, 태양광
        Terrain terrain = Object.FindAnyObjectByType<Terrain>(); // 아직 Intro에는 없으므로 전투 씬의 지형
        CopyInto(terrain != null ? terrain.gameObject : null, root, intro);
        CopyInto(FindRoot(battle, "WinterBase"), root, intro);
        Light sun = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional && l.gameObject.scene == battle) sun = l;
        }
        CopyInto(sun != null ? sun.gameObject : null, root, intro);

        // Intro에 원래 있던 태양광은 끔 (전투 씬과 같은 조명을 쓰기 위해)
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional && l.gameObject.scene == intro && !l.transform.IsChildOf(root.transform))
            {
                Undo.RecordObject(l.gameObject, "Build Intro Backdrop");
                l.gameObject.SetActive(false);
            }
        }

        // 회전 중심: 전투 씬의 플레이어 시작 위치 (뒤쪽에 겨울 기지가 보이는 곳)
        Vector3 center = Vector3.zero;
        TankMove player = null;
        foreach (TankMove m in Object.FindObjectsByType<TankMove>(FindObjectsSortMode.None))
        {
            if (m.gameObject.scene == battle) player = m;
        }
        if (player != null) center = player.transform.position;

        // Intro 메인 카메라: 천천히 도는 카메라 + 눈 내리는 효과
        Camera cam = null;
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (c.gameObject.scene == intro && c.CompareTag("MainCamera")) cam = c;
        }
        if (cam != null)
        {
            Undo.RecordObject(cam, "Build Intro Backdrop");
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 1500.0f;
            OrbitCamera orbit = cam.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = Undo.AddComponent<OrbitCamera>(cam.gameObject);
            orbit.center = center;

            // 전투 카메라에 붙어 있던 눈 파티클을 Intro 카메라에도 복사
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (ps.gameObject.scene != battle || ps.transform.parent == null || ps.transform.parent.GetComponent<Camera>() == null) continue;
                Transform oldSnow = cam.transform.Find(ps.name);
                if (oldSnow != null) Undo.DestroyObjectImmediate(oldSnow.gameObject);
                GameObject snow = Object.Instantiate(ps.gameObject);
                snow.name = ps.name;
                SceneManager.MoveGameObjectToScene(snow, intro);
                snow.transform.SetParent(cam.transform, false);
                snow.transform.localPosition = ps.transform.localPosition;
                snow.transform.localRotation = ps.transform.localRotation;
                Undo.RegisterCreatedObjectUndo(snow, "Build Intro Backdrop");
            }
        }

        // UI 뒤 어두운 패널을 옅게 해서 배경 전장이 보이게
        FadeBackgroundPanel(intro, 0.35f);

        EditorSceneManager.CloseScene(battle, true); // 전투 씬은 읽기만 했으므로 저장하지 않고 닫음
        EditorSceneManager.MarkSceneDirty(intro);
        Debug.Log("Intro 배경 전장 완료. 실행해서 확인한 뒤 Ctrl+S로 저장하세요.");
    }

    // ───────────────────────── 2) 전차 선택 3D 미리보기 ─────────────────────────
    [MenuItem("Tools/TankAttack/Add Tank Previews")]
    public static void AddTankPreviews()
    {
        Scene intro = SceneManager.GetActiveScene();
        TankSelectMenu menu = Object.FindAnyObjectByType<TankSelectMenu>(FindObjectsInactive.Include);
        if (intro.name != "scIntro" || menu == null || menu.cards == null || menu.cards.Length < 3)
        {
            Debug.LogError("scIntro를 열고, 먼저 Add Tank Select Menu를 실행하세요.");
            return;
        }
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TankModelPath);

        GameObject oldStage = FindRoot(intro, "TankPreviewStage");
        if (oldStage != null) Undo.DestroyObjectImmediate(oldStage);
        GameObject stage = new GameObject("TankPreviewStage");
        SceneManager.MoveGameObjectToScene(stage, intro);
        Undo.RegisterCreatedObjectUndo(stage, "Add Tank Previews");
        stage.transform.position = new Vector3(5000.0f, 0.0f, 0.0f); // 전장과 메인 카메라에서 안 보이는 먼 곳

        Transform[] tanks = new Transform[3];
        Camera[] cams = new Camera[3];
        RawImage[] views = new RawImage[3];
        for (int i = 0; i < 3; i++)
        {
            // 전차 모델 (스크립트 없는 순수 모델) — 서로 화면에 겹치지 않게 100m 간격
            GameObject tank = (GameObject)PrefabUtility.InstantiatePrefab(model, intro);
            tank.name = $"PreviewTank{i + 1}";
            tank.transform.SetParent(stage.transform, false);
            tank.transform.localPosition = new Vector3(i * 100.0f, 0.0f, 0.0f);
            tank.transform.localRotation = Quaternion.Euler(0.0f, 150.0f, 0.0f);
            tanks[i] = tank.transform;

            // 전차를 비스듬히 위에서 비추는 전용 카메라
            GameObject camGo = new GameObject($"PreviewCamera{i + 1}");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = new Vector3(i * 100.0f, 9.0f, -30.0f);
            camGo.transform.LookAt(tank.transform.position + Vector3.up * 3.0f);
            Camera c = camGo.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.12f, 0.16f, 0.22f);
            c.fieldOfView = 40.0f;
            c.depth = -10;
            c.farClipPlane = 80.0f;
            camGo.AddComponent<MapCameraNoFog>(); // 미리보기에는 안개를 끄고 선명하게
            cams[i] = c;

            // 카드 안 위쪽에 미리보기 화면, 글자는 아래쪽으로
            Image card = menu.cards[i];
            Undo.RecordObject(card.rectTransform, "Add Tank Previews");
            card.rectTransform.sizeDelta = new Vector2(440.0f, 440.0f);
            card.rectTransform.anchoredPosition = new Vector2(-500.0f + i * 500.0f, 30.0f);

            Transform oldView = card.transform.Find("TankCardView");
            if (oldView != null) Undo.DestroyObjectImmediate(oldView.gameObject);
            GameObject viewGo = new GameObject("TankCardView", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(viewGo, "Add Tank Previews");
            viewGo.layer = 5;
            viewGo.transform.SetParent(card.transform, false);
            RectTransform vr = viewGo.GetComponent<RectTransform>();
            vr.anchorMin = new Vector2(0.5f, 1.0f); vr.anchorMax = new Vector2(0.5f, 1.0f); vr.pivot = new Vector2(0.5f, 1.0f);
            vr.anchoredPosition = new Vector2(0.0f, -15.0f);
            vr.sizeDelta = new Vector2(410.0f, 256.0f);
            RawImage view = viewGo.AddComponent<RawImage>();
            view.raycastTarget = false;
            views[i] = view;

            if (menu.cardTexts != null && i < menu.cardTexts.Length && menu.cardTexts[i] != null)
            {
                RectTransform tr = menu.cardTexts[i].rectTransform;
                Undo.RecordObject(tr, "Add Tank Previews");
                tr.anchorMin = new Vector2(0.0f, 0.0f); tr.anchorMax = new Vector2(1.0f, 0.0f); tr.pivot = new Vector2(0.5f, 0.0f);
                tr.anchoredPosition = new Vector2(0.0f, 10.0f);
                tr.sizeDelta = new Vector2(-20.0f, 160.0f);
                Undo.RecordObject(menu.cardTexts[i], "Add Tank Previews");
                menu.cardTexts[i].fontSize = 24;
            }
        }

        // 카드가 커진 만큼 제목·설명·버튼 위치 조정
        MoveIfExists(intro, "TankSelectTitle", new Vector2(0.0f, 400.0f));
        MoveIfExists(intro, "TankSelectHint", new Vector2(0.0f, 330.0f));
        MoveIfExists(intro, "TankSelectDesc", new Vector2(0.0f, -245.0f));
        MoveIfExists(intro, "TankBackBtn", new Vector2(-260.0f, -360.0f));
        MoveIfExists(intro, "TankConfirmBtn", new Vector2(260.0f, -360.0f));

        TankPreview preview = menu.GetComponent<TankPreview>();
        if (preview == null) preview = Undo.AddComponent<TankPreview>(menu.gameObject);
        Undo.RecordObject(preview, "Add Tank Previews");
        preview.tanks = tanks;
        preview.cameras = cams;
        preview.views = views;

        EditorSceneManager.MarkSceneDirty(intro);
        Debug.Log("전차 3D 미리보기 추가 완료. Ctrl+S로 저장하세요.");
    }

    // ───────────────────────── 3) 엔딩 배경: 마지막 전투 화면 ─────────────────────────
    [MenuItem("Tools/TankAttack/Add Ending Battle Background")]
    public static void AddEndingBackground()
    {
        Scene scene = SceneManager.GetActiveScene();
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if ((scene.name != "scWin" && scene.name != "scLose") || canvas == null)
        {
            Debug.LogError("scWin 또는 scLose를 열고 실행하세요.");
            return;
        }

        Transform old = canvas.transform.Find("EndingShotView");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject go = new GameObject("EndingShotView", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Add Ending Background");
        go.layer = 5;
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling(); // 맨 뒤 = 다른 UI보다 먼저 그려짐
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        RawImage view = go.AddComponent<RawImage>();
        view.raycastTarget = false;

        EndingShot shot = canvas.GetComponent<EndingShot>();
        if (shot == null) shot = Undo.AddComponent<EndingShot>(canvas.gameObject);
        Undo.RecordObject(shot, "Add Ending Background");
        shot.view = view;

        FadeBackgroundPanel(scene, 0.4f);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{scene.name}: 마지막 전투 화면 배경 추가 완료. Ctrl+S로 저장하세요. (전투를 끝내고 넘어왔을 때 보입니다)");
    }

    // ───────────────────────── 공용 ─────────────────────────
    static void CopyInto(GameObject original, GameObject parent, Scene scene)
    {
        if (original == null) return;
        GameObject copy = Object.Instantiate(original);
        copy.name = original.name;
        SceneManager.MoveGameObjectToScene(copy, scene);
        copy.transform.SetParent(parent.transform, true);
        copy.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == name) return go;
        }
        return null;
    }

    // 기존 UI 뒤의 어두운 "Background" 패널을 옅게
    static void FadeBackgroundPanel(Scene scene, float alpha)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.name != "Background") continue;
                Undo.RecordObject(img, "Fade Background");
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }
    }

    static void MoveIfExists(Scene scene, string name, Vector2 pos)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.name != name) continue;
                Undo.RecordObject(rt, "Move UI");
                rt.anchoredPosition = pos;
            }
        }
    }
}
