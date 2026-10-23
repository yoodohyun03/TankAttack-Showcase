// 일시정지 상태를 다른 스크립트(카메라, 지도 포격 등)가 확인할 수 있게 공유
// 일시정지 메뉴 스크립트가 값을 바꿈
public static class PauseState
{
    public static bool IsPaused;
    public static int ChangedFrame = -1; // 일시정지를 켜거나 끈 프레임 (같은 프레임의 Esc·클릭을 다른 곳에서 다시 처리하지 않도록)
}
