# TDS 위키 (시스템 레퍼런스)

이 문서는 **현재 구현된 시스템의 동작 방식**과 **데이터 카탈로그**, 그리고 **추후 구현할 설계**를 정리한 레퍼런스다.
진행 계획·변경 이력은 [Roadmap.md](Roadmap.md) 참조. 스폰/AI 원본 설계는 형제 프로젝트 `SpawntableGenerator/Docs/` 참조.

> 한 줄: *데이터로 맵을 짜고, 부트가 시스템을 보장하고, 스포너가 데이터로 적을 띄운다. 게임플레이는 테스트로 고정.*

---

## 1. 어셈블리 구조 (asmdef)

| 어셈블리 | 경로 | 내용 | 참조 |
|---|---|---|---|
| `TDS.Core` | `Assets/Scripts/Core/` | 디커플링·데이터·순수 로직 (테스트 가능, 게임코드 역참조 금지) | UnityEngine, AI.Navigation |
| `TDS.Input` | `Assets/Input Manager/` | `PlayerControls`(InputSystem 생성) | Unity.InputSystem |
| `TDS.Game` | `Assets/Scripts/` | 게임플레이 전부(Player/Enemy/UI/Weapon/Mission/Car…) | TDS.Core, TDS.Input, 패키지들 |
| `TDS.Tests.EditMode` | `Assets/Tests/EditMode/` | 순수 로직 단위 테스트 | TDS.Core |
| `TDS.Tests.PlayMode` | `Assets/Tests/PlayMode/` | 통합 테스트(부트/Player/Enemy) | TDS.Core, TDS.Game, TDS.Input, InputSystem, AI.Navigation |

**규칙**: 새 디커플링/데이터/순수로직은 `TDS.Core`. 새 파일 추가 후 테스트가 안 잡히면 `refresh scope:all`(에셋 임포트)로 디스커버리 갱신.

### 폴더 레이아웃
- **`Assets/Imports/`** — 임포트한 서드파티 팩(벤더). 현재: `Models`, `SciFi_Space_Soldier`, `JMO Assets`(CFXR), `MagicaCloth2`, `VolumetricFog2`. **게임 저작물과 분리.** (TextMesh Pro는 Unity 관리라 루트 유지.)
- **게임 저작**: `Scripts`(TDS.Game) · `Tests` · `Input Manager`(TDS.Input) · `Data`/`GameData`(SO) · `Resources`(런타임 로드: Player/Systems/Spawn 테이블) · `Scenes` · `Prefab` · `Audio` · `Materials`/`Textures`/`Animations`/`Graphics` · `URP Settings`.
- **이동 규칙**: 폴더 이동은 반드시 `AssetDatabase.MoveAsset`(GUID/참조 보존). 파일시스템 mv 금지.
- **추후 — `TDS.Contents` (염두)**: 스킬/아이템 등 "기능"은 스크립트+모델+사운드+프리팹을 **기능 폴더 하나**에 몰고 자체 asmdef(`TDS.Contents.*`)로 분리할 계획. (수직 슬라이스형 콘텐츠 모듈.)

---

## 2. 부트스트랩 & 서비스 (TDS.Core)

- **`GameServices.Registry`** (`ServiceRegistry`): 타입→인스턴스 전역 레지스트리. 매니저가 Awake에서 인터페이스로 자기등록, 소비자는 Resolve. `.instance` 직접결합을 점진 대체.
- **`GameBootstrap.EnsureSystems()`**: `Resources/Systems` 프리팹을 **한 번만**(멱등) 인스턴스화 + DontDestroyOnLoad. `SystemsEnsurer`(순수 멱등 로직)로 구현.
- **`SceneEntryPoint`**: 씬에 하나 두면 진입 시 자동으로 EnsureSystems → **어느 씬이든 단독 Play/테스트 가능**(Boot 씬 강제 없음).
- **Systems 프리팹**(`Resources/Systems`): 전역 매니저 — TimeManager·MissionManager·GameManager·ObjectPool·ControlsManager (각각 인터페이스 `IClockService`/`IMissionService`/`IGameStateService`/`IObjectPoolService`/`IControlsService`로 등록). UI/카메라는 씬 종속이라 제외.

