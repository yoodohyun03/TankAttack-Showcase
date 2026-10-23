using UnityEditor;
using UnityEngine;

// 메뉴 [Tools > TankAttack > Decorate Item Prefabs]
// Resources/Items의 아이템 프리팹 3종을 종류별 모양으로 바꾸고, 같은 색의 Point Light를 붙인다.
// - HP: 십자가(+), 속도: 캡슐, 공격력: 구
// - 루트의 트리거 콜라이더, Item 스크립트(둥실둥실·회전), 직접 만든 머티리얼은 그대로 사용
// - 여러 번 실행해도 이전에 만든 모양과 조명을 지우고 다시 만듦
public static class ItemDecorator
{
    const string ItemDir = "Assets/Resources/Items";

    [MenuItem("Tools/TankAttack/Decorate Item Prefabs")]
    public static void Decorate()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ItemDir });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Item item = root.GetComponent<Item>();
            if (item != null)
            {
                DecorateOne(root, item.type);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"아이템 프리팹 {count}개 꾸미기 완료");
    }

    static void DecorateOne(GameObject root, Item.ItemType type)
    {
        // 직접 만든 머티리얼을 가져옴 (루트 렌더러 또는 이전에 만든 모양에서)
        Material mat = null;
        MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
        if (rootRenderer != null) mat = rootRenderer.sharedMaterial;
        Transform oldVisual = root.transform.Find("Visual");
        if (mat == null && oldVisual != null)
        {
            MeshRenderer r = oldVisual.GetComponentInChildren<MeshRenderer>();
            if (r != null) mat = r.sharedMaterial;
        }

        // 루트의 큐브 모양은 숨김 (콜라이더와 스크립트는 루트에 그대로 둠)
        if (rootRenderer != null) Object.DestroyImmediate(rootRenderer);
        MeshFilter rootFilter = root.GetComponent<MeshFilter>();
        if (rootFilter != null) Object.DestroyImmediate(rootFilter);

        // 이전에 만든 모양과 조명 제거 후 다시 생성
        if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
        Transform oldLight = root.transform.Find("Glow");
        if (oldLight != null) Object.DestroyImmediate(oldLight.gameObject);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        switch (type)
        {
            case Item.ItemType.Heal:
                // 십자가: 가로 막대 + 세로 막대
                AddPart(visual, PrimitiveType.Cube, Vector3.zero, new Vector3(1.0f, 0.32f, 0.32f), mat);
                AddPart(visual, PrimitiveType.Cube, Vector3.zero, new Vector3(0.32f, 1.0f, 0.32f), mat);
                break;
            case Item.ItemType.Speed:
                // 캡슐을 비스듬히 눕혀서 속도감
                GameObject cap = AddPart(visual, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.45f, 0.45f, 0.45f), mat);
                cap.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 60.0f);
                break;
            case Item.ItemType.Attack:
                AddPart(visual, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.8f, 0.8f, 0.8f), mat);
                break;
        }

        // 같은 색의 Point Light: 주변 눈을 아이템 색으로 물들여 멀리서도 보이게
        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(root.transform, false);
        Light light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = GetItemColor(mat);
        light.range = 15.0f;   // 빛의 범위는 부모 Scale과 상관없이 월드 단위
        light.intensity = 2.0f;
        light.shadows = LightShadows.None;
    }

    static GameObject AddPart(GameObject parent, PrimitiveType shape, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = shape.ToString();
        // 기본 도형에 붙는 콜라이더는 제거 (아이템 판정은 루트의 트리거 콜라이더가 담당)
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        MeshRenderer r = part.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part;
    }

    // 조명 색: 머티리얼의 Emission 색(없으면 기본 색)을 밝기만 1로 맞춰 사용
    static Color GetItemColor(Material mat)
    {
        if (mat == null) return Color.white;
        Color c = mat.IsKeywordEnabled("_EMISSION") && mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : mat.color;
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max > 0.0f) c /= max;
        c.a = 1.0f;
        return c;
    }
}
