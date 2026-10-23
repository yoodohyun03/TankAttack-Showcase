using System.IO;
using UnityEditor;
using UnityEngine;

// 메뉴 [Tools > TankAttack > Build Enemy Prefabs]를 누르면
// 플레이어와 같은 Abrams 탱크 모델로 적 프리팹 3종을 자동 생성한다.
// 결과: Assets/Resources/Enemies/Enemy_Scout, Enemy_Heavy, Enemy_Artillery, Enemy_Boss
//       Assets/Resources/Allies/Ally_Tank
public static class EnemyPrefabBuilder
{
    const string SourcePrefab = "Assets/05.Models/Abrams_Tank/Prefab/Tank.prefab";
    const string OutDir = "Assets/Resources/Enemies";
    const string MatDir = "Assets/Resources/Enemies/Materials";
    const string AllyDir = "Assets/Resources/Allies";

    struct Spec
    {
        public string name;
        public EnemyTank.EnemyType type;
        public Color tint;
        public float scale;
        public float hp;
        public float moveSpeed, turnSpeed, stopDistance, retreatDistance;
        public float attackRange, fireInterval, damage, launchSpeed, shellLifeTime, spread;
        public bool highArc;
        public float minPitch;
        public bool isAlly;       // 아군 AI
    }

    [MenuItem("Tools/TankAttack/Build Enemy Prefabs")]
    public static void Build()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (source == null)
        {
            Debug.LogError($"원본 탱크 프리팹을 찾을 수 없습니다: {SourcePrefab}");
            return;
        }
        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(MatDir);

        Spec[] specs =
        {
            // 정찰형: 작고 빠르고 약함. 가까이 붙어서 자주 쏨
            new Spec { name = "Enemy_Scout", type = EnemyTank.EnemyType.Scout, tint = new Color(0.95f, 0.85f, 0.45f), scale = 0.8f,
                hp = 60, moveSpeed = 16, turnSpeed = 90, stopDistance = 35, retreatDistance = 0,
                attackRange = 110, fireInterval = 3.0f, damage = 5, launchSpeed = 120, shellLifeTime = 3, spread = 4.0f, highArc = false, minPitch = -30 },
            // 중전차: 크고 느리고 단단함. 한 방이 셈
            new Spec { name = "Enemy_Heavy", type = EnemyTank.EnemyType.Heavy, tint = new Color(0.55f, 0.25f, 0.22f), scale = 1.25f,
                hp = 250, moveSpeed = 6, turnSpeed = 30, stopDistance = 30, retreatDistance = 0,
                attackRange = 90, fireInterval = 5.0f, damage = 15, launchSpeed = 120, shellLifeTime = 3, spread = 2.5f, highArc = false, minPitch = -30 },
            // 자주포: 멀리서 곡사로 포격. 가까이 오면 후진
            new Spec { name = "Enemy_Artillery", type = EnemyTank.EnemyType.Artillery, tint = new Color(0.4f, 0.5f, 0.65f), scale = 1.0f,
                hp = 90, moveSpeed = 8, turnSpeed = 45, stopDistance = 160, retreatDistance = 100,
                attackRange = 260, fireInterval = 8.0f, damage = 20, launchSpeed = 55, shellLifeTime = 15, spread = 4.5f, highArc = true, minPitch = -75 },
            // 보스: 매우 크고 단단함, 체력이 절반 아래면 광폭화 (EnemyTank.CheckEnrage)
            new Spec { name = "Enemy_Boss", type = EnemyTank.EnemyType.Boss, tint = new Color(0.25f, 0.25f, 0.28f), scale = 2.0f,
                hp = 1500, moveSpeed = 5, turnSpeed = 25, stopDistance = 60, retreatDistance = 0,
                attackRange = 160, fireInterval = 4.0f, damage = 30, launchSpeed = 120, shellLifeTime = 3, spread = 2.0f, highArc = false, minPitch = -35 },
            // 아군: 파란색, 적을 공격하고 적이 없으면 플레이어를 따라다님
            new Spec { name = "Ally_Tank", type = EnemyTank.EnemyType.Ally, tint = new Color(0.35f, 0.6f, 1.0f), scale = 0.9f,
                hp = 180, moveSpeed = 12, turnSpeed = 60, stopDistance = 45, retreatDistance = 0,
                attackRange = 120, fireInterval = 3.0f, damage = 12, launchSpeed = 120, shellLifeTime = 3, spread = 3.0f, highArc = false, minPitch = -30, isAlly = true },
        };

