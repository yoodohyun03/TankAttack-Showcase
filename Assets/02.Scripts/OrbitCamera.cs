using UnityEngine;

// Intro 배경: 전장의 한 지점을 중심으로 카메라가 천천히 원을 그리며 돎
public class OrbitCamera : MonoBehaviour
{
    public Vector3 center;            // 회전 중심 (전장 가운데)
    public float radius = 180.0f;     // 중심에서의 거리
    public float height = 70.0f;      // 중심보다 높이
    public float degreesPerSecond = 4.0f;
    public float lookHeight = 10.0f;  // 중심에서 이만큼 위를 바라봄

    float angle;

    void Update()
    {
        angle += degreesPerSecond * Time.unscaledDeltaTime;
        Vector3 offset = Quaternion.Euler(0.0f, angle, 0.0f) * new Vector3(0.0f, height, -radius);
        transform.position = center + offset;
        transform.LookAt(center + Vector3.up * lookHeight);
    }
}
