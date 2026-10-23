using UnityEngine;

// 포물선 탄도 계산 (적 AI 조준, 지도 포격 공용)
public static class Ballistics
{
    // 속도 speed로 쏴서 origin -> target에 맞히기 위한 발사 각도(수평 기준, 도 단위)를 구한다.
    // 같은 지점을 맞히는 각도는 보통 두 개(낮은 탄도/높은 탄도)이며, highArc로 선택한다.
    // 사거리 밖이라 해가 없으면 false를 반환한다.
    //
    // 공식: tanθ = (v² ± √(v⁴ - g(g·x² + 2·y·v²))) / (g·x)
    //   x: 수평 거리, y: 높이 차, v: 발사 속도, g: 중력 가속도
    public static bool TryGetLaunchAngle(Vector3 origin, Vector3 target, float speed, bool highArc, out float angleDeg)
    {
        Vector3 diff = target - origin;
        float y = diff.y;
        diff.y = 0.0f;
        float x = diff.magnitude;
        float g = -Physics.gravity.y;
        float v2 = speed * speed;

        float root = v2 * v2 - g * (g * x * x + 2.0f * y * v2);
        if (root < 0.0f || x < 0.01f)
        {
            angleDeg = 0.0f;
            return false;
        }

        float sqrt = Mathf.Sqrt(root);
        float tanTheta = highArc ? (v2 + sqrt) / (g * x) : (v2 - sqrt) / (g * x);
        angleDeg = Mathf.Atan(tanTheta) * Mathf.Rad2Deg;
        return true;
    }

    // 정해진 각도(angleDeg)로 쏴서 target에 떨어지게 하는 발사 속도를 구한다 (곡사로 장애물을 넘길 때 사용)
    // 공식: v² = g·x² / (2·cos²θ·(x·tanθ − y))   x: 수평 거리, y: 높이 차
    public static bool TryGetSpeedForAngle(Vector3 origin, Vector3 target, float angleDeg, out float speed)
    {
        Vector3 diff = target - origin;
        float y = diff.y;
        diff.y = 0.0f;
        float x = diff.magnitude;
        float g = -Physics.gravity.y;
        float theta = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(theta);
        float denom = 2.0f * cos * cos * (x * Mathf.Tan(theta) - y);
        if (denom <= 0.0f || x < 0.01f)
        {
            speed = 0.0f;
            return false;   // 그 각도로는 목표 높이까지 닿지 못함
        }
        speed = Mathf.Sqrt(g * x * x / denom);
        return true;
    }

    // 포탄 궤적을 미리 따라가 보면서 목표 근처에 닿기 전에 지형·건물·나무에 막히는지 확인
    // ignoreRoot(쏘는 탱크 자신)와 탱크들은 무시, 목표 반경 clearRadius 안에서 맞는 건 "도착"으로 봄
    public static bool IsTrajectoryClear(Vector3 origin, Vector3 velocity, Vector3 target, Transform ignoreRoot, float clearRadius = 12.0f)
    {
        Vector3 g = Physics.gravity;
        Vector3 prev = origin;
        for (float t = 0.05f; t < 30.0f; t += 0.05f)
        {
            Vector3 p = origin + velocity * t + 0.5f * g * t * t;
            Vector3 seg = p - prev;
            RaycastHit[] hits = Physics.RaycastAll(prev, seg.normalized, seg.magnitude, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
            {
                if (ignoreRoot != null && h.transform.IsChildOf(ignoreRoot)) continue;
                if (h.collider.GetComponentInParent<TankHealth>() != null) continue;
                return Vector3.Distance(h.point, target) <= clearRadius; // 목표 근처에 떨어지면 성공, 그 전이면 막힘
            }
            if (p.y < target.y - 20.0f) return false; // 아무것도 안 맞고 목표보다 한참 아래로 내려감
            prev = p;
        }
        return false;
    }

    // 발사 각도가 적용된 월드 방향 벡터
    public static Vector3 GetLaunchDirection(Vector3 origin, Vector3 target, float angleDeg)
    {
        Vector3 flat = target - origin;
        flat.y = 0.0f;
        flat.Normalize();
        // 수평 방향을 기준으로, 수평축을 따라 angleDeg만큼 위로 들어 올림
        Vector3 axis = Vector3.Cross(flat, Vector3.up);
        return Quaternion.AngleAxis(angleDeg, axis) * flat;
    }
}
