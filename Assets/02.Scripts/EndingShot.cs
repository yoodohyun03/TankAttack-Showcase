using UnityEngine;
using UnityEngine.UI;

// 승리·패배 씬 배경: 전투가 끝나는 순간의 실제 화면(HUD 제외)을 보여 줌
// - WaveManager가 엔딩 씬으로 넘어가기 직전에 화면을 찍어 Captured에 저장 (static이라 씬이 바뀌어도 유지)
// - 엔딩 씬에서는 그 화면을 꽉 채워 띄우고 아주 천천히 확대해서 멈춘 사진처럼 보이지 않게 함
public class EndingShot : MonoBehaviour
{
    public static Texture2D Captured;
    public static bool Capturing;    // 화면을 찍는 동안 true → OnGUI 표시들이 숨음

    public RawImage view;            // 배경으로 쓸 RawImage (Canvas 맨 뒤)
    public float zoomPerSecond = 0.01f;
    public float maxZoom = 1.12f;

    void Start()
    {
        if (view == null) return;
        if (Captured != null)
        {
            view.texture = Captured;
            view.color = Color.white;
        }
        else
        {
            // 씬을 직접 열어서 실행한 경우 등 찍힌 화면이 없으면 배경을 숨김
            view.enabled = false;
        }
    }

    void Update()
    {
        if (view == null || !view.enabled) return;
        float s = Mathf.Min(view.rectTransform.localScale.x + zoomPerSecond * Time.unscaledDeltaTime, maxZoom);
        view.rectTransform.localScale = new Vector3(s, s, 1.0f);
    }
}
