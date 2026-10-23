using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴 [Tools > TankAttack > Place Winter Base]
// Military Base Pack(Tiny Teacup Studio) 건물을 설원 분위기에 맞게 흰색 계열로 바꿔서 배치한다.
// 1) 팔레트 텍스처를 흰색 쪽으로 섞은 겨울용 복사본과 머티리얼 생성 (원본 팩은 그대로)
// 2) 플레이어 시작 지점 "뒤쪽"에 전선 기지(격납고, 차고, 감시탑, 컨테이너, 드럼통, 가로등)
// 3) 맵 네 모서리 근처에 감시탑
// 전투가 벌어지는 앞쪽 평지는 비워 둠 (적 AI가 장애물을 피하지 못하기 때문)
public static class WinterBaseBuilder
{
    const string PackDir = "Assets/Tiny Teacup Studio/Military Base Pack";
    const string OutDir = "Assets/04.Images/Materials";
    const string RootName = "WinterBase";

    const float WhiteBlend = 0.6f;                            // 원래 색과 흰색을 섞는 비율 (0~1, 클수록 하얗게)
    static readonly Color SnowTint = new Color(0.93f, 0.96f, 1.0f); // 살짝 푸른 눈 색

    [MenuItem("Tools/TankAttack/Place Winter Base")]
    public static void Place()
    {
        TankMove player = Object.FindAnyObjectByType<TankMove>();
        Terrain terrain = Terrain.activeTerrain;
        if (player == null || terrain == null)
        {
            Debug.LogError("scBattleField를 열고 실행하세요. (플레이어 탱크와 지형이 필요)");
            return;
        }

        Material winterMat = MakeWinterMaterial();
        if (winterMat == null) return;

        // 이전에 만든 기지가 있으면 지우고 다시 배치
        GameObject old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Place Winter Base");

        Transform p = player.transform;
        Vector3 fwd = Vector3.ProjectOnPlane(p.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        // (뒤쪽 거리, 옆 거리, 회전 추가, 프리팹 이름) — 플레이어 기준 뒤쪽에 반원 모양 기지
        var camp = new (float back, float side, float yaw, string prefab)[]
        {
            (95,    0, 180, "Buildings/Hangar1"),
            (80,  -70, 150, "Buildings/Garage1"),
            (80,   70, 210, "Buildings/Hangar2"),
            (45,  -55,   0, "Buildings/Tower1"),
            (45,   55,   0, "Buildings/Tower1"),
            (55,  -30,  90, "Props/Container1"),
            (60,  -30,  90, "Props/Container2"),
            (55,   32,  80, "Props/Container3"),
            (40,   28,   0, "Props/Barrel1"),
            (41,   31,   0, "Props/Barrel2"),
            (38,   30,   0, "Props/Barrel3"),
            (42,  -26,   0, "Props/Barrel4"),
            (35,  -15,   0, "Props/Lamp1"),
            (35,   15,   0, "Props/Lamp1"),
        };
        foreach (var c in camp)
        {
            Vector3 pos = p.position - fwd * c.back + right * c.side;
            // 기지 건물은 플레이어 시작 지점(전방)을 바라보게
            Quaternion rot = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, c.yaw, 0);
            Spawn(c.prefab, pos, rot, root.transform, terrain, winterMat);
        }

        // 맵 네 모서리 근처 감시탑 (적 소환 제외 구역 안쪽 = 가장자리 25m)
        Vector3 tMin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        const float inset = 25.0f;
        Vector3[] corners =
        {
            new Vector3(tMin.x + inset,          0, tMin.z + inset),
            new Vector3(tMin.x + size.x - inset, 0, tMin.z + inset),
            new Vector3(tMin.x + inset,          0, tMin.z + size.z - inset),
            new Vector3(tMin.x + size.x - inset, 0, tMin.z + size.z - inset),
        };
        Vector3 center = tMin + new Vector3(size.x * 0.5f, 0, size.z * 0.5f);
        foreach (Vector3 c in corners)
        {
            Vector3 toCenter = center - c;
            toCenter.y = 0;
            Spawn("Buildings/Tower1", c, Quaternion.LookRotation(toCenter), root.transform, terrain, winterMat);
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"겨울 기지 배치 완료: {root.transform.childCount}개. 위치는 직접 옮겨도 됩니다. Ctrl+S로 저장하세요.");
    }

