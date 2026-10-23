using System.Collections;
using UnityEngine;

// 아이템: 플레이어 탱크가 닿으면 효과를 주고 사라짐
public class Item : MonoBehaviour
{
    public enum ItemType { Heal, Speed, Attack }
    public ItemType type = ItemType.Heal;
    public float value = 80.0f;      // Heal: 회복량 / Speed, Attack: 배율 (1.5 = 1.5배)
    public float duration = 10.0f;   // Speed, Attack 지속 시간(초)
    public float lifeTime = 20.0f;   // 안 먹으면 사라지는 시간
    public float bobHeight = 0.5f;   // 위아래로 움직이는 높이
    public float bobSpeed = 2.0f;    // 움직이는 속도
    float startY;                    // 처음 높이

    void Start()
    {
        Destroy(gameObject, lifeTime);
        startY = transform.position.y;
    }

    void Update()
    {
        // 눈에 띄도록 제자리에서 회전
        transform.Rotate(0.0f, 90.0f * Time.deltaTime, 0.0f);
        // 사인 곡선으로 위아래 왕복
        Vector3 pos = transform.position;
        pos.y = startY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = pos;
    }

    void OnTriggerEnter(Collider other)
    {
        // 플레이어 탱크(TankMove가 붙은 탱크)만 먹을 수 있음
        TankMove player = other.GetComponentInParent<TankMove>();
        if (player == null) return;

        switch (type)
        {
            case ItemType.Heal:
                player.GetComponent<TankHealth>().Heal(value);
                break;
            case ItemType.Speed:
                // 아이템은 바로 사라지므로, 지속 효과 코루틴은 플레이어 쪽에서 실행
                player.StartCoroutine(SpeedBuff(player));
                break;
            case ItemType.Attack:
                player.StartCoroutine(AttackBuff(player.GetComponent<FireCannon>()));
                break;
        }
        Debug.Log($"아이템 획득: {type}");
        Destroy(gameObject);
    }

    IEnumerator SpeedBuff(TankMove player)
    {
        float original = player.moveSpeed;
        player.moveSpeed = original * value;
        yield return new WaitForSeconds(duration);
        player.moveSpeed = original;
    }

    IEnumerator AttackBuff(FireCannon fire)
    {
        fire.damageMultiplier = value;
        yield return new WaitForSeconds(duration);
        fire.damageMultiplier = 1.0f;
    }
}