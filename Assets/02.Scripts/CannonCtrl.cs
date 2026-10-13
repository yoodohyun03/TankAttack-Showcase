using UnityEngine;

public class CannonCtrl : MonoBehaviour
{
    Transform tr;
    public float rotSpeed = 100.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    float currAngle = 0.0f;
    public float minAngle = -20.0f;
    public float maxAngle = 20.0f;

    void Start()
    {
        tr = GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        float angle = -Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * rotSpeed;
        currAngle += angle;
        currAngle = Mathf.Clamp(currAngle, minAngle, maxAngle);
        tr.localRotation = Quaternion.Euler(currAngle, 0.0f, 0.0f);
    }
}
