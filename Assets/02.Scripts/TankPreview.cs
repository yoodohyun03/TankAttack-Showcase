using UnityEngine;
using UnityEngine.UI;

// 전차 선택 화면의 3D 미리보기
// 화면 밖 먼 곳에 놓인 전차 모델 3대를 각각 전용 카메라로 비추고, 그 영상(RenderTexture)을 카드의 RawImage에 띄움
// - 전차는 제자리에서 천천히 돌고, 고른 전차는 더 빨리 돎
// - 색은 TankSelection의 전차별 색을 그대로 입혀서 실제 게임과 같은 모습
public class TankPreview : MonoBehaviour
{
    public Transform[] tanks;     // 표준 / 경전차 / 중전차 모델
    public Camera[] cameras;      // 각 전차를 비추는 카메라
    public RawImage[] views;      // 카드 안의 미리보기 화면
    public float spinSpeed = 25.0f;

    RenderTexture[] textures;

    void Start()
    {
        textures = new RenderTexture[cameras.Length];
        for (int i = 0; i < cameras.Length; i++)
        {
            textures[i] = new RenderTexture(512, 320, 16);
            cameras[i].targetTexture = textures[i];
            if (i < views.Length && views[i] != null) views[i].texture = textures[i];
        }

        // 전차별 색과 크기를 실제 게임과 똑같이 적용
        for (int i = 0; i < tanks.Length; i++)
        {
            TankSelection.ApplyLook(tanks[i], (TankSelection.TankType)i);
        }
    }

    void Update()
    {
        int picked = (int)TankSelection.Selected;
        for (int i = 0; i < tanks.Length; i++)
        {
            float speed = i == picked ? spinSpeed * 2.5f : spinSpeed;
            tanks[i].Rotate(0.0f, speed * Time.unscaledDeltaTime, 0.0f, Space.World);
        }
    }

    void OnDestroy()
    {
        if (textures == null) return;
        foreach (RenderTexture rt in textures)
        {
            if (rt != null) rt.Release();
        }
    }
}
