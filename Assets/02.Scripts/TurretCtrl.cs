using UnityEngine;

public class TurretCtrl : MonoBehaviour
{
    Transform tr;
    public float rotSpeed = 5.0f;
    TankCamera tankCam; // 조준점(AimPoint)을 계산해 주는 카메라

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tr = GetComponent<Transform>();
        tankCam = FindAnyObjectByType<TankCamera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (tankCam == null) return;
        // 화면 중앙 조준점이 가리키는 지점 (TankCamera가 Raycast로 계산)
        Vector3 aimPoint = tankCam.AimPoint;

        //Raycast: 월드공간 기준을 현재 tr(터렛)을 중심으로 한 지역 좌표계(Local)로 변환
        //터렛 입장에서 목표물이 어디에 있는지(방향) 확인이 가능함
        Vector3 relative = tr.InverseTransformPoint(aimPoint);
        //터렛의 정면(Z축)을 기준으로 목표지점(X, Z좌표)까지의 각도를 구함,
        //계산된 라디안 값을 Mathf.Rad2Deg를 곱해 Degree(일반적인 각도)로 변환
        float angle = Mathf.Atan2(relative.x, relative.z) * Mathf.Rad2Deg;
        //구한 각도(angle)에 시간 오차를 보정하여 Y축을 부드럽게 회전
        tr.Rotate(0, angle * Time.deltaTime * rotSpeed, 0);
    }
}
