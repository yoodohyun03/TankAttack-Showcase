# TankAttack — 융합게임제작실습Ⅱ 중간고사 (싱글 모드)

## 개요
- 과목: 융합게임제작실습Ⅱ (담당: 심한뫼 교수), 중간고사 과제형 시험, 30점
- **싱글 모드, Photon 사용 안 함.** 플레이어 vs 적 AI 탱크
- 네트워크(Photon) 버전은 기말고사용으로 `C:\GitHub\TankAttack-Photon`에서 따로 진행. 이 프로젝트에 네트워크 코드를 섞지 말 것
- Unity 6.5 (6000.5.10f1), 에셋 직렬화 Force Text
- GitHub: https://github.com/yoodohyun03/TankAttack (private)

## 마감 · 제출 (2026-10-25)
1. 게임 실행파일 .zip → 교수님 이메일
2. 게임기획서(PPT) → EClass 과제게시판
   - 게임 기획(소개, 조작법, 목표, 승리 조건)
   - 기능별 구현 여부: 구현한 기능은 방법과 주요 코드 설명 / 실패한 기능은 시도한 내용과 막힌 이유

## 확정된 기획 (자세한 내용은 `Docs/기획.md`)
- **설원 웨이브 서바이벌** (사막에서 변경, 다른 학생들과 겹치지 않게): 5웨이브를 버티면 승리, 시간이 남으면 마지막 웨이브에 보스 탱크 추가
- 나만의 기능: **정밀 조준 모드** (우클릭 유지 시 조준경 시점, 줌, 크로스헤어, 마우스로 정밀 조절)
- 나만의 기능 2: **지도 포격** (M키 지도에서 목표 클릭 → 탄도 공식으로 포신 각도 자동 계산 후 발사, 쿨타임)

## 요구사항
1. **Intro 씬**: 게임 방식이나 키 조작 설명, 버튼 또는 키 입력으로 Main 씬 시작
2. 게임 구현
   - 배경 변경: 기획에 맞게 지형, 건물 등
   - 플레이어 탱크 기능: **포신 각도 제한** (완료), 탱크와 포탄 종류 다양화
   - **적 AI 탱크 2종류 이상**: 외형, 체력, 공격력, 사거리, 이동속도 등을 다르게
   - 난이도 조절: Wave 또는 디펜스 형식
   - 아이템: HP, 이동 속도, 공격력 등 (현재 HP 변수는 없음)
   - 본인이 추가하고 싶은 기능 1개 이상
3. **Ending 씬**: 승리와 패배를 서로 다른 화면으로

## 규칙
- **Asset Store는 무료 에셋만** 사용 (공정성 때문에 유료 금지)
- AI 사용 허용. 단, 학생이 기능을 이해하고 기획서에 설명할 수 있어야 함
  → 구현할 때는 동작 원리를 함께 설명하고, PPT에 넣을 수 있게 핵심 코드를 짚어 줄 것
- 기획서에 **기능별로 "직접 한 부분 / AI 도움 받은 부분"을 구분해서** 적어야 함
  → 기능 하나가 끝날 때마다 `Docs/구현노트.md`에 구현 방법, 핵심 코드, 직접/AI 구분을 기록할 것
- 사용자가 직접 짜 보고 싶다고 하면 정답 대신 힌트와 리뷰 위주로 도울 것
- UI는 사용자가 직접 디자인할 예정 (강의 슬라이드 UI를 그대로 따라 하지 않음)
- 커밋과 푸시는 사용자가 요청하면 Claude가 진행
- **기능을 추가·수정하거나 버그를 고칠 때마다 문서를 함께 갱신할 것** (사용자 요청)
  - `Docs/구현노트.md`: 기능별 구현 방법, 핵심 코드, 직접/AI 구분
  - `Docs/기획.md`: 최종 게임 내용과 "기획 변경 이력" 표
  - `Docs/AI활용.md`: 사용자의 판단·결정, 직접 발견한 문제, AI가 틀린 사례
  - `scIntro` 조작법·안내 문구가 실제 게임과 맞는지
- 문서에는 실제로 한 일만 적을 것. 커밋 전에 바뀐 파일을 확인해서 사실과 다른 기록이 없는지 점검
- 문서 수정은 셸(`node -e`, `sed`) 대신 Edit/Write 도구로 할 것 (백틱이 셸 명령으로 실행되어 글자가 빠지거나 빈 파일이 생긴 적이 있음)

