# Escape Mine

산소가 소진되기 전에 광산의 최상단 돌을 하나 파괴해 탈출하는 Windows 2D 게임입니다.
C# / .NET 9 / WinForms / glc2d / Direct2D로 구현했습니다.
기획 원문은 [doc/Gamedesign.md](doc/Gamedesign.md)입니다.

## 실행

Visual Studio에서 `BrickOut/BrickOut.sln`을 열고 실행하거나, .NET 9 SDK가 설치된 환경에서 아래 명령을 사용합니다.

```powershell
dotnet run --project BrickOut/BrickOut.csproj
dotnet build BrickOut/BrickOut.csproj -c Release
```

Release 실행 파일은 `BrickOut/bin/Release/net9.0-windows7.0/BrickOut.exe`입니다.
배포 시 EXE만 옮기지 말고 같은 폴더의 DLL, JSON, `resource` 폴더를 함께 전달해야 합니다.
이 Build는 .NET 9 Desktop Runtime이 필요합니다.

## 조작

| 입력 | 동작 |
| --- | --- |
| Space | 타이틀 → 대기 |
| 마우스 이동 | 곡괭이 이동 |
| 좌클릭 | 대기에서 발사 / 플레이 중 스윙 |
| 우클릭 | 일반·강타 전환 |
| R | 결과에서 타이틀로 복귀 |
| ESC | 타이틀·결과에서 종료 |
| M | 음소거 전환 |
| Alt+Enter | 전체 화면 전환 |

밝은 사각형은 스윙 판정 범위입니다. 공이 범위 안에 있을 때 클릭해야 타격합니다.
공을 놓치면 바닥에서 반사되며, 산소와 시간이 계속 소모됩니다.
게임 창이 포커스를 잃으면 플레이가 일시 정지됩니다.

## 구현된 규칙

- Title → Ready → Play → Result. Ready에서는 산소·시간이 정지합니다.
- 일반 타격 Power 1, 강타 Power 2. 타격 지점에 따라 발사 각도를 바꾸며 강타는 수직에 가깝습니다.
- 강타는 명중·헛스윙 모두 한 번 사용 후 해제됩니다.
- 돌은 내구도 1/2. 남은 Power만큼 피해를 주고, Power가 남으면 관통하며 0이 되면 반사됩니다.
- 18열의 돌을 화면 양끝까지 배치해 측면 우회를 막습니다. 별도의 측벽은 없으며 가장자리 돌도 파괴할 수 있습니다.
- 손상된 화강암은 균열과 내구도 숫자로 표시합니다.
- 산소 100, 자연 감소 0.25/초, 일반 스윙 1, 강타 2.5, 산소통 회복 20(최대 100).
- 산소통은 돌이 없는 칸에 있으며 공의 Power와 진행 방향을 유지합니다.
- 최상단 돌 하나 파괴 시 승리. 산소 0이면 패배. 산소 소진 판정이 같은 Simulation Step의 충돌보다 우선합니다.
- 일반 돌 20점, 화강암 40점, 승리 시 잔여 산소 × 20의 정수 부분을 보너스로 지급합니다.
- 결과에서 최종 점수·플레이 시간·세션 최고 점수를 표시합니다. 최고 점수는 앱 종료 시 초기화됩니다.

## 기획서 미정 항목의 구현 결정

- 기존 요청대로 논리 해상도는 1200×900(4:3). Window/전체 화면 비율이 달라지면 Letterbox로 비율을 유지합니다.
- 필드는 18열×8행을 모두 표시합니다. 화면 밖 필드·스크롤은 구현하지 않았습니다.
  배치의 예측 가능성과 남은 탈출 경로 확인을 우선한 첫 완성 버전입니다.
