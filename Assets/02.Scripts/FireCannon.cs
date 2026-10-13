using UnityEngine;

public class FireCannon : MonoBehaviour
{
    public GameObject cannon = null;
    public Transform firePos;
    AudioClip fireSfx = null;
    AudioSource sfx = null;
    private void Awake()
    {
        cannon = (GameObject)Resources.Load("Cannon");
        fireSfx = Resources.Load<AudioClip>("CannonFire");
        sfx = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Fire();
        }
    }
    void Fire()
    {
        sfx.PlayOneShot(fireSfx, 1.0f);
        GameObject obj = Instantiate(cannon, firePos.position, firePos.rotation);
        obj.GetComponent<Cannon>().owner = transform.root; // 자기 포탄에 맞지 않도록 발사한 탱크를 기록
    }
}
