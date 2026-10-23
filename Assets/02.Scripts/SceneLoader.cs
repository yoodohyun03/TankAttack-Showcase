

using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public bool startWithEnterKey = false; // Enter 키로 시작 여부

    void Start()
    {
        // 일시정지 상태에서 씬을 옮겨 왔을 수 있으므로 시간과 일시정지 상태를 원래대로
        Time.timeScale = 1.0f;
        PauseState.IsPaused = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }
    void Update()
    {
        if (startWithEnterKey && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            StartGame();
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1.0f;          // 일시정지(시간 0) 상태로 다음 씬이 시작되지 않도록
        PauseState.IsPaused = false;
        SceneManager.LoadScene("scBattleField");
    }
    // 보스전 바로가기: 마지막(보스) 웨이브부터 게임 시작 (빠른 확인·시연용)
    public void StartBossFight()
    {
        WaveManager.SkipToLastWave = true;
        StartGame();
    }

    public void GoToIntro()
    {
        SceneManager.LoadScene("scIntro");
    }
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("게임 종료");
    }


}