        foreach (Spec spec in specs)
        {
            BuildOne(source, spec);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"AI 탱크 프리팹 {specs.Length}종 생성 완료 (적·보스: {OutDir}, 아군: {AllyDir})");
    }

    static void BuildOne(GameObject source, Spec spec)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = spec.name;
        go.transform.localScale = Vector3.one * spec.scale;

        // 물리: 플레이어 탱크와 같은 설정
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = 2000.0f;
        rb.angularDamping = 0.05f;

        // 충돌: 플레이어 탱크 BodyMesh의 BoxCollider와 같은 크기
        Transform body = Find(go.transform, "BodyMesh");
        BoxCollider box = body.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0.008f, 2.215f, -0.334f);
        box.size = new Vector3(7.57f, 3.71f, 17.34f);

        // 포구 위치
        Transform cannon = Find(go.transform, "Cannon");
        GameObject firePos = new GameObject("FirePos");
        firePos.transform.SetParent(cannon, false);
        firePos.transform.localPosition = new Vector3(0.0f, 0.0f, 9.13f);

        // 사운드
        AudioSource audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1.0f;
        audio.minDistance = 50.0f;
        audio.maxDistance = 200.0f;

        // 체력
        TankHealth health = go.AddComponent<TankHealth>();
        health.maxHp = spec.hp;
        health.team = spec.isAlly ? TankHealth.Team.Player : TankHealth.Team.Enemy;

        // AI 스펙
        EnemyTank ai = go.AddComponent<EnemyTank>();
        ai.type = spec.type;
        ai.isAlly = spec.isAlly;
        ai.moveSpeed = spec.moveSpeed;
        ai.turnSpeed = spec.turnSpeed;
        ai.stopDistance = spec.stopDistance;
        ai.retreatDistance = spec.retreatDistance;
        ai.attackRange = spec.attackRange;
        ai.fireInterval = spec.fireInterval;
        ai.damage = spec.damage;
        ai.launchSpeed = spec.launchSpeed;
        ai.shellLifeTime = spec.shellLifeTime;
        ai.spread = spec.spread;
        ai.highArc = spec.highArc;
        ai.minPitch = spec.minPitch; // 곡사(자주포)는 포신을 높이 들어야 함
        ai.turret = Find(go.transform, "Turret");
        ai.cannon = cannon;
        ai.firePos = firePos.transform;

        // 외형: 원본 머티리얼을 복사해서 색만 입힘 (원본 플레이어 탱크 색은 그대로)
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string path = $"{MatDir}/{spec.name}_{Sanitize(mats[i].name)}.mat";
                Material tinted = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (tinted == null)
                {
                    tinted = new Material(mats[i]);
                    AssetDatabase.CreateAsset(tinted, path);
                }
                else
                {
                    tinted.CopyPropertiesFromMaterial(mats[i]);
                }
                tinted.color = spec.tint;
                EditorUtility.SetDirty(tinted);
                mats[i] = tinted;
            }
            r.sharedMaterials = mats;
        }

        // 아군은 Resources/Allies, 적과 보스는 Resources/Enemies
        string outDir = spec.isAlly ? AllyDir : OutDir;
        Directory.CreateDirectory(outDir);
        PrefabUtility.SaveAsPrefabAsset(go, $"{outDir}/{spec.name}.prefab");
        Object.DestroyImmediate(go);
    }

    static Transform Find(Transform root, string childName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        throw new System.Exception($"'{childName}' 오브젝트를 찾지 못했습니다.");
    }

    static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace('#', '_').Replace(' ', '_');
    }
}
