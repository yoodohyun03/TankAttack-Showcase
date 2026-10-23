using System;
using UnityEngine;

// 탱크 체력 관리 (플레이어 탱크, 적 탱크 공용)
public class TankHealth : MonoBehaviour
{
    // 진영: 플레이어와 아군 AI는 Player, 적은 Enemy. 같은 진영끼리는 포탄 데미지가 없음
    public enum Team { Player, Enemy }
    public Team team = Team.Player;

    public float maxHp = 100.0f;
    float currHp;
    bool isDead = false;

    // UI(HP바), 게임 매니저(패배 처리) 등이 구독해서 쓰는 이벤트
    public event Action<float, float> OnHpChanged; // (현재 HP, 최대 HP)
    public event Action OnDeath;

    public float CurrHp => currHp;
    public bool IsDead => isDead;

    GameObject deathEffect; // 파괴 폭발 이펙트

    void Awake()
    {
        currHp = maxHp;
        deathEffect = Resources.Load<GameObject>("TankExplosion");
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currHp = Mathf.Clamp(currHp - damage, 0.0f, maxHp);
        OnHpChanged?.Invoke(currHp, maxHp);
        Debug.Log($"{name} 피격: -{damage} (HP {currHp}/{maxHp})");

        if (currHp <= 0.0f)
        {
            Die();
        }
    }

    // 최대 HP를 바꾸고 체력을 가득 채움 (난이도 조절용)
    public void SetMaxHp(float value)
    {
        maxHp = value;
        currHp = maxHp;
        OnHpChanged?.Invoke(currHp, maxHp);
    }

    // HP 회복 아이템용
    public void Heal(float amount)
    {
        if (isDead) return;

        currHp = Mathf.Clamp(currHp + amount, 0.0f, maxHp);
        OnHpChanged?.Invoke(currHp, maxHp);
    }

    void Die()
    {
        isDead = true;
        Debug.Log($"{name} 파괴됨");

        if (deathEffect != null)
        {
            GameObject effect = Instantiate(deathEffect, transform.position + Vector3.up * 2.0f, Quaternion.identity);
            Destroy(effect, 3.0f);

        }
        OnDeath?.Invoke();
    }
}
