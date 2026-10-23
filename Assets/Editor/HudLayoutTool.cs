using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메뉴 [Tools > TankAttack > Layout Game HUD]
// 현재 열린 씬(scBattleField)의 Canvas 아래 HUD 오브젝트를 이름으로 찾아
// 위치·크기·글자·색을 일괄 배치하고, 빠진 오브젝트는 만들고, GameHUD 연결과 임시 표시 끄기까지 처리한다.
public static class HudLayoutTool
{
    static Transform canvasTr;
    static Font font;

    [MenuItem("Tools/TankAttack/Layout Game HUD")]
    public static void Layout()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("씬에 Canvas가 없습니다. scBattleField를 열고 실행하세요.");
            return;
        }
        canvasTr = canvas.transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 해상도가 바뀌어도 배치가 유지되도록
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        Undo.RecordObject(scaler, "Layout HUD");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Color white = Color.white;

        // ── 왼쪽 위: 웨이브, 남은 적 ──
        Text wave = GetText("WaveText", canvasTr, new Vector2(0, 1), new Vector2(40, -30), new Vector2(600, 70), 54, true, TextAnchor.UpperLeft, white);
        Text enemies = GetText("EnemiesText", canvasTr, new Vector2(0, 1), new Vector2(40, -105), new Vector2(500, 44), 30, false, TextAnchor.UpperLeft, new Color(1f, 0.75f, 0.7f));

        // ── 위 가운데: 상태 안내 ──
        Text status = GetText("StatusText", canvasTr, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1400, 60), 38, true, TextAnchor.UpperCenter, new Color(1f, 0.9f, 0.45f));

        // ── 왼쪽 아래: HP바 ──
        Image hpBack = GetImage("HpBarBack", canvasTr, new Vector2(0, 0), new Vector2(40, 40), new Vector2(460, 36), new Color(0, 0, 0, 0.55f), uiSprite);
        Image hpFill = GetImage("HpFill", hpBack.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.35f, 0.85f, 0.4f), uiSprite);
        Stretch(hpFill.rectTransform, 4);
        MakeFilled(hpFill);
        Text hpText = GetText("HpText", canvasTr, new Vector2(0, 0), new Vector2(40, 82), new Vector2(460, 40), 28, true, TextAnchor.LowerLeft, white);

        // ── 오른쪽 아래: 포탄, 장전바, 지도 포격 ──
        Text[] shells = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            shells[i] = GetText($"Shell{i + 1}", canvasTr, new Vector2(1, 0), new Vector2(-40 - (2 - i) * 175, 100), new Vector2(170, 40), 26, true, TextAnchor.LowerRight, white);
        }
        Image reloadBack = GetImage("ReloadBack", canvasTr, new Vector2(1, 0), new Vector2(-40, 80), new Vector2(520, 12), new Color(0, 0, 0, 0.55f), uiSprite);
        Image reload = GetImage("ReloadFill", reloadBack.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Color(1f, 0.85f, 0.4f), uiSprite);
        Stretch(reload.rectTransform, 2);
        MakeFilled(reload);
        Text strike = GetText("StrikeText", canvasTr, new Vector2(1, 0), new Vector2(-40, 30), new Vector2(520, 40), 26, true, TextAnchor.LowerRight, new Color(0.7f, 0.9f, 1f));

        // ── 가운데: 일반 조준점 ──
        GameObject crosshair = MakeCrosshair();

        // ── 정밀 조준 화면: 십자선 + 거리 ──
        GameObject scope = GetOrCreate("ScopeOverlay", canvasTr, false).gameObject;
        RectTransform scopeRt = scope.GetComponent<RectTransform>();
        Stretch(scopeRt, 0);
        Color red = new Color(1f, 0.25f, 0.2f, 0.9f);
        GetImage("ScopeLineLeft", scope.transform, new Vector2(0.5f, 0.5f), new Vector2(-260, 0), new Vector2(460, 3), red, null);
        GetImage("ScopeLineRight", scope.transform, new Vector2(0.5f, 0.5f), new Vector2(260, 0), new Vector2(460, 3), red, null);
        GetImage("ScopeLineDown", scope.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(3, 320), red, null);
        GetImage("ScopeDot", scope.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6), red, null);
        Text scopeDist = GetText("ScopeDistance", scope.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(400, 40), 28, true, TextAnchor.MiddleCenter, red);
        scopeDist.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // ── GameHUD 연결 ──
        GameHUD hud = canvas.GetComponent<GameHUD>();
        if (hud == null) hud = Undo.AddComponent<GameHUD>(canvas.gameObject);
        Undo.RecordObject(hud, "Layout HUD");
        hud.waveText = wave;
        hud.enemiesText = enemies;
        hud.statusText = status;
        hud.hpFill = hpFill;
        hud.hpText = hpText;
        hud.shellTexts = shells;
        hud.reloadFill = reload;
        hud.strikeText = strike;
        hud.crosshair = crosshair;
        hud.scopeOverlay = scope;
        hud.scopeDistanceText = scopeDist;

        // ── 기존 임시 표시 끄기 (적 위치 표시와 지도 화면은 유지) ──
        WaveManager wm = Object.FindAnyObjectByType<WaveManager>();
        if (wm != null) { Undo.RecordObject(wm, "Layout HUD"); wm.showDebugHud = false; }
        FireCannon fc = Object.FindAnyObjectByType<FireCannon>();
        if (fc != null) { Undo.RecordObject(fc, "Layout HUD"); fc.showDebugHud = false; }
        TankCamera tc = Object.FindAnyObjectByType<TankCamera>();
        if (tc != null) { Undo.RecordObject(tc, "Layout HUD"); tc.showDebugCrosshair = false; }
        MapStrike ms = Object.FindAnyObjectByType<MapStrike>();
        if (ms != null) { Undo.RecordObject(ms, "Layout HUD"); ms.showDebugStatus = false; }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("HUD 배치 완료. Ctrl+S로 씬을 저장하세요.");
    }

    // 메뉴 [Tools > TankAttack > Add Boss HP Bar]
    // 직접 다듬은 기존 HUD 배치는 건드리지 않고, 보스 HP바만 화면 위쪽에 추가하고 GameHUD에 연결
    [MenuItem("Tools/TankAttack/Add Boss HP Bar")]
    public static void AddBossBar()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        GameHUD hud = canvas != null ? canvas.GetComponent<GameHUD>() : null;
        if (hud == null)
        {
            Debug.LogError("GameHUD가 붙은 Canvas가 없습니다. scBattleField를 열고 실행하세요.");
            return;
        }
        canvasTr = canvas.transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        RectTransform panel = GetOrCreate("BossPanel", canvasTr, false);
        Place(panel, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(820, 80));
        Text name = GetText("BossName", panel, new Vector2(0.5f, 1), Vector2.zero, new Vector2(820, 36), 30, true, TextAnchor.UpperCenter, new Color(1f, 0.5f, 0.75f));
        Image back = GetImage("BossBarBack", panel, new Vector2(0.5f, 0), Vector2.zero, new Vector2(820, 30), new Color(0, 0, 0, 0.6f), uiSprite);
        Image fill = GetImage("BossFill", back.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.9f, 0.15f, 0.45f), uiSprite);
        Stretch(fill.rectTransform, 3);
        MakeFilled(fill);

        Undo.RecordObject(hud, "Add Boss HP Bar");
        hud.bossPanel = panel.gameObject;
        hud.bossFill = fill;
        hud.bossText = name;
        panel.gameObject.SetActive(false); // 보스가 등장할 때 GameHUD가 켬

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("보스 HP바 추가 완료. Ctrl+S로 씬을 저장하세요.");
    }

    // 메뉴 [Tools > TankAttack > Add Boss Shortcut Button]
    // scIntro를 연 상태에서 실행: 오른쪽 아래에 작은 "보스전 바로가기" 버튼을 만들고 SceneLoader.StartBossFight에 연결
    [MenuItem("Tools/TankAttack/Add Boss Shortcut Button")]
    public static void AddBossShortcut()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        SceneLoader loader = Object.FindAnyObjectByType<SceneLoader>();
        if (canvas == null || loader == null)
        {
            Debug.LogError("Canvas와 SceneLoader가 있는 씬(scIntro)을 열고 실행하세요.");
            return;
        }
        canvasTr = canvas.transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // 버튼 본체 (오른쪽 아래, 작게)
        Image img = GetImage("BossShortcutBtn", canvasTr, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(260, 56), new Color(0.85f, 0.25f, 0.45f, 0.9f), uiSprite);
        img.raycastTarget = true; // 클릭을 받아야 함
        Button btn = img.GetComponent<Button>();
        if (btn == null) btn = Undo.AddComponent<Button>(img.gameObject);
        Undo.RecordObject(btn, "Add Boss Shortcut");
        btn.targetGraphic = img;

        // 기존 연결을 지우고 StartBossFight 하나만 연결
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
        }
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, loader.StartBossFight);

        // 버튼 글자
        Text label = GetText("BossShortcutLabel", img.transform, Vector2.zero, Vector2.zero, Vector2.zero, 22, true, TextAnchor.MiddleCenter, Color.white);
        Stretch(label.rectTransform, 0);
        label.text = "보스전 바로가기 ▶";

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("보스전 바로가기 버튼 추가 완료. Ctrl+S로 씬을 저장하세요.");
    }

    // 메뉴 [Tools > TankAttack > Add Music Credit]
    // scIntro를 연 상태에서 실행: 왼쪽 아래에 배경음악 출처(CC BY 4.0 표기)를 작은 글씨로 추가
    [MenuItem("Tools/TankAttack/Add Music Credit")]
    public static void AddMusicCredit()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Canvas가 있는 씬(scIntro)을 열고 실행하세요.");
            return;
        }
        canvasTr = canvas.transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Text credit = GetText("MusicCredit", canvasTr, new Vector2(0, 0), new Vector2(30, 30), new Vector2(1100, 60), 20, false, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.75f));
        credit.text = "Music: \"Clash Defiant\" Kevin MacLeod (incompetech.com)\nLicensed under Creative Commons: By Attribution 4.0 License (creativecommons.org/licenses/by/4.0/)";

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("음악 출처 표기 추가 완료. Ctrl+S로 씬을 저장하세요.");
    }

    // 메뉴 [Tools > TankAttack > Add Pause Menu]
    // scBattleField의 Canvas에 Esc 일시정지 화면(계속하기 / 처음으로 / 종료)을 만들고 PauseMenu에 연결
    // 직접 다듬은 기존 HUD 배치는 건드리지 않음
    [MenuItem("Tools/TankAttack/Add Pause Menu")]
    public static void AddPauseMenu()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null || canvas.GetComponent<GameHUD>() == null)
        {
            Debug.LogError("GameHUD가 붙은 Canvas가 있는 scBattleField를 열고 실행하세요.");
            return;
        }
        canvasTr = canvas.transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        PauseMenu menu = canvas.GetComponent<PauseMenu>();
        if (menu == null) menu = Undo.AddComponent<PauseMenu>(canvas.gameObject);

        // 화면 전체를 어둡게 덮는 패널 (맨 위에 그려지도록 Canvas의 마지막 자식으로)
        Image panel = GetImage("PausePanel", canvasTr, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.03f, 0.05f, 0.08f, 0.75f), null);
        Stretch(panel.rectTransform, 0);
        panel.raycastTarget = true; // 뒤쪽 UI가 눌리지 않도록 클릭을 막음
        panel.transform.SetAsLastSibling();

        Text title = GetText("PauseTitle", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(800, 120), 90, true, TextAnchor.MiddleCenter, new Color(0.75f, 0.9f, 1f));
        title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        title.text = "일시정지";
        Text hint = GetText("PauseHint", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(800, 50), 30, false, TextAnchor.MiddleCenter, Color.white);
        hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hint.text = "Esc를 다시 누르면 계속합니다";

        AddMenuButton("ResumeBtn", "계속하기", panel.transform, new Vector2(0, 0), uiSprite, menu.Resume);
        AddMenuButton("IntroBtn", "처음으로", panel.transform, new Vector2(0, -115), uiSprite, menu.GoToIntro);
        AddMenuButton("QuitBtn", "게임 종료", panel.transform, new Vector2(0, -230), uiSprite, menu.QuitGame);

        Undo.RecordObject(menu, "Add Pause Menu");
        menu.panel = panel.gameObject;
        panel.gameObject.SetActive(false); // 평소에는 숨김, Esc로 켜짐

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("일시정지 메뉴 추가 완료. Ctrl+S로 씬을 저장하세요.");
    }

    // 메뉴용 버튼 하나 (Intro·엔딩 씬과 같은 스타일)
    static void AddMenuButton(string name, string label, Transform parent, Vector2 pos, Sprite sprite, UnityEngine.Events.UnityAction action)
    {
        Image img = GetImage(name, parent, new Vector2(0.5f, 0.5f), pos, new Vector2(460, 90), new Color(0.92f, 0.96f, 1f, 0.95f), sprite);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.raycastTarget = true;
        Button btn = img.GetComponent<Button>();
        if (btn == null) btn = Undo.AddComponent<Button>(img.gameObject);
        Undo.RecordObject(btn, "Add Pause Menu");
        btn.targetGraphic = img;
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
        }
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);

        Text t = GetText(name + "Label", img.transform, Vector2.zero, Vector2.zero, Vector2.zero, 38, true, TextAnchor.MiddleCenter, new Color(0.1f, 0.16f, 0.24f));
        Stretch(t.rectTransform, 0);
        t.text = label;
        Outline o = t.GetComponent<Outline>();
        if (o != null) Undo.DestroyObjectImmediate(o); // 밝은 버튼 위 글자라 외곽선 불필요
    }

    // 이름으로 찾고, 없으면 새로 만듦
    static RectTransform GetOrCreate(string name, Transform parent, bool _)
    {
        Transform found = FindDeep(canvasTr, name);
        if (found == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Layout HUD");
            go.layer = 5;
            found = go.transform;
        }
        Undo.SetTransformParent(found, parent, "Layout HUD");
        found.localScale = Vector3.one;
        RectTransform rt = found.GetComponent<RectTransform>();
        if (rt == null) rt = found.gameObject.AddComponent<RectTransform>();
        return rt;
    }

    static Text GetText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, bool bold, TextAnchor align, Color color)
    {
        RectTransform rt = GetOrCreate(name, parent, false);
        Place(rt, anchor, pos, size);
        Text t = rt.GetComponent<Text>();
        if (t == null)
        {
            // 텍스트가 아닌 다른 그래픽이 붙어 있으면 제거 후 텍스트로
            Graphic g = rt.GetComponent<Graphic>();
            if (g != null) Undo.DestroyObjectImmediate(g);
            t = Undo.AddComponent<Text>(rt.gameObject);
        }
        Undo.RecordObject(t, "Layout HUD");
        if (t.font == null) t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        // 눈 배경 위에서도 잘 보이도록 검은 외곽선
        Outline outline = rt.GetComponent<Outline>();
        if (outline == null) outline = Undo.AddComponent<Outline>(rt.gameObject);
        outline.effectColor = new Color(0, 0, 0, 0.85f);
        outline.effectDistance = new Vector2(2, -2);
        if (string.IsNullOrEmpty(t.text) || t.text == "New Text") t.text = name;
        return t;
    }

    static Image GetImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        RectTransform rt = GetOrCreate(name, parent, false);
        Place(rt, anchor, pos, size);
        Image img = rt.GetComponent<Image>();
        if (img == null)
        {
            Graphic g = rt.GetComponent<Graphic>();
            if (g != null) Undo.DestroyObjectImmediate(g);
            img = Undo.AddComponent<Image>(rt.gameObject);
        }
        Undo.RecordObject(img, "Layout HUD");
        img.color = color;
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    static GameObject MakeCrosshair()
    {
        // 일반 조준점: 가운데 작은 + 모양 (텍스트로 표현)
        Text t = GetText("Crosshair", canvasTr, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60), 44, true, TextAnchor.MiddleCenter, Color.white);
        t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        t.text = "+";
        return t.gameObject;
    }

    static void MakeFilled(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1.0f;
    }

    // anchor 위치를 기준점(pivot)으로 삼아 배치
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Undo.RecordObject(rt, "Layout HUD");
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float padding)
    {
        Undo.RecordObject(rt, "Layout HUD");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