- 최상단은 모두 돌, 아래 7개 행은 각 한 칸에 산소통, 돌은 약 27% 확률로 화강암입니다.
- 공의 속도는 510px/초이며 Physics는 240Hz Fixed Step으로 처리합니다.
- 이미지 원본은 변경하지 않았습니다. 공·산소 게이지·균열·타격 효과는 Direct2D로 그립니다.
- 원격 `sound` 커밋의 실제 WAV 효과음·광산 배경음과 강타 이미지를 통합했습니다.
  일반/강타 스윙, 모드 전환, 돌 파괴, 반사, 산소, 호흡, 박수에 실제 음원을 사용하며,
  타이틀 BGM과 패배 효과음은 합성 사운드입니다. 저산소 호흡은 5초 간격으로 재생합니다.
  Audio 장치가 없으면 무음으로 동작합니다. 음원 출처는 `BrickOut/resource/sound/CREDITS.md`에 있습니다.
- 5분 플레이 목표와 산소 수치는 사람의 플레이 테스트로 추가 조정해야 합니다.

## 코드 변경점

| 파일 | 역할 / 변경 |
| --- | --- |
| `BrickOut/GameSession.cs` | 상태 전환·산소·점수·타격·승패 처리 및 게임 진행 총괄 |
| `BrickOut/GameRules.cs` | 해상도·18×8 배치·속도·산소 등 규칙 상수 |
| `BrickOut/GameObjects.cs` | Ball/Pickaxe/Rock 상태 데이터 |
| `BrickOut/RockField.cs` | Seed 기반 돌·산소통 배치 생성 |
| `BrickOut/BallPhysics.cs` | 공 이동·화면 경계 반사·원과 돌의 충돌·진입 면 보정 |
| `BrickOut/GameMain.cs` | 실제 Input을 게임에 전달하고 타이틀·필드·HUD·결과를 Rendering. Texture·Font·Brush 수명 관리 |
| `BrickOut/GameAudio.cs` | 실제 WAV 효과음·광산 BGM, 합성 타이틀 BGM·패배 효과음, BGM 전환·음소거 |
| `BrickOut/GameGlobal.cs` | Window 제목을 Escape Mine으로 변경 |
| `BrickOut/glc2d/G2AppBase.cs` | 포커스 확인과 화면 비율 유지·중앙 정렬 |
| `BrickOut/glc2d/G2InputContext.cs` | Frame 사이의 짧은 입력 이벤트 보관, 포커스 상실 시 Reset, Letterbox 마우스 좌표 변환 |
| `BrickOut/glc2d/G2Texture.cs` | 원본 크기에 맞춰 출력하기 위한 SourceBounds 추가 |
| `BrickOut/BrickOut.csproj` | Build/Publish 시 Resource 자동 복사 |
| `tests/` | 게임 규칙과 전체 플레이 자동 검증용 독립 Console 프로젝트 |

Framework의 기존 96 DPI 설정은 유지했습니다. 기존 그래픽 Resource 파일은 수정하지 않았습니다.
원격에서 가져온 `Pickaxe.cs`, `OxygenBreathTimer.cs`와 `doc/InteractionDemo.md`는 이전 조작 데모의 기록입니다.
현재 완성 게임은 `GameObjects.cs`의 곡괭이와 `GameSession.cs`의 산소 상태를 사용하며 F1~F5 미리듣기 대신 실제 게임 이벤트에 사운드를 연결합니다.

## 검증

```powershell
dotnet run --project tests/EscapeMine.Rules.csproj
```

49개 검사: Power/관통/화강암/산소통/벽·바닥/가장자리 돌 차단·파괴/승패/보너스/재시작과 20개 Seed의 전체 자동 플레이를 포함합니다.
자동 플레이는 공을 정확히 따라가는 검증용 입력이므로 사람의 난이도나 5분 목표를 증명하지 않습니다.
실제 앱에서는 타이틀·대기 화면과 Space, 우클릭 강타, 좌클릭 발사, 산소 소모, 돌 파괴·점수 증가를 확인했습니다.

2026-10-04 추가 UI 검증: 최종 Release에서 기본 창, 전체 화면(좌우 Letterbox),
가로 폭을 줄인 세로형 창(상하 Letterbox)을 확인했습니다.
세 경우 모두 4:3 게임 영역, 정사각형 돌, 원형 공, 하단 HUD가 잘리지 않았습니다.
전체 화면과 세로형 창에서 마우스 중앙에 곡괭이가 정렬되고 우클릭 강타가 반응했으며,
Alt+Enter로 전체 화면에서 창 모드로 복귀하는 동작도 확인했습니다.
