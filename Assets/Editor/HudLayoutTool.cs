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
