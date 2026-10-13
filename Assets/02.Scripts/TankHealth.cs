using System;
using UnityEngine;

// 탱크 체력 관리 (플레이어 탱크, 적 탱크 공용)
public class TankHealth : MonoBehaviour
{
    public float maxHp = 100.0f;
    float currHp;
    bool isDead = false;

    // UI(HP바), 게임 매니저(패배 처리) 등이 구독해서 쓰는 이벤트
    public event Action<float, float> OnHpChanged; // (현재 HP, 최대 HP)
    public event Action OnDeath;

    public float CurrHp => currHp;
    public bool IsDead => isDead;

    void Awake()
    {
        currHp = maxHp;
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
        OnDeath?.Invoke();
    }
}
