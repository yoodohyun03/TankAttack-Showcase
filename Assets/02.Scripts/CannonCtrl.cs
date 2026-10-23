using UnityEngine;

public class CannonCtrl : MonoBehaviour
{
    Transform tr;
    public float rotSpeed = 40.0f; // 포신이 목표 각도로 움직이는 속도 (도/초)

    float currAngle = 0.0f;
    public float minAngle = -20.0f;  // 위쪽 한계 (음수가 위쪽)
    public float maxAngle = 20.0f;   // 아래쪽 한계

    public float strikeMinAngle = -75.0f; // 지도 포격(곡사) 중에만 쓰는 위쪽 한계

    TankCamera tankCam;      // 조준점과 정밀 조준 여부
    FireCannon fireCannon;   // 현재 포탄 속도 (탄도 계산용)

    // 지도 포격이 정한 실제 발사 방향(월드). 값이 있으면 조준점 대신 이 방향으로 포신을 맞춤 → 보이는 포신 = 포탄 방향
    public Vector3? ForcedDirection { get; set; }
    float lastTargetAngle;
    public bool IsAimed => Mathf.Abs(currAngle - Mathf.Clamp(lastTargetAngle, CurrentMinAngle, maxAngle)) < 1.5f; // 포신이 목표 각도에 거의 도달했는지
    float CurrentMinAngle => ForcedDirection.HasValue ? strikeMinAngle : minAngle;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tr = GetComponent<Transform>();
        tankCam = FindAnyObjectByType<TankCamera>();
        fireCannon = GetComponentInParent<FireCannon>();
    }

    // Update is called once per frame
    void Update()
    {
        if (tankCam == null) return;

        // 마우스 휠 대신, 조준점 기준으로 목표 각도를 구해서 그쪽으로 움직임
        float targetAngle = GetTargetAngle();
        lastTargetAngle = targetAngle;
        // 지도 포격 곡사는 각도가 커서 더 빨리 들어 올림
        float speed = ForcedDirection.HasValue ? rotSpeed * 2.0f : rotSpeed;
        currAngle = Mathf.MoveTowards(currAngle, targetAngle, speed * Time.deltaTime);
        currAngle = Mathf.Clamp(currAngle, CurrentMinAngle, maxAngle);
        tr.localRotation = Quaternion.Euler(currAngle, 0.0f, 0.0f);
    }

    // 일반 조준: 조준점을 직선으로 겨눔 (멀면 포탄이 떨어지므로 플레이어가 감으로 위를 겨눠야 함)
    // 정밀 조준: 조준점에 포탄이 떨어지도록 탄도 계산으로 발사 각도를 구함
    float GetTargetAngle()
    {
        Vector3 aimPoint = tankCam.AimPoint;
        Vector3 dir = (aimPoint - tr.position).normalized;

        if (ForcedDirection.HasValue)
        {
            // 지도 포격: 미리 정한 실제 발사 방향(직사 또는 곡사)을 그대로 사용
            dir = ForcedDirection.Value;
        }
        // 정밀 조준이거나 지도 포격 중이면 탄도 계산
        else if ((tankCam.IsPrecision || tankCam.AimOverride.HasValue) && fireCannon != null)
        {
            float speed = fireCannon.Current.launchSpeed;
            if (Ballistics.TryGetLaunchAngle(tr.position, aimPoint, speed, false, out float launchAngle))
            {
                dir = Ballistics.GetLaunchDirection(tr.position, aimPoint, launchAngle);
            }
            else
            {
                // 사거리 밖: 최대 사거리 각도(45도)로
                dir = Ballistics.GetLaunchDirection(tr.position, aimPoint, 45.0f);
            }
        }

        // 포탑 기준 높이 각도로 변환 (차체가 기울어져 있어도 보정)
        Vector3 local = tr.parent.InverseTransformDirection(dir);
        return -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
    }
}
