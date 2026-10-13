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
