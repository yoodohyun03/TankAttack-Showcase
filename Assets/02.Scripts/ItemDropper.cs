using UnityEngine;

// 적이 파괴되면 확률적으로 아이템을 떨어뜨림
public class ItemDropper : MonoBehaviour
{
    [Range(0.0f, 1.0f)] public float dropChance = 0.4f; // 40% 확률
    GameObject[] itemPrefabs;

    void Awake()
    {
        // Resources/Items 폴더의 아이템 프리팹을 전부 불러옴
        itemPrefabs = Resources.LoadAll<GameObject>("Items");
    }

    void OnEnable()
    {
        EnemyTank.OnEnemyDied += DropItem;   // 적 파괴 이벤트 구독
    }

    void OnDisable()
    {
        EnemyTank.OnEnemyDied -= DropItem;
    }

    void DropItem(EnemyTank enemy)
    {
        if (itemPrefabs.Length == 0) return;
        if (Random.value > dropChance) return;

        GameObject prefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];
        Vector3 pos = enemy.transform.position + Vector3.up * 3.0f;
        Instantiate(prefab, pos, Quaternion.identity);
    }
}