using UnityEngine;
using UnityEngine.UI;

// Intro의 전차 선택 화면
// - "게임 시작"(또는 Enter) → 이 화면을 열고, 전차 카드 3장 중 하나를 고른 뒤 "출격!"(또는 Enter)으로 시작
// - 1 / 2 / 3 키로도 선택, Esc로 닫기
// - 카드 글자(이름, 능력치)는 TankSelection의 값으로 채워서 코드와 화면이 항상 일치
public class TankSelectMenu : MonoBehaviour
{
    public GameObject panel;        // 전차 선택 화면 (평소에는 꺼 둠)
    public Image[] cards;           // 표준 / 경전차 / 중전차 순서
    public Text[] cardTexts;
    public Text descriptionText;
    public SceneLoader loader;

    static readonly Color Normal = new Color(0.92f, 0.96f, 1f, 0.85f);
    static readonly Color Picked = new Color(1f, 0.85f, 0.35f, 1f);

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        for (int i = 0; i < cardTexts.Length; i++)
        {
            TankSelection.Stats s = TankSelection.Get((TankSelection.TankType)i);
            cardTexts[i].text = $"<size=40><b>{s.name}</b></size>\n\nHP {s.maxHp:0}   속도 {s.moveSpeed:0}\n공격 {s.damageScale:0.0}배   장전 {s.reloadScale:0.0}배";
        }
        Refresh();
    }

    void Update()
    {
        bool open = panel != null && panel.activeSelf;
        if (!open)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Open();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Confirm();
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // 버튼: 게임 시작 → 전차 선택 화면 열기
    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        Refresh();
    }

    // 버튼: 뒤로
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    // 버튼: 전차 카드 (0: 표준, 1: 경전차, 2: 중전차)
    public void Select(int index)
    {
        TankSelection.Selected = (TankSelection.TankType)index;
        Refresh();
    }

    // 버튼: 출격!
    public void Confirm()
    {
        if (loader != null) loader.StartGame();
    }

    // 고른 카드만 강조하고 설명 갱신
    void Refresh()
    {
        int picked = (int)TankSelection.Selected;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null) cards[i].color = i == picked ? Picked : Normal;
        }
        if (descriptionText != null) descriptionText.text = TankSelection.Current.description;
    }
}
