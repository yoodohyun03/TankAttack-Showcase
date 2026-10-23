using UnityEngine;
using UnityEngine.SceneManagement;

// Esc 일시정지 메뉴: 계속하기 / 처음으로 / 종료
// - Time.timeScale = 0으로 게임 시간을 멈춤 (Update는 계속 돌지만 deltaTime이 0이 되어 움직임이 멈춤)
// - 지도가 열려 있을 때의 Esc는 지도 닫기용이라 무시, 승패가 정해진 뒤에는 열지 않음
public class PauseMenu : MonoBehaviour
{
    public GameObject panel;     // 일시정지 화면 (평소에는 꺼 둠)

    TankCamera tankCam;
    WaveManager wave;

    void Start()
    {
        tankCam = FindAnyObjectByType<TankCamera>();
        wave = FindAnyObjectByType<WaveManager>();
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (MapStrike.IsMapOpen || MapStrike.ClosedFrame == Time.frameCount) return; // 지도 닫기용 Esc
        if (wave != null && wave.IsGameOver) return;

        if (PauseState.IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        PauseState.IsPaused = true;
        PauseState.ChangedFrame = Time.frameCount;
        Time.timeScale = 0.0f;
        if (panel != null) panel.SetActive(true);
        if (tankCam != null) tankCam.SetCursorLock(false); // 버튼을 누를 수 있게 커서 보이기
    }

    // 버튼: 계속하기
    public void Resume()
    {
        PauseState.IsPaused = false;
        PauseState.ChangedFrame = Time.frameCount; // 이 프레임의 클릭이 발사로 이어지지 않도록
        Time.timeScale = 1.0f;
        if (panel != null) panel.SetActive(false);
        if (tankCam != null) tankCam.SetCursorLock(true);
    }

    // 버튼: 처음으로
    public void GoToIntro()
    {
        Time.timeScale = 1.0f;
        PauseState.IsPaused = false;
        SceneManager.LoadScene("scIntro");
    }

    // 버튼: 종료
    public void QuitGame()
    {
        Application.Quit();          // 빌드한 게임에서만 동작
        Debug.Log("게임 종료");
    }

    void OnDestroy()
    {
        // 씬이 바뀌어도 멈춘 상태가 남지 않도록
        Time.timeScale = 1.0f;
        PauseState.IsPaused = false;
    }
}
