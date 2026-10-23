using UnityEngine;

public class TankMove : MonoBehaviour
{
    public float moveSpeed = 20.0f;
    public float rotSpeed = 50.0f;
    Rigidbody rbody;
    Transform tr;
    float h, v;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rbody = GetComponent<Rigidbody>();
        tr = GetComponent<Transform>();
        //Rigidbody의 무게중심을 낮게 설정
        rbody.centerOfMass = new Vector3(0.0f, -0.5f, 0.0f);
        // 건물 등에 겹쳤을 때 물리 엔진이 밀어내는 속도의 상한 (하늘로 튀어 오르는 현상 방지)
        rbody.maxDepenetrationVelocity = 2.0f;
        rbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rbody.interpolation = RigidbodyInterpolation.Interpolate; // 물리 이동을 화면에서 부드럽게
    }

    // Update is called once per frame
    void Update()
    {
        // 입력은 매 프레임 읽고, 실제 이동은 물리 업데이트(FixedUpdate)에서 처리
        h = Input.GetAxis("Horizontal"); //방향키 좌우(-1 - +1 값 리턴)
        v = Input.GetAxis("Vertical");
    }

    // Translate(순간이동)로 움직이면 물리 충돌 검사를 건너뛰어 건물을 뚫고 지나가므로,
    // Rigidbody의 속도와 회전으로 움직여서 물리 엔진이 충돌을 처리하게 함
    void FixedUpdate()
    {
        Quaternion turn = Quaternion.Euler(0.0f, rotSpeed * h * Time.fixedDeltaTime, 0.0f);
        rbody.MoveRotation(rbody.rotation * turn);

        Vector3 velocity = tr.forward * v * moveSpeed;
        velocity.y = rbody.linearVelocity.y; // 위아래(중력) 속도는 물리 엔진에 맡김
        rbody.linearVelocity = velocity;
    }
}
