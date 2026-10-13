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

## 현재 구조 (강의 6주차까지 완료된 상태)
- 씬: `Assets/01.Scenes/scBattleField.unity`
- 스크립트 (`Assets/02.Scripts`)
  - `TankMove`: `Translate`와 `Rotate`로 이동과 회전. Rigidbody mass 2000, centerOfMass (0,-0.5,0)
  - `TankAnim`: Track 머티리얼의 `SetTextureOffset`(_MainTex, _BumpMap)으로 무한궤도 애니메이션
  - `SmoothFollow`: Main Camera가 탱크 자식 `CamPivot`을 따라감
  - `TurretCtrl`: 마우스 레이캐스트(8번 TERRAIN 레이어), `InverseTransformPoint`와 `Atan2`로 포탑 회전
  - `CannonCtrl`: 마우스 휠로 포신 상하 조절, `Mathf.Clamp`로 각도 제한 (minAngle/maxAngle)
  - `FireCannon`: 마우스 왼쪽 버튼으로 `Resources.Load("Cannon")` 생성, `FirePos`에서 발사, `PlayOneShot` 사운드
  - `Cannon`: `AddForce` 6000으로 발사. `OnTriggerEnter`에 맞으면 즉시, 안 맞으면 3초 뒤 폭발 코루틴 실행
- 포탄 프리팹과 발사 사운드는 `Assets/Resources`에 있음
- 주의: `Assets/03.Prefabs/ExplosionPack` 폭발 프리팹은 파티클이 깨져 있음 → 무료 에셋으로 교체 필요
