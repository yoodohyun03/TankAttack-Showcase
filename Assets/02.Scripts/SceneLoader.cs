

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