    // 메뉴 [Tools > TankAttack > Add Solid Colliders to Winter Base]
    // 팩의 구조물은 모델 모양 그대로의 얇은 면(Mesh Collider)이라 비스듬히·빠르게 부딪히면 뚫리거나,
    // 열린 격납고·감시탑 다리 사이처럼 빈 곳이 있음 → 모델 크기에 맞는 꽉 찬 Box Collider를 하나 더 씌움
    [MenuItem("Tools/TankAttack/Add Solid Colliders to Winter Base")]
    public static void AddSolidColliders()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogError("씬에 WinterBase가 없습니다. scBattleField를 열고 실행하세요.");
            return;
        }

        int count = 0;
        foreach (Transform building in root.transform)
        {
            // 이전에 추가한 상자는 지우고 다시 계산
            Transform old = building.Find("SolidCollider");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            // 건물 기준(로컬) 좌표로 모든 메쉬를 감싸는 상자 계산 → 건물이 회전해 있어도 딱 맞음
            bool has = false;
            Bounds local = new Bounds();
            foreach (MeshFilter mf in building.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = building.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!has) { local = new Bounds(p, Vector3.zero); has = true; }
                    else local.Encapsulate(p);
                }
            }
            if (!has) continue;

            GameObject solid = new GameObject("SolidCollider");
            Undo.RegisterCreatedObjectUndo(solid, "Add Solid Colliders");
            solid.transform.SetParent(building, false);
            BoxCollider box = solid.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = local.size;
            count++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"구조물 {count}개에 꽉 찬 상자 콜라이더를 추가했습니다. Ctrl+S로 저장하세요.");
    }

    // 메뉴 [Tools > TankAttack > Flatten Ground Under Winter Base]
    // WinterBase 아래 건물마다 바닥 넓이만큼 지형을 평평하게 다지고, 가장자리는 주변 지형과 부드럽게 이어지게 한 뒤
    // 건물을 그 높이에 정확히 올려놓는다. (땅에 묻히거나 떠 보이는 문제 해결)
    [MenuItem("Tools/TankAttack/Flatten Ground Under Winter Base")]
    public static void FlattenGround()
    {
        Terrain terrain = Terrain.activeTerrain;
        GameObject root = GameObject.Find(RootName);
        if (terrain == null || root == null)
        {
            Debug.LogError("scBattleField에 지형과 WinterBase가 있어야 합니다. 먼저 Place Winter Base를 실행하세요.");
            return;
        }

        TerrainData data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Flatten Ground");

        const float margin = 2.0f;   // 건물 바닥보다 조금 더 넓게 다짐 (m)
        const float falloff = 8.0f;  // 다진 곳에서 원래 지형으로 이어지는 경사 구간 (m)

        int count = 0;
        foreach (Transform building in root.transform)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            // 기준 높이: 건물 중심 아래의 지형 높이
            float groundY = terrain.SampleHeight(building.position) + terrain.transform.position.y;
            FlattenArea(terrain, b, groundY, margin, falloff);

            Undo.RecordObject(building, "Flatten Ground");
            Vector3 pos = building.position;
            pos.y = groundY;
            building.position = pos;
            count++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"건물 {count}개 밑 지형을 평평하게 다졌습니다. Ctrl+S로 저장하세요. (되돌리기: Ctrl+Z)");
    }

    // 직사각형 바닥 영역(b의 XZ + margin)을 높이 targetY로 맞추고, 바깥 falloff 구간은 원래 높이와 부드럽게 섞음
    static void FlattenArea(Terrain terrain, Bounds b, float targetY, float margin, float falloff)
    {
        TerrainData data = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        Vector3 size = data.size;
        int res = data.heightmapResolution;

        float minX = b.min.x - margin, maxX = b.max.x + margin;
        float minZ = b.min.z - margin, maxZ = b.max.z + margin;

        // 월드 좌표 → 높이맵 인덱스
        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - falloff - tPos.x) / size.x * (res - 1)), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX + falloff - tPos.x) / size.x * (res - 1)), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - falloff - tPos.z) / size.z * (res - 1)), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ + falloff - tPos.z) / size.z * (res - 1)), 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        if (w <= 0 || h <= 0) return;

        float[,] heights = data.GetHeights(x0, z0, w, h); // [z, x], 값은 0~1 (지형 최대 높이 기준)
        float target01 = Mathf.Clamp01((targetY - tPos.y) / size.y);

        for (int iz = 0; iz < h; iz++)
        {
            for (int ix = 0; ix < w; ix++)
            {
                float wx = tPos.x + (x0 + ix) / (float)(res - 1) * size.x;
                float wz = tPos.z + (z0 + iz) / (float)(res - 1) * size.z;
                // 바닥 영역 밖으로 얼마나 떨어져 있는지 (안쪽이면 0)
                float dx = Mathf.Max(minX - wx, 0, wx - maxX);
                float dz = Mathf.Max(minZ - wz, 0, wz - maxZ);
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(dist / falloff)); // 0: 완전히 평평, 1: 원래 지형
                heights[iz, ix] = Mathf.Lerp(target01, heights[iz, ix], t);
            }
        }
        data.SetHeights(x0, z0, heights);
    }

    static void Spawn(string prefabName, Vector3 pos, Quaternion rot, Transform parent, Terrain terrain, Material winterMat)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackDir}/Prefabs/{prefabName}.prefab");
        if (prefab == null)
        {
            Debug.LogWarning($"프리팹 없음: {prefabName}");
            return;
        }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        go.transform.SetPositionAndRotation(pos, rot);

        // 원본 팔레트 머티리얼(Default)만 겨울용으로 교체, 가로등 불빛(Light) 머티리얼은 그대로
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name == "Default") mats[i] = winterMat;
            }
            r.sharedMaterials = mats;
        }
        Undo.RegisterCreatedObjectUndo(go, "Place Winter Base");
    }

    // 팔레트 텍스처를 흰색 쪽으로 섞은 복사본과, 그것을 쓰는 머티리얼을 만든다 (이미 있으면 재사용)
    static Material MakeWinterMaterial()
    {
        string matPath = $"{OutDir}/MilitaryWinter.mat";
        string texPath = $"{OutDir}/MilitaryWinter.png";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null) return existing;

        string srcTexPath = $"{PackDir}/Materials/Texture.png";
        Material srcMat = AssetDatabase.LoadAssetAtPath<Material>($"{PackDir}/Materials/Default.mat");
        if (!File.Exists(srcTexPath) || srcMat == null)
        {
            Debug.LogError("Military Base Pack의 Texture.png 또는 Default.mat을 찾지 못했습니다.");
            return null;
        }

        // 원본 PNG를 읽어서 픽셀마다 흰색과 섞음
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(srcTexPath));
        Color[] px = tex.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            Color c = Color.Lerp(px[i], SnowTint, WhiteBlend);
            c.a = px[i].a;
            px[i] = c;
        }
        tex.SetPixels(px);
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);

        // 팔레트 텍스처는 색이 번지지 않도록 원본과 같은 가져오기 설정 사용
        TextureImporter src = (TextureImporter)AssetImporter.GetAtPath(srcTexPath);
        TextureImporter dst = (TextureImporter)AssetImporter.GetAtPath(texPath);
        dst.filterMode = src.filterMode;
        dst.mipmapEnabled = src.mipmapEnabled;
        dst.wrapMode = src.wrapMode;
        dst.textureCompression = src.textureCompression;
        dst.SaveAndReimport();

        Material mat = new Material(srcMat);
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