> 다이어그램(스폰 파이프라인): `입력(난이도/시간/어그로) → 스폰 디렉터(언제·얼마나) → 스폰 테이블(무엇·가중치) → 스폰 지점(어디) → 군집 스포너(어떻게) → navmesh 활성 군집 → (개체수 피드백)`. 현재는 **스폰 테이블 + 스포너 + 기본 웨이브 디렉터(클리어/타임아웃 진행)** 까지 구현, **긴장도(intensity) 페이싱/군집(Pack)/인지(FSM)** 는 §6 추후.

---

## 3. 맵 생성 (TDS.Game)

- **`MapGenerator`** + **`MapConfig`**(SO): 시드 결정적 그리드. 바닥/경계벽/장애물/엄폐물 + NavMesh 베이크. 프리팹 비면 프리미티브 폴백. 전용 `System.Random(seed)`(전역 Random 비오염).
- **데이터 기반 콘텐츠** (`Assets/GameData/Map/MapConfig_Default`): 사막 황무지 테마 — 바닥 머티리얼(`Mat_DesertSand`), 장애물 풀(부서진 차/연료탱크/콘크리트관/사막바위/돌/선인장), 엄폐물(`sea_container`). 임포트 프리팹은 바닥(y=0) 배치, 정적 MeshCollider는 `convex=false`(navmesh 카빙 + 충돌).
- **엄폐 실작동**: 배치된 엄폐물에 `Cover` 컴포넌트 + `CoverPoint` 4지점(오프셋 `coverPointOffset`로 풋프린트 밖). 원거리 적(coverPerk)이 `OverlapSphere`로 찾아 엄폐 → §4 적 AI와 연결. CoverPoint 마커 렌더러는 비활성(디버그용).
- **씬 `Assets/Scenes/Map_Generated.unity`**: "맵만 있는 씬" — Light/Camera(+CameraFollow)/MapGenerator/EntryPoint/PlayerSpawner/PlayerMapBootstrap/**SpawnDirector**(5웨이브)/**HUD**.
- **`MapHUD`**(TDS.Game): 자족형 미니 HUD. 캔버스/TMP를 코드로 생성(UI 프리팹·기존 UI 싱글톤 의존 X). 체력·현재무기 탄약(탄창/예비)·웨이브 표시. **승리**(전 웨이브 클리어)/**패배**(체력 0) → 종료 패널 + **R 재시작**(Input System). 재시작은 씬 리로드(빌드세팅 등록됨); `PlayerSpawner`가 재스폰 직전 `IControlsService.RecreateControls()`로 옛 입력 구독 누수 차단.
- 같은 시드 → 같은 맵. `MapGenerator.onGenerated` 이벤트로 후속(스포너/카메라) 연동.

---

## 4. 플레이어 (TDS.Game + TDS.Core 글루)

- **프리팹 `Resources/Player.prefab`** (SampleScene Player에서 추출, 비파괴). 태그 `Player`.
- **`PlayerSpawner`**(Core): EnsureSystems 후 맵 중앙(`PlayerSpawnPoint`)에 스폰, 인스턴스명 `Player`(적 AI가 `Find("Player")`로 찾음).
- **`PlayerMapBootstrap`**(Game 글루): 스폰된 플레이어에 컨트롤 활성화 + 기본무기(Pistol+AutoRifle) 부여 — UI 없이 빈 맵에서도 전투.
- **카메라**: `CameraFollow`(Core)가 태그로 플레이어 추적(3/4 뷰) + `FollowPosition`(순수 시임). 추적 base와 별개로 `CameraShake`(순수) 오프셋·롤을 unscaled 시간으로 위에 더함(셰이크가 추적에 피드백 안 됨).
- **전투 연출(손맛)**: 무기 비의존 `ICombatFeedbackService`(`CombatFeedback`, Systems). 적 피격(`Enemy.GetHit`)/사망(`Die`)·플레이어 피격(`Player_Health`)에서 호출 → 카메라 셰이크(`CameraShake`) + 처치 시 히트스톱(`HitStop`, Time.timeScale) + 피격 FX(CFXR). 순수 시임 2종은 EditMode 테스트.
- **조준 IK**: aim 리그(`Head_Aim`/`Gun_Aim` MultiAimConstraint) source = 프리팹 내부 `Aim_Target`. `AimRotation.FaceHorizontal`(0벡터 가드).
- **회복력 가드**: UI/Camera.main/CameraManager/currentWeapon/fogVolume null 컨텍스트에서도 안 죽도록 가드(맵 단독 실행).
- **망토(cape)**: 메시/본은 프리팹에 있으나 `MagicaCloth`(천 시뮬) 비활성 — 의존성 삭제로 크래시. 정리/복원은 추후.

---

## 5. 스폰 시스템 & 데이터 카탈로그 (TDS.Core)

- **`MonsterDef`**(SO): 기존 적 프리팹 래핑 + `weight`/`cost`/`tags`. 메뉴 `TDS/Spawn/Monster Def`.
- **`SpawnTable`**(SO): `MonsterDef[]` + `Pick(roll)` 가중 선택. 메뉴 `TDS/Spawn/Spawn Table`. 상황/난이도/테마별로 **여러 개**.
- **`SpawnSelection`**(순수): 가중 누적분포 선택(테스트됨).
- **`MonsterSpawner`**(컴포넌트): 플레이어 생성 후 navmesh 링에 **단일** 웨이브 시드 스폰. `count`/`minRadius`/`maxRadius`/`seed`.
- **`WaveSequencer`**(순수, 테스트됨): 웨이브 진행 결정 로직(Wait/SpawnNext/Done). 생존 수·경과시간·웨이브별 타임아웃으로 판정 — 전멸(clear) 또는 타임아웃 시 다음 웨이브, 마지막 클리어 시 종료. 긴장도(intensity)는 §6.1 추후.
- **`SpawnDirector`**(컴포넌트, **TDS.Game**): `WaveSequencer` 글루. 스폰한 적의 생존 수를 추적(적 타입 필요 → Game)하며 각 웨이브를 `SpawnTable`에서 navmesh 링에 스폰. `WaveDef[]`(table/count/maxWaveTime) + 시드 결정적. Map_Generated가 이걸 사용(5웨이브: Basic4→MeleeRush5→RangedDefense5→Mixed6→Boss4, 90s 안전 타임아웃).

### 데이터 카탈로그
`Assets/GameData/Spawn/` (MonsterDef), `Assets/Resources/` (테스트/런타임 로드용 테이블).

**MonsterDef** (`Assets/GameData/Spawn/MD_*.asset`):

| MD | 적 프리팹 | weight | cost |
|---|---|---|---|
| `MD_Melee` | Enemy_Melee | 3 | 1 |
| `MD_Melee_Shield` | Enemy_Melee - Shield Variant | 1 | 3 |
| `MD_Melee_Dodge` | Enemy_Melee - Dodge Variant | 1 | 3 |
| `MD_Range` | Enemy_Range | 2 | 2 |
| `MD_Range_Sniper` | Enemy_Range - Sniper Variant | 1 | 4 |
| `MD_Range_Cover` | Enemy_Range - Cover + Grenade Variant | 1 | 4 |
| `MD_Boss_Hummer` | Enemy_Boss_Hummer | 1 | 8 |
| `MD_Boss_Flame` | Enemy_Boss_Flamethrower | 1 | 8 |

**SpawnTable** (`Assets/Resources/*.asset` — 테스트/런타임 로드 가능):

| ST | 구성(가중치순) | 용도 |
|---|---|---|
| `ST_Basic` | Melee×2, Range×1 | 기본 혼합 (Map_Generated 기본) |
| `ST_MeleeRush` | Melee, Shield, Dodge | 근접 러시 |
| `ST_RangedDefense` | Range, Sniper, Cover | 원거리 방어전(엄폐 필요) |
| `ST_Mixed` | Melee, Range, Shield, Sniper | 균형 혼합 |
| `ST_Boss` | Boss_Hummer + Melee 호위 | 보스전 |

> 스포너의 `table` 필드를 바꿔 끼우면 테마가 바뀐다. cost는 추후 SpawnDirector 예산 스케일링용(§6.1).

> 기존 적 로스터(근접 5변형 + 원거리 6변형 + 보스 2종)는 그대로 재사용. MonsterDef로 래핑만 하면 됨.

---

## 6. 추후 구현 (설계 확정, 미구현) — ⏳

> 원본 설계: `SpawntableGenerator/Docs/SpawnSystem-Design.md`. **맵 수정 후 빠르게 진행 예정.**

### 6.1 긴장도(intensity) 기반 스폰 페이싱 — `SpawnDirector`
```
intensity     = w_time · (경과시간 / 최대시간) + w_obj · (1 − 남은목표 / 전체목표)
spawnInterval = lerp(최대간격, 최소간격, intensity)   // 최소간격으로 clamp(무한 스폰 방지)
```
- 목표를 깰수록 긴장도↑ → 스폰 간격↓ (진행 = 압박 증가). 목표 0 → 엔드게임 전환.
- 구현 시 `TensionCalculator`(순수, 테스트) + `SpawnDirector`(MonoBehaviour, DirectorProfile SO).

### 6.2 발각(인지) 모델 — 시야/소음/거리
- **시야 = 유일한 진짜 "발각" 채널**: 플레이어가 (시야각 콘 안 + 사거리 안 + LoS 차단 없음) → 발각.
- **소음 = "발각"이 아니라 "고개를 돌리게 하는 트리거"**: 소리 나면 그쪽으로 시야를 돌려 조사 → 그 결과 콘에 들어오면 발각.
- **거리 = 시야의 게이트**: 시야 안이어도 너무 멀면 미발각, 가까우면 확정. 시야 밖이면 거리 무의미.

### 6.3 3상태 FSM + 공용 칠판(blackboard)
```
순찰(패트롤 전진) --소음--> 경계(자극원 조사) --시야 확인--> 교전(추격+증원)
        <--조사 실패(타임아웃)--          <--시야 상실--
순찰 --직접 시야(근거리)--------------------------> 교전
```
- 멤버는 각자 콘으로 감지(분산 센서). 한 놈이라도 발각 → 공용 칠판 `knownPlayerPosition`에 기록 → **전원 교전**.
- 전원 시야 상실 + T초 경과 → 위치 낡음 → 경계(마지막 위치 수색) → 순찰 복귀.
- 구현 시 순수 `PackFsm`(전이 규칙, 테스트) + MonsterPack 통합.

### 6.4 기타 추후
- 군집(Pack) 가상 앵커 + boids, navmesh "군집당 1경로"(성능), 화면 밖 스폰(절두체 후보점).
- 전투 연출: 히트스톱·카메라 셰이크·피격 FX·사망 랙돌·총알 임팩트 구현됨. 추가 폴리시(데미지 넘버·피격 플래시/적 머티리얼 점멸 등)는 추후.
- 맵 청크 스티칭(광역 맵 이어붙이기) — 단일 맵 비주얼·엄폐는 Phase B, HUD·승패·재시작은 Phase D1에서 완료. 차량 재통합, 미션 재통합 남음.
- 사운드(보류): 총소리·근접 swoosh 등. `AudioManager` 부트 + SFX 연결 시 자동 재생되도록 가드해 둠(melee swoosh 등).
- 미래: 생존 루프 · 광역 스티칭 · 동굴(씬 전환) · 수송선 탈출/전리품 반출 · 인벤토리/파밍.

---

## 7. 테스트

- **EditMode** (순수 로직): ServiceRegistry·BootSequence·GameServices·SystemsEnsurer·AimRotation·PlayerSpawnPoint·FollowPosition·SpawnSelection·WaveSequencer·GameOutcome·**HitStop·CameraShake**. **50 green.**
- **PlayMode** (통합): 부트(서비스등록·멱등·영속·씬단독), Player(스폰·컨트롤·이동·무기장착/전환·사격·피해), Enemy(피해→사망), SpawnDirector(웨이브), MapGenerator(결정성·중앙비움·경계), Cover(엄폐 획득), ControlsManager.RecreateControls, **CombatFeedback(서비스 등록·처치 히트스톱)**. **23 green.**
- **TDD 하네스 가이드: [Testing.md](Testing.md) · 작업 루프: [Workflow.md](Workflow.md)** — 새 기능은 여기 규칙대로(시임 먼저 → EditMode, 통합은 PlayMode).
- 실행: Test Runner 창 또는 MCP `run_tests(mode, assembly_names)`.
- 한계: navmesh 의존 적 AI 테스트는 테스트 씬에 navmesh 베이크 필요(`EnemyCombatTests` 참고).
