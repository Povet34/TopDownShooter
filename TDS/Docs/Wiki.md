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
- **엄폐 실작동**: 배치된 엄폐물에 `Cover` 컴포넌트 + `CoverPoint` 4지점(오프셋 `coverPointOffset`로 풋프린트 밖, **NavMesh.SamplePosition으로 스냅 → 도달 가능한 지점만 생성**, 못 닿아 비비는 버그 방지). 원거리 적(coverPerk)이 `OverlapSphere`로 찾아 엄폐 → §4 적 AI와 연결. CoverPoint 마커 렌더러는 비활성(디버그용).
- **Cover 높이 가중치** (기획 2026-06-21): `Cover.CoverHeight`(렌더러 bounds) 기준 — **낮은 단상(≤0.8)=사격 가능(`IsShootable`, 교전 시 선호), 높은 것=은폐 전용**. 순수 `CoverEvaluation`(ShootFrom/HideOnly/Unusable). 교전 중 `Enemy_Range.AttemptToFindCover`는 **사격 가능한 cover만** 수집. 맵 생성은 낮은 단상(`lowCoverRatio`, 높이 ≤0.8)+높은 cover를 섞음. `Cover.AuditForRange()`로 검증(PlayMode `CoverAuditTests`).
- **맵 오브젝트 종류 (`Scripts/MapObjects/`)**: `MapObjectRole`(플래그: Blocking/Cover/Hide/Breakable/Movable) — 순수 `MapObjectClassifier.Classify(height,isCover,breakable,movable)`. `MapObject` 컴포넌트가 태그(Cover는 측정 높이로 자동 분류). **`Breakable`**(IDamagable — 총알이 `TakeDamage`, 순수 `BreakableHealth` 누적 피해→0이면 파편+제거), **`Movable`**(Rigidbody+회전고정+NavMeshObstacle carving, Bullet `OnCollisionEnter`가 `GetComponentInParent<Movable>().Push`). 맵 생성이 배럴(movable+breakable) 배치.
- **적 끼임 안전망**: 순수 `StuckTracker`(이동하려는데 1.5s 진전<0.5면 끼임) + `Enemy.UpdateStuckRecovery`(가까운 navmesh 바닥으로 `agent.Warp`+재경로). 낮은 장애물 위 베이크/회피 교착 등 원인 불문.
- **씬 `Assets/Scenes/Map_Generated.unity`**: "맵만 있는 씬" — Light/Camera(+CameraFollow)/MapGenerator/EntryPoint/PlayerSpawner/PlayerMapBootstrap/**SpawnDirector**(5웨이브)/**HUD**/**AimReticle**.
- **`AimReticle`**(TDS.Game): 조준 시각화. `Player_AimController`가 시스템 커서를 숨겨 조준 위치가 안 보이던 문제 해결 — 마우스 위치 스크린 크로스헤어 + 에임 타겟(`player.aim.Aim()` 월드 히트) 바닥 링. 캔버스/도형 코드 생성.
- **`MapHUD`**(TDS.Game): 자족형 미니 HUD. 캔버스/TMP를 코드로 생성(UI 프리팹·기존 UI 싱글톤 의존 X). 체력·현재무기 탄약(탄창/예비)·웨이브 표시. **승리**(전 웨이브 클리어)/**패배**(체력 0) → 종료 패널 + **R 재시작**(Input System). 재시작은 씬 리로드(빌드세팅 등록됨); `PlayerSpawner`가 재스폰 직전 `IControlsService.RecreateControls()`로 옛 입력 구독 누수 차단.
- 같은 시드 → 같은 맵. `MapGenerator.onGenerated` 이벤트로 후속(스포너/카메라) 연동.

---

## 4. 플레이어 (TDS.Game + TDS.Core 글루)

- **프리팹 `Resources/Player.prefab`** (SampleScene Player에서 추출, 비파괴). 태그 `Player`.
- **`PlayerSpawner`**(Core): EnsureSystems 후 맵 중앙(`PlayerSpawnPoint`)에 스폰, 인스턴스명 `Player`(적 AI가 `Find("Player")`로 찾음).
- **`PlayerMapBootstrap`**(Game 글루): 스폰된 플레이어에 컨트롤 활성화 + 기본무기(Pistol+AutoRifle) 부여 — UI 없이 빈 맵에서도 전투.
- **카메라**: `CameraFollow`(Core)가 태그로 플레이어 추적(3/4 뷰) + `FollowPosition`(순수 시임). 추적 base와 별개로 `CameraShake`(순수) 오프셋·롤을 unscaled로 위에 더함(피드백 방지). 마우스 휠 줌(`CameraZoom` 순수 + `CameraZoomInput` 글루 → `offset×zoom`).
- **전투 연출(손맛)**: 무기 비의존 `ICombatFeedbackService`(`CombatFeedback`, Systems). 적 피격(`Enemy.GetHit`)/사망(`Die`)·플레이어 피격(`Player_Health`)에서 호출 → 카메라 셰이크(`CameraShake`) + 처치 시 히트스톱(`HitStop`, Time.timeScale) + 피격 FX(CFXR). 순수 시임 2종은 EditMode 테스트.
- **조준 IK**: aim 리그(`Head_Aim`/`Gun_Aim` MultiAimConstraint) source = 프리팹 내부 `Aim_Target`. `AimRotation.FaceHorizontal`(0벡터 가드).
- **무기 조준(탑다운)**: 무기·총구·총알 방향은 **수평**(`AimDirection.ResolveHorizontal`) — 조준점이 무기 바로 아래(발밑)면 거의 수직→`LookRotation` up-모호성으로 **무기가 팽글팽글 도는** 버그가 있어 수평+0벡터 가드로 해결. 프리팹 `isAimingPrecisly` 기본 false(정밀조준은 우클릭 홀드 기능).
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

> 원본 설계: `SpawntableGenerator/Docs/SpawnSystem-Design.md`.
>
> **방향 전환(확정): WAVE → 패트롤.** WAVE(`SpawnDirector`)는 임시 골격. 목표는 **스폰테이블로 묶인 패트롤
> 그룹(3~15)이 순찰하다 플레이어를 발각하면 추격**(§6.2 인지 + §6.3 FSM). 현재 적엔 idle↔move 순찰 +
> 거리기반 감지→chase 골격만 있음(시야·그룹조율·유틸리티 이동 전무 — 코드 감사로 확인).
>
> **합의 우선순위:** ① 크로스헤어 ✅ → ② 이동 애니 폴리시(제자리걸음) → ③ **§6.5 BattleMover**(가장 원하는 "지능", 교전 이동 레이어라 자족적) → ④ §6.2 인지 + §6.3 FSM + 패트롤 스폰(웨이브 대체).

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

### 6.5 교전 이동 — 시야-회피 유틸리티 스코어링 `BattleMover` (사용자 §12)
> **포위는 목표가 아니라 결과.** 몬스터는 플레이어 시야를 피하려 움직이고, 그 결과 "어쩌다 포위"가 됨. 고정 슬롯 포위(균일) 아님 — 창발적.
> **구현됨**: 순수 `BattleMover`(FrontExposure·Score·PickEngagePosition·**ViewAvoidWeight** 그레이스 게이트, EditMode 13).
> - **근접(ChaseState_Melee)**: 평소엔 공격 사거리(`attackData.attackRange×0.85`)까지 **근접해 둘러싸 공격**(포위=근접). **최근 피격(그레이스 2.5s) 시에만** 강한 시야 회피로 안전 각 재배치. in-game: 9/10 근접 공격.
> - **원거리(BattleState_Range)**: 기본적으로 **피격당하거나 시야로 발각되면 근처 엄폐 우선 시도**(`RunToCoverState`로 숨음), 적절한 엄폐가 없으면 **BattleMover로 사거리 유지 strafe 재배치**(폴백). 결정은 순수 `RangedEngageDecision.Decide`(threatened·inDanger·coverAllowed·coverAvailable → TakeCover/Reposition/Hold, EditMode 6). 전투 중 `agent.updateRotation=false`로 플레이어를 보며 이동(strafe). `Enemy_Range`/`Sniper` prefab `coverPerk=CanTakeAndChangeCover` 기본화.
> - **엄폐 도달 견고화**: 엄폐점이 navmesh에서 살짝 벗어나(PathPartial) 정확히 못 닿으면 `RunToCoverState`가 무한 대기하던 버그 → "더 못 가고 정지하면 도착 간주"로 전이.
> - 회피 강도 = `Enemy.LastTimeDamaged` 기반 `ViewAvoidWeight`(피격 직후 高 → 그레이스 동안 감쇠).
> - **사선뛰기 애니 (완료)**: BattleMover 재배치(엄폐 없는 폴백) 중 적이 **플레이어를 조준한 채 다리만 옆/대각으로 달림**. Pro Rifle Pack 방향 달리기 8종(Mixamo, Humanoid 재임포트)을 `Enemy_Range.controller`의 2D 블렌드(`Strafe`, FreeformCartesian, StrafeX/Y)로. 순수 `StrafeBlend.Compute(velocity, facing)`(EditMode 10)가 속도→블렌드 파라미터, `BattleState_Range`가 `Strafing` bool + 파라미터 구동. 상체 조준은 Rifle 레이어가 유지.
> - **2차 (완료)**: ① **몹 간 소프트 간격** — `BattleMover.SpacingPenalty`(allies가 spacingRadius 안일수록 페널티) → 멜레·원거리 둘 다 `Enemy.NearbyAllyPositions`로 아군 전달(겹침/뭉침 완화). ② **회피 행동(능력 게이트)** — 순수 `EvasionPlanner.Decide`(Hold/Strafe/Backstep/Flee): 저체력+도주플래그→Flee, 너무 가까움→Backstep, 그 외 위협→Strafe. `TargetDistance`로 목표 사거리 조절. 원거리 `BattleState_Range`가 체력·`canStrafe/canBackstep/canFlee` 플래그로 구동. EditMode: BattleMover 17 + EvasionPlanner 8.
- **플레이어 "시야" 인식**: 현재 forward 콘 + 최근 공격 방향(감쇠) → "압박(pressure)" 점수.
- **회피 행동(능력 게이트, 선호순)**: 시야콘 이탈 → strafe(좌우, 바라보는 방향과 이동 분리) → backstep → 저체력시 도주(도주 플래그 적만).
- **유틸리티 스코어링**: 후보 목적지에 점수(시야콘 회피 ← 핵심 / 선호 교전거리 / 다른 몹과 소프트 간격(강제 아님) / 행동비용 / 관성·이력)를 매겨 최선 선택. 즉각 반응 X(점수+확률+이력).
- **거리 레짐**: 멀면 스코어링 끄고 직진, 근접권에서만 스코어링, 원거리몹은 선호 사거리 유지.
- 구현: 순수 `BattleMover`(후보 점수화 → 목적지, EditMode) + 글루(Enemy battle 상태가 매 틱 목적지 받음). 가중치는 per-monster 설정 + 튜닝.

### 6.4 기타 추후
- 군집(Pack) 가상 앵커 + boids, navmesh "군집당 1경로"(성능), 화면 밖 스폰(절두체 후보점).
- 전투 연출: 히트스톱·카메라 셰이크·피격 FX·사망 랙돌·총알 임팩트 구현됨. 추가 폴리시(데미지 넘버·피격 플래시/적 머티리얼 점멸 등)는 추후.
- 맵 청크 스티칭(광역 맵 이어붙이기) — 단일 맵 비주얼·엄폐는 Phase B, HUD·승패·재시작은 Phase D1에서 완료. 차량 재통합, 미션 재통합 남음.
- 사운드(보류): 총소리·근접 swoosh 등. `AudioManager` 부트 + SFX 연결 시 자동 재생되도록 가드해 둠(melee swoosh 등).
- 미래: 생존 루프 · 광역 스티칭 · 동굴(씬 전환) · 수송선 탈출/전리품 반출 · 인벤토리/파밍.

### 6.6 플레이어 시야 / 전장의 안개 (FoV) — **구현 완료 (셰이더 마스크, 2026-06-21)**
> 시야 콘+사거리 안 + 가려지지 않은 곳만 밝게, 나머지는 회색(맵은 보이되 적은 안 보임). 발사 시 밝아짐.
**구현**: 순수 `ViewCone`(콘+거리, EditMode 9) + `FieldOfView`(콘+사거리+눈높이 레이캐스트 차폐+nearRadius+Reveal, 적 renderer on/off, PlayMode 4) + `Player_FieldOfView`(조준 방향 구동·발사 시 Reveal). 비주얼 = `VisionMask`(**GPU**) + `Shaders/VisionFog`(지면 fog 쿼드, `alpha=(1-mask)*MaxDarkness`로 시야 밖 회색, 9-tap 블러로 소프트 엣지). occluder=Default|Environment(낮은 cover는 넘어 봄).
- **GPU 마스크**: 플레이어에서 360° 레이로 가시성 폴리곤 메시 생성(콘 안=장애물까지 레이캐스트, 밖=nearRadius 작은 원) → 탑다운 직교 행렬로 **CommandBuffer가 메시를 512 RT에 직접 렌더**(URP 카메라/데칼 파이프라인 우회 — 추가 카메라는 DBuffer assert로 터짐). 비용 = 레이 ~rayCount개(콘 안만) + 메시 1장 → in-game 토글 시 fps 차이 거의 없음(이전 CPU 텍셀별 수천 레이캐스트/프레임을 대체). `Shaders/VisMesh`(흰색)로 폴리곤 렌더, fog 쿼드는 RT를 bilinear+블러 샘플(D3D Y-flip 보정). 검증: 폴리곤 정점(전방 멀리/후방 nearRadius·장애물에 잘림, PlayMode 2) + 마스크 텍셀(ReadMaskAt 전방 1.0/후방 0.0) + 명도 스크린샷.
- 튜닝(Inspector): rtResolution·worldSize·rayCount·fogColor·maxDarkness·`_BlurSize` 등.

**방식**: **가시성 마스크 RenderTexture** + **지면 셰이더** (`color = lerp(어두운 회색, 원색, mask)`).
- **마스크 생성**: 플레이어에서 시야 콘 범위로 레이캐스트 → 가려지는 지점은 0, 보이는 지점은 1. 탑다운 직교 카메라가 가시성 폴리곤 메시(플레이어 중심 부채꼴, 정점=레이 끝/장애물 모서리)를 마스크 RT에 흰색으로 렌더 → 차폐가 폴리곤을 잘라 자연 occlusion.
- **지면 셰이더**: 월드 XZ → 마스크 UV(마스크 카메라 직교 영역)로 샘플 → 밝기 보간. 부드러운 falloff로 2D 느낌 억제.
- **적 숨김(게임플레이)**: 적 위치를 순수 `ViewCone.InView`(각도+거리) + 레이캐스트 차폐로 판정 → 안 보이면 renderer off. (마스크와 별개로 정밀 판정.)
- **발사 시 밝아짐**: 발사 직후 콘 각/사거리 일시 확대(또는 주변 원형 플래시) → 마스크에 반영.

**추후(별도)**: 광원 보유 적(횃불) = 벽 뒤라도 마스크에 가산 스폿 → 보임 · 낮/밤으로 콘 각/사거리 스케일 · 플레이어 소리 인지 범위 · 적 인지(§6.2)와 대칭 통합.

**정밀 검증**: 순수 `ViewCone` 단위테스트(각도/거리 경계) + PlayMode(콘 안=보임, 차폐 뒤=숨김, 사거리 밖=숨김) + **마스크 RT 텍셀 샘플링**(아는 좌표 가시/비가시 값) + 화면 명도 샘플링(밝음/어둠 수치).

### 6.7 카메라
- **마우스 휠 줌 ✅**: `CameraZoom`(순수) + `CameraFollow.AddScroll` + `CameraZoomInput`(글루). 휠 위=줌인, 아래=줌아웃, clamp.
- **무기별 에임-방향 오프셋 (추후)**: 우클릭(정밀 조준) 시 무기 타입에 따라 카메라를 **에임 방향으로** 이동 — 피스톨 ~3, 스나이퍼 ~10. 플레이어가 화면 중앙이 아니라 총구 방향 쪽으로 치우쳐 더 멀리 보게. `CameraFollow`에 에임-방향 오프셋 + 무기별 거리(Weapon 데이터) + 정밀조준 토글 연동. 줌과 합성.

---

## 7. 테스트

- **EditMode** (순수 로직): ServiceRegistry·BootSequence·GameServices·SystemsEnsurer·AimRotation·PlayerSpawnPoint·FollowPosition·SpawnSelection·WaveSequencer·GameOutcome·HitStop·CameraShake·LocomotionAnim·BattleMover(시야-회피/그레이스 회피)·CameraZoom·AimDirection(조준 0벡터 가드)·RangedEngageDecision(엄폐/재배치 결정)·StrafeBlend(facing 기준 2D 블렌드)·**CoverEvaluation(높이→적합도)·CoverApproach(arrival/비비기 방지)·EvasionPlanner(strafe/backstep/flee)·BattleMover 소프트 간격·StuckTracker(끼임 감지)·BreakableHealth·MapObjectClassifier·**ViewCone(시야 콘)**. **144 green.**
- **PlayMode** (통합): 부트, Player(스폰·컨트롤·이동·무기·사격·피해), Enemy(피해→사망·**사망 후 래그돌 고정**), SpawnDirector(웨이브), MapGenerator(결정성·중앙비움·경계), Cover(엄폐 획득), ControlsManager.RecreateControls, CombatFeedback(처치 히트스톱), Locomotion(anim 속도), BattleMove(**근접: 평소 근접/피격시 회피, 원거리: 피격시 재배치**), **CoverAudit(각 cover의 range 적합도 분류)**. **30 green.**
- **TDD 하네스 가이드: [Testing.md](Testing.md) · 작업 루프: [Workflow.md](Workflow.md)** — 새 기능은 여기 규칙대로(시임 먼저 → EditMode, 통합은 PlayMode).
- 실행: Test Runner 창 또는 MCP `run_tests(mode, assembly_names)`.
- 한계: navmesh 의존 적 AI 테스트는 테스트 씬에 navmesh 베이크 필요(`EnemyCombatTests` 참고).
