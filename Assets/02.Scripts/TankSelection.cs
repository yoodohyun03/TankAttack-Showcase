using UnityEngine;

// 플레이어 전차 종류: Intro의 전차 선택 화면에서 고르고, 전투 씬에서 능력치와 색을 적용
// static이라 씬이 바뀌어도 선택이 유지됨 (다시하기, 보스전 바로가기에도 그대로 적용)
public static class TankSelection
{
    public enum TankType { Standard, Light, Heavy }

    public struct Stats
    {
        public string name;
        public string description;
        public float maxHp;
        public float moveSpeed;
        public float rotSpeed;
        public float damageScale;   // 포탄 공격력 배율
        public float reloadScale;   // 재장전 시간 배율 (작을수록 빠름)
        public Color tint;          // 차체 색 배율 (흰색이면 원래 색, 1보다 크면 원래 무늬를 살린 채 밝아짐)
        public float scale;         // 전차 크기 배율 (실루엣으로도 구분되게)
    }

    public static TankType Selected = TankType.Standard;

    public static Stats Get(TankType type)
    {
        switch (type)
        {
            case TankType.Light:
                return new Stats
                {
                    name = "경전차",
                    description = "빠른 기동과 빠른 장전으로 치고 빠지는 전차\n대신 체력과 공격력이 낮음",
                    maxHp = 200, moveSpeed = 30, rotSpeed = 75, damageScale = 0.8f, reloadScale = 0.7f,
                    tint = new Color(1.8f, 1.85f, 1.95f), // 설원 위장 흰색 (무늬는 살리고 밝게)
                    scale = 0.85f,                        // 작고 날렵
                };
            case TankType.Heavy:
                return new Stats
                {
                    name = "중전차",
                    description = "두꺼운 장갑과 강한 포로 버티며 밀고 나가는 전차\n대신 느리고 장전이 오래 걸림",
                    maxHp = 450, moveSpeed = 13, rotSpeed = 35, damageScale = 1.3f, reloadScale = 1.3f,
                    tint = new Color(0.42f, 0.48f, 0.3f), // 짙은 국방색
                    scale = 1.2f,                         // 크고 묵직
                };
            default:
                return new Stats
                {
                    name = "표준 전차",
                    description = "속도, 체력, 화력이 고르게 균형 잡힌 전차\n처음 플레이할 때 추천",
                    maxHp = 300, moveSpeed = 20, rotSpeed = 50, damageScale = 1.0f, reloadScale = 1.0f,
                    tint = Color.white,                   // 원래 숲 위장색
                    scale = 1.0f,
                };
        }
    }

    public static Stats Current => Get(Selected);

    // 전차 종류별 외형(색, 크기) 적용: 실제 게임의 플레이어 전차와 전차 선택 화면의 3D 미리보기가 똑같이 사용
    public static void ApplyLook(Transform tank, TankType type)
    {
        Stats s = Get(type);
        tank.localScale = Vector3.one * s.scale;
        if (s.tint == Color.white) return;
        foreach (Renderer r in tank.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.materials) // materials: 이 전차만의 복사본이라 원본 머티리얼은 그대로
            {
                if (m.HasProperty("_Color")) m.color = m.color * s.tint;
            }
        }
    }

    // 전투 씬에서 플레이어 전차에 선택한 능력치와 색을 적용 (WaveManager가 시작할 때 호출)
    public static void ApplyTo(GameObject player)
    {
        Stats s = Current;
        TankMove move = player.GetComponent<TankMove>();
        if (move != null)
        {
            move.moveSpeed = s.moveSpeed;
            move.rotSpeed = s.rotSpeed;
        }
        TankHealth health = player.GetComponent<TankHealth>();
        if (health != null) health.SetMaxHp(s.maxHp);
        FireCannon fire = player.GetComponent<FireCannon>();
        if (fire != null)
        {
            fire.tankDamageScale = s.damageScale;
            fire.tankReloadScale = s.reloadScale;
        }
        ApplyLook(player.transform, Selected);
        Debug.Log($"플레이어 전차: {s.name} (HP {s.maxHp}, 속도 {s.moveSpeed}, 공격 {s.damageScale}배, 장전 {s.reloadScale}배)");
    }
}
