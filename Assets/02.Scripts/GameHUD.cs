using UnityEngine;
using UnityEngine.UI;

// 게임 화면 HUD: 각 스크립트의 상태를 읽어 UI에 표시
public class GameHUD : MonoBehaviour
{
    [Header("웨이브")]
    public Text waveText;
    public Text enemiesText;
    public Text statusText;

    [Header("체력")]
    public Image hpFill;          // Image Type: Filled
    public Text hpText;

    [Header("포탄")]
    public Text[] shellTexts;     // 일반탄, 고폭탄, 철갑탄 순서
    public Image reloadFill;      // Image Type: Filled

    [Header("지도 포격")]
    public Text strikeText;

    [Header("조준")]
    public GameObject crosshair;     // 일반 조준점
    public GameObject scopeOverlay;  // 정밀 조준 화면
    public Text scopeDistanceText;

    WaveManager wave;
    TankHealth health;
    FireCannon fire;
    MapStrike strike;
    TankCamera tankCam;

    void Start()
    {
        wave = FindAnyObjectByType<WaveManager>();
        tankCam = FindAnyObjectByType<TankCamera>();
        TankMove player = FindAnyObjectByType<TankMove>();
        if (player != null)
        {
            health = player.GetComponent<TankHealth>();
            fire = player.GetComponent<FireCannon>();
            strike = player.GetComponent<MapStrike>();
        }

        // HP는 바뀔 때만 이벤트로 갱신
        if (health != null)
        {
            health.OnHpChanged += UpdateHp;
            UpdateHp(health.CurrHp, health.maxHp);
        }
    }

    void OnDestroy()
    {
        if (health != null) health.OnHpChanged -= UpdateHp;
    }

    void UpdateHp(float curr, float max)
    {
        if (hpFill != null) hpFill.fillAmount = curr / max;
        if (hpText != null) hpText.text = $"HP  {curr:0} / {max:0}";
    }

    void Update()
    {
        // 웨이브
        if (wave != null)
        {
            if (waveText != null) waveText.text = wave.CurrentWave > 0 ? $"WAVE {wave.CurrentWave} / {wave.waves.Count}" : "READY";
            if (enemiesText != null) enemiesText.text = $"남은 적  {wave.EnemiesLeft}";
            if (statusText != null) statusText.text = wave.CurrentWave > 0 && wave.StatusText.StartsWith("WAVE") && !wave.StatusText.Contains("클리어") ? "" : wave.StatusText;
        }

        // 포탄: 선택된 포탄만 밝게
        if (fire != null)
        {
            for (int i = 0; i < shellTexts.Length && i < fire.shellTypes.Length; i++)
            {
                if (shellTexts[i] == null) continue;
                shellTexts[i].text = $"[{i + 1}] {fire.shellTypes[i].name}";
                shellTexts[i].color = i == fire.CurrentIndex ? fire.shellTypes[i].trailColor : new Color(1, 1, 1, 0.45f);
            }
            // 장전 바: 장전이 끝나면 가득 참
            if (reloadFill != null) reloadFill.fillAmount = 1.0f - fire.ReloadRemaining / fire.Current.reloadTime;
        }

        // 지도 포격
        if (strike != null && strikeText != null)
        {
            if (MapStrike.IsBusy) strikeText.text = "[M] 지도 포격: 포격 중";
            else if (strike.CooldownLeft > 0.0f) strikeText.text = $"[M] 지도 포격: {strike.CooldownLeft:0}초";
            else strikeText.text = "[M] 지도 포격: 준비 완료";
        }

        // 조준: 정밀 조준이면 조준경 화면, 아니면 일반 조준점 (지도가 열려 있으면 둘 다 숨김)
        if (tankCam != null)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool scope = tankCam.IsPrecision;
            if (crosshair != null) crosshair.SetActive(locked && !scope);
            if (scopeOverlay != null) scopeOverlay.SetActive(locked && scope);
            if (scope && scopeDistanceText != null) scopeDistanceText.text = $"{tankCam.AimDistance:0}m  [탄도 보정]";
        }
    }
}
