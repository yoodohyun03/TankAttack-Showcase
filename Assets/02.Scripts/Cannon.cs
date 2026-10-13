using System.Collections;
using UnityEngine;

public class Cannon : MonoBehaviour
{
    public float speed = 6000.0f;
    public float damage = 20.0f;
    public GameObject expEffect;
    [HideInInspector] public Transform owner; // 발사한 탱크 (자기 포탄에 맞지 않도록)
    private CapsuleCollider _collider;
    Rigidbody _rigidbody;
    bool isExploded = false; // 폭발이 두 번 일어나지 않도록

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _collider = GetComponent<CapsuleCollider>();
        _rigidbody = GetComponent<Rigidbody>();
        GetComponent<Rigidbody>().AddForce(transform.forward * speed);
        StartCoroutine(this.ExplosionCannon(3.0f));
    }
    private void OnTriggerEnter(Collider other)
    {
        if (isExploded) return;
        // 발사한 탱크 자신이나 다른 포탄과는 충돌 무시
        if (owner != null && other.transform.IsChildOf(owner)) return;
        if (other.GetComponentInParent<Cannon>() != null) return;

        isExploded = true; // 같은 프레임에 여러 콜라이더에 닿아도 데미지는 한 번만
        TankHealth health = other.GetComponentInParent<TankHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }

        StopAllCoroutines(); // Start에서 시작한 3초 타이머 취소
        StartCoroutine(this.ExplosionCannon(0.0f));
    }
    IEnumerator ExplosionCannon(float tm)
    {

        yield return new WaitForSeconds(tm);
        isExploded = true;
        _collider.enabled = false; // 충돌이 안되도록
        _rigidbody.isKinematic = true;  //물리엔진의 영향을 받지 않음
        //폭발 프리팹 생성
        GameObject obj = (GameObject)Instantiate(expEffect, transform.position, Quaternion.identity);
        Destroy(obj, 1.0f);//폭발효과 1초뒤 사라짐,
        Destroy(this.gameObject, 1.0f); //Trail Renderer가 사라질 때 까지 기다림
    }
    // Update is called once per frame
    void Update()
    {

    }
}
