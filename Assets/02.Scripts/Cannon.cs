using System.Collections;
using UnityEngine;

public class Cannon : MonoBehaviour
{
    // 발사 속도(m/s). 예전 AddForce(6000) 방식과 같은 속도(6000 × 0.02s ÷ 질량 1 = 120m/s)
    // 속도를 직접 지정해야 탄도 계산(Ballistics)이 정확하게 맞는다.
    public float launchSpeed = 120.0f;
    public float damage = 20.0f;
    public float splashRadius = 0.0f; // 0보다 크면 범위 피해 (고폭탄)
    public float lifeTime = 3.0f; // 아무것도 안 맞았을 때 사라지는 시간 (높은 탄도는 길게)
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
        _rigidbody.linearVelocity = transform.forward * launchSpeed;
        StartCoroutine(this.ExplosionCannon(lifeTime));
    }
    private void OnTriggerEnter(Collider other)
    {
        if (isExploded) return;
        // 발사한 탱크 자신이나 다른 포탄과는 충돌 무시
        if (owner != null && other.transform.IsChildOf(owner)) return;
        if (other.GetComponentInParent<Cannon>() != null) return;

        isExploded = true; // 같은 프레임에 여러 콜라이더에 닿아도 데미지는 한 번만
        if (splashRadius > 0.0f)
        {
            ApplySplashDamage(); // 고폭탄: 폭발 지점 주변 전체에 피해
        }
        else
        {
            TankHealth health = other.GetComponentInParent<TankHealth>();
            if (health != null && !IsFriendlyFire(health))
            {
                health.TakeDamage(damage);
            }
        }

        StopAllCoroutines(); // Start에서 시작한 타이머 취소
        StartCoroutine(this.ExplosionCannon(0.0f));
    }

    // 범위 피해: 반경 안의 탱크마다 거리에 따라 피해 감소 (폭심 100% → 가장자리 30%)
    void ApplySplashDamage()
    {
        System.Collections.Generic.HashSet<TankHealth> damaged = new System.Collections.Generic.HashSet<TankHealth>();
        foreach (Collider col in Physics.OverlapSphere(transform.position, splashRadius))
        {
            TankHealth health = col.GetComponentInParent<TankHealth>();
            // 한 탱크에 콜라이더가 여러 개여도 한 번만, 자기 자신과 아군은 제외
            if (health == null || !damaged.Add(health)) continue;
            if (owner != null && health.transform.IsChildOf(owner)) continue;
            if (IsFriendlyFire(health)) continue;

            float dist = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
            float falloff = Mathf.Lerp(1.0f, 0.3f, dist / splashRadius);
            health.TakeDamage(damage * falloff);
        }
    }

    // 적끼리 쏜 포탄은 서로 데미지를 주지 않음
    bool IsFriendlyFire(TankHealth target)
    {
        if (owner == null) return false;
        bool ownerIsEnemy = owner.GetComponent<EnemyTank>() != null;
        bool targetIsEnemy = target.GetComponent<EnemyTank>() != null;
        return ownerIsEnemy && targetIsEnemy;
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
