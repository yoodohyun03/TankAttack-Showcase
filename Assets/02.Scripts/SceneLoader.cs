

using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public bool startWithEnterKey = false; // Enter 키로 시작 여부

    void Start()
    {
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
        SceneManager.LoadScene("scBattleField");
    }
    // 보스전 바로가기: 마지막(보스) 웨이브부터 게임 시작 (채점·시연용)
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