## 현재 구조
- 씬 (Build 순서): `scIntro`(0) → `scBattleField`(1) → `scWin`(2) → `scLose`(3), 모두 `Assets/01.Scenes`
- 진행 상황과 기능별 설명은 `Docs/구현노트.md`, 최종 기획은 `Docs/기획.md`, AI 활용 정리는 `Docs/AI활용.md`
- 스크립트 (`Assets/02.Scripts`)
  - 수업 코드: `TankMove`(이동, 건물 관통 버그 때문에 `Translate` 대신 `FixedUpdate`에서 Rigidbody 속도로 이동하도록 수정), `TankAnim`(궤도 애니메이션), `SmoothFollow`(옛 카메라, `TankCamera`가 자동으로 끔)
  - `TurretCtrl`: `TankCamera.AimPoint`(화면 중앙 조준점)로 포탑 회전
  - `CannonCtrl`: 조준점 기준 포신 각도(일반: 직선, 정밀·지도 포격: 탄도 계산), `Mathf.Clamp`로 각도 제한 (사용자가 직접 짠 구조)
  - `TankCamera`: 마우스 시점 카메라, 우클릭 정밀 조준(줌), 조준점 계산, `AimOverride`(지도 포격용)
  - `FireCannon`: 포탄 3종(1/2/3키), 재장전, 조준 모드별 명중 오차, `FireAt()`(지도 포격), 공격력 배율
  - `Cannon`: 속도 직접 지정 발사, 직격/범위 피해, 같은 진영 오인 사격·자기 피격·중복 폭발 방지
  - `TankHealth`: 체력 공용 컴포넌트, 진영(`Team`), `OnHpChanged`/`OnDeath`, 파괴 폭발
  - `EnemyTank`: 적·아군·보스 공용 AI(가장 가까운 상대 진영 추적, 탄도 조준, 보스 광폭화, 물리 이동, 장애물 회피, 끼임 탈출, 뒤집힘 복구), `OnEnemyDied`, `CurrentBoss`
  - `Ballistics`: 발사 각도 계산(적 AI, 정밀 조준, 지도 포격 공용)
  - `WaveManager`: 5웨이브, 마지막 보스, 아군 소환·증원, 소환 위치 검사, 승패 판정, 적 위치 표시, `SkipToLastWave`(보스전 바로가기), 채점용 단축키(F10 즉시 승리, F9 즉시 패배)
  - `MapStrike`: M키 전술 지도(확대·이동), 목표 지정, 4연발 포격, 쿨타임
  - `Item`, `ItemDropper`: 아이템 3종과 드롭 (사용자가 입력)
  - `SceneLoader`: 씬 전환 버튼, 보스전 바로가기 (사용자가 입력)
  - `GameHUD`: 게임 HUD와 보스 HP바
  - `PauseMenu`, `PauseState`: Esc 일시정지 메뉴(`Time.timeScale = 0`), 다른 스크립트가 일시정지 여부 확인
- 에디터 도구 (`Assets/Editor`, 메뉴 `Tools > TankAttack`)
  - `EnemyPrefabBuilder`: 적 3종·보스·아군 프리팹 생성 (수치를 바꾸면 다시 실행)
  - `UrpParticleMaterialConverter`: URP 전용 폭발 머티리얼을 Built-in으로 변환
  - `HudLayoutTool`: HUD 자동 배치, 보스 HP바·보스전 바로가기 버튼·일시정지 메뉴 추가 (HUD는 사용자가 직접 다듬었으므로 Layout Game HUD는 다시 실행하지 말 것)
  - `ItemDecorator`: 아이템 모양·조명 (사용자가 다듬은 뒤에는 다시 실행하지 말 것)
  - `WinterBaseBuilder`: 흰색 계열 군사 기지 배치, 건물 밑 지형 평탄화
- `Assets/Resources`: 포탄, 발사 사운드, `Enemies/`(적·보스), `Allies/`, `Items/`, `TankExplosion`
- 무료 에셋: Dynamic Snow System(눈 텍스처), Cinematic Explosions FREE(`Mirza Beig`, 머티리얼 변환함), Military Base Pack(`Tiny Teacup Studio`, 데모 폴더 삭제함)
- 임시 `OnGUI` 표시는 각 스크립트의 스위치로 꺼 둠. 적 위치 표시와 지도 화면은 유지
