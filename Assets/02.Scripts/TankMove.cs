using UnityEngine;

public class TankMove : MonoBehaviour
{
    public float moveSpeed = 20.0f;
    public float rotSpeed = 50.0f;
    // 탱크 모델이 실제보다 커서(길이 약 17m) 보통 중력으로는 천천히 떨어지는 것처럼 보임 → 탱크에만 중력을 더 줌
    // (전체 중력을 바꾸면 포탄 탄도·사거리가 달라지므로 Physics.gravity는 그대로 둠)
    public float gravityMultiplier = 2.5f;
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
        velocity.y = Mathf.Min(rbody.linearVelocity.y, 2.0f); // 중력은 물리 엔진에 맡기되, 충돌로 솟구치는 속도는 제한
        rbody.linearVelocity = velocity;

        // 추가 중력: 기본 중력 1배는 물리 엔진이 주므로 나머지(배율 - 1)만큼 더함 (질량과 상관없는 가속도로)
        rbody.AddForce(Physics.gravity * (gravityMultiplier - 1.0f), ForceMode.Acceleration);

        // 충돌로 생긴 앞뒤·좌우로 넘어가는 회전을 줄여서 뒤집히지 않게
        Vector3 av = rbody.angularVelocity;
        av.x *= 0.8f;
        av.z *= 0.8f;
        rbody.angularVelocity = av;
    }
}
