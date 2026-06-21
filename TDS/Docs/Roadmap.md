# TDS 개발 로드맵 & 현재 코드 지도 (계획 문서, Living Document)

탑다운 슈터(TDS)를 **데이터 기반 절차적 맵 + 분리된 몬스터 스폰 시스템**으로 재구성하고,
최종적으로 **생존 / 광역 맵 / 동굴 전환 / 수송선 탈출 + 전리품 반출 / 인벤토리·파밍**까지 확장한다.

> **룰**: 기획이 바뀌면 이 문서를 먼저 고치고 코드는 그다음. 충돌/모호하면 구현 전에 사용자에게 확인(추측 금지).
> 상태표기: ✅완료 · 🔧진행중 · 📋미착수 · ❌보류
>
> 참고 설계: 형제 프로젝트 `SpawntableGenerator/Docs/` (SpawnDirector·SpawnTable·MonsterDef·예산/긴장도 스폰).
> 이 프로젝트(TDS)에 **적용 가능한 개념만 골라** 이식한다(§3).

---

## 1. 현재 코드 상태 (직접 확인함 — 유지보수 기준선)

### 1.1 맵 생성 — `Assets/Scripts/LevelGeneration/`
- **방식**: `LevelGenerator`가 `levelParts` 리스트에서 랜덤으로 하나씩 꺼내 **일렬(체인)로 스냅**해 이어붙임.
  `LevelPart.SnapAndAlignPartTo()`가 Enter/Exit `SnapPoint`로 정렬·배치, `IntersectionDetected()`(OverlapBox)로
  겹치면 **전체 재생성**. 끝에 `lastLevelPart` 붙이고 `NavMeshSurface.BuildNavMesh()` → 미션 시작.
- **한계 / 사실**:
  - ❗ **시드 없음** — `Random.Range` 직접 사용 → 같은 맵 재현 불가. (결정성 필요: `Random.InitState(seed)`)
  - ❗ **선형 복도(corridor) 구조** — "아주 넓은 맵"·스티칭·동굴엔 그대로는 부적합.
  - ❗ **적이 LevelPart 프리팹 안에 자식으로 박혀 있음** (`MyEnemies() = GetComponentsInChildren<Enemy>`).
    생성 후 부모만 떼고 활성화. → **스폰이 데이터가 아니라 프리팹에 하드코딩**됨. (당신이 바꾸려는 핵심)
  - 맵 파트가 데이터(SO)가 아니라 **씬/프리팹 Transform 리스트**로 관리됨.

### 1.2 "모든 게 SampleScene에 박혀 있음" (당신 지적 #2)
- 현재 `SampleScene` 한 씬에 맵·플레이어·적·차량·미션·UI·포그·카메라가 **전부 혼재**.
- 목표: **씬 1개 = 시드로 배치된 맵 1개만**. 맵 외 요소(스포너·게임플레이)는 데이터/별도 구성으로.

### 1.3 적 / 보스 / 엄폐 — 충분히 만들어져 있음 ✅
- **근접**: `Enemy_Melee` + Regular / Shield / Dodge / Axe-throw 변형. FSM(Idle/Move/Chase/Attack/Recovery/Ability/Dead).
- **원거리**: `Enemy_Range` + Advance / Advance+Grenade / **Cover+Grenade** / Sniper / Unstoppable 변형.
  FSM(Idle/Move/Battle/RunToCover/Advance/ThrowGrenade/Dead).
- **보스 2종**: `Enemy_Boss_Hummer`, `Enemy_Boss_Flamethrower`. FSM(Idle/Move/Attack/JumpAttack/Ability/Dead) + 화염방사 데미지존.
- **엄폐 시스템**(`Enemy/CoverSystem/`): `Cover`가 런타임에 4방향 `CoverPoint`(front/back/left/right) 생성 →
  `Enemy_Range`가 유효 엄폐점 선택(플레이어 최원거리·등뒤 금지·근접 금지·점유/직전엄폐 회피).
  → **맵에 `Cover` 오브젝트가 배치돼 있어야 작동**. 절차 생성이 엄폐물을 깔아줘야 함(당신 요청).

### 1.4 전투 / 카메라 / 매니저
- `Player_*`(Movement/AimController/WeaponController/Health/Interaction), `Weapon`/`WeaponModel`.
- 카메라: ✅ Cinemachine 3 마이그레이션 완료(`CinemachineCamera`+`CinemachinePositionComposer`, `CameraManager`).
- 매니저: `GameManager`, `MissionManager`(EnemyHunt/KeyFind/LastDefence/CarDelivery/Timer), `ObjectPool`,
  `LevelGenerator`, `AudioManager`, `ControlsManager`(Input System), `TimeManager`(슬로모).

### 1.5 오디오 — 거의 비어 있음 (당신 지적: 밋밋함)
- `AudioManager`: **AudioSource 배열 BGM** + `PlaySFX(AudioSource, randomPitch)` + 페이드. `Start()`에서 `PlayBGM(3)`.
- 보유 에셋: `Assets/Audio/` 에 **mp3 5개뿐** — `Melee_Impact`, `Melee_Swoosh`, `carEngine_start/works/off`.
- ❗ **총소리·레이저·피격·사망·발소리·UI·BGM 음원이 없음.** SFX가 "씬의 AudioSource 참조" 방식이라 확장도 번거로움.

### 1.6 트레일(걸을 때 바닥 자국) — 당신 지적
- Player 스크립트에 `TrailRenderer` 코드 **없음** → **씬/프리팹의 Player 자식에 붙은 `TrailRenderer` 컴포넌트**로 추정.
  (작업 시 Player 하위에서 찾아 머티리얼/시간/폭/정렬 조정 또는 제거·교체.)

---

## 2. 목표 정리 (당신이 말한 것 — 우선순위화)

### P0 — 최우선: 맵/씬 분리 + 데이터 기반 절차 생성 + 분리된 스포너 (지적 #1~#4)
1. 맵을 **데이터들로 조합**해 **절차적으로 "맵만"** 생성.
2. **씬 1개 = 특정 시드로 배치된 맵 1개**. 씬에는 **맵 정보만**, 나머지는 최소화.
3. **몬스터 스포너를 맵과 분리해 별도 배치/데이터화** → 다양한 스폰 데이터로 스폰.
4. **엄폐 지원 맵** — 원거리 적 엄폐가 작동하도록 절차 생성이 엄폐물/엄폐점을 배치.

### P1 — 체감 개선 (병렬 가능, 저위험)
- **사운드 패스**: 총소리/레이저/피격/사망/발소리/UI. (수단은 §5 사운드 항목 — 합성 or 무료에셋 임포트)
- **트레일 수정**: 바닥 자국 자연스럽게(머티리얼/페이드/폭) 또는 제거.
- 몬스터/보스 점검 ✅ (위 1.3 — 충분함 확인).

### P2 — 미래(이 문서에 기록, 지금은 설계만)
- **생존 기능**(서바이벌 루프).
- **아주 넓은 맵**: 절차 맵을 **계속 이어붙여** 확장.
- **동굴**: 진입 시 **새 맵(씬?) 전환**, 나오면 복귀.
- **수송선 탈출**: 마지막에 수송선 호출 → 탈출 시 **획득 아이템을 "집"으로 반출**(메타 저장).

### P3 — 추후(보류)
- 인벤토리 시스템 · 파밍 시스템 · interaction 아이템 확장(현재 `Interactable`/`Player_Interaction` 존재 → 확장).
- **차량/운전 시스템 재통합** — `Car_Controller`/`Car_Interaction`/`Car_Sounds`/`Car_HealthController` **이미 구현됨**(SampleScene 기준). 새 맵/스폰/부트 구조에 맞춰 재배선 필요(맵에 차량 배치, 탑승 시 `ControlsManager.SwitchToCarControls` 등). **지금은 보류 — 기록만.**

---

## 3. SpawntableGenerator에서 가져올 개념 → TDS 적용 매핑

| 개념(원본) | 원본 정의 | TDS 적용 방안 |
|---|---|---|
| `MonsterDef` (SO) | 몬스터 정체성+스탯+태그 | 기존 **Enemy 프리팹 변형 12종을 `MonsterDef` SO로 래핑**(prefab 참조 + cost/weight/tags). 새 적 AI 안 만듦 — 기존 FSM 재사용 |
| `SpawnEntry` | {monster, weight, cost, groupSize, minDifficulty, tags} | 동일 구조 SO. cost로 난이도 예산 스케일 |
| `SpawnTable` (SO) | SpawnEntry[] + 선택모드 | 상황/테마/난이도별 **여러 테이블**. 근접·원거리·엄폐형·보스 혼합 |
| `SpawnDirector` | 긴장도·예산 연속 스폰 | TDS는 **배치형 스포너 우선**(아래) + 선택적 전역 디렉터 |
| off-screen 스폰 | 카메라 절두체 밖 후보점 | 증원/웨이브에 재사용 가능 |
| 엄폐 연계 | navmesh 제외 벽 | TDS는 이미 `Cover`/`CoverPoint` 존재 → 절차 생성이 Cover 배치 |

> **핵심 차이**: 당신은 "**스포너를 따로 배치**"라고 했으니, 1차는 **맵에 배치하는 Spawner 컴포넌트(각자 SpawnTable 보유)** 모델로
> 가고, SpawnDirector식 전역 예산/긴장도는 P2 생존 루프에서 얹는 걸 추천.

---

## 4. 단계별 로드맵 (Phase)

### Phase A — 씬/맵 디커플링 + 시드 결정성  📋 (P0의 뼈대)
- `MapGenerator`(신규 or `LevelGenerator` 리팩터)에 **seed 필드** + `Random.InitState(seed)`.
- 맵 생성 결과물 = **맵 전용 루트** 하나. 적/스포너/플레이어와 분리.
- "맵만 있는 씬" 생성 흐름 확립(플레이어/매니저는 부트스트랩 또는 별도 씬에서 주입).

### Phase B — 데이터 기반 맵 조합 + 엄폐 배치  📋
- **맵 파트 카탈로그 SO**(파트 프리팹 + 태그 + 가중치 + Enter/Exit 메타). Transform 리스트 → 데이터로.
- 절차 생성이 **엄폐물(Cover)·엄폐 가능 지형**을 규칙적으로 배치(원거리 적 대응).
- (방향 결정 필요: 선형 체인 유지 확장 vs 그리드/구역형 — §5 Q1.)

### Phase C — 스포너 / 스폰테이블 시스템  📋
- 적을 **LevelPart 프리팹에서 분리** → `MonsterDef`/`SpawnTable` 데이터 + **배치형 `MonsterSpawner`**.
- 스포너가 시드/난이도/예산으로 테이블에서 몬스터를 뽑아 스폰(기존 `ObjectPool` 재사용).
- 보스 스폰 엔트리 포함.

### Phase D — 사운드 패스 + 트레일 수정  📋 (P1, A~C와 병렬 가능)
- AudioManager를 **clip+key 기반 SFX**로 확장(또는 래퍼 추가). 발사/피격/사망/발소리/UI 훅 연결.
- 음원: ① C# 절차 합성 placeholder(즉시) 또는 ② 무료 에셋 임포트 후 배선(§5).
- 트레일: Player 하위 `TrailRenderer` 조정/제거.

### Phase E+ — 미래 (P2/P3, 설계만)
- 생존 루프 → 광역 스티칭 → 동굴 전환 → 수송선 탈출/반출 → 인벤토리/파밍.

---

## 5. 결정 사항 (확정 — 2026-06-20)

- **D1. 맵 구조 = 그리드/구역형으로 전환** ✅. 광역 스티칭·동굴 연결·엄폐 배치 대비. 1차는 작은 그리드로 시작.
- **D2. 적 배치 = 스포너로 완전 분리** ✅. 적을 LevelPart에서 빼고 `MonsterDef`/`SpawnTable` + 배치형 `MonsterSpawner`로만 스폰.
- **D3. 시작 지점 = P0 맵/씬 디커플링부터** ✅.
- **D4. 사운드 = 보류** ⏸️. P0/맵 작업 우선. (Phase D는 뒤로.)
- **남은 결정(추후)**: 시드 지속성(씬 저자 vs 런타임 저장) — 미래 "반출" 메타 설계 시 확정.

### 진행 상태
- ✅ **Phase A** — 시드 기반 그리드 `MapGenerator` + "맵만 있는 씬" **완료·검증** (커밋 `cf61244`)
  - `Assets/Scripts/LevelGeneration/MapGenerator.cs` + `MapConfig.cs`(SO, `TDS/Map/Map Config`).
  - `Assets/Scenes/Map_Generated.unity` — 루트 3개만. 시드 7로 바닥+경계벽+장애물26+엄폐12, 중앙 스폰존 비움, **NavMesh 베이크 확인**.
  - 결정성: 전용 `System.Random(seed)`. 프리팹 비면 프리미티브 폴백.
- 🔧 **Phase 0** — **기존 코드 실용적 디커플링** (B/C보다 먼저 — 결정 D5) ← **현재 작업**
  - **0.1 (a·b·c) ✅ 완료** — 명세 5종 전부 green(Ignored 0). `SceneEntryPoint`로 `Map_Generated`가 **단독 자가 부팅**(EnsureSystems→Systems 영속→5서비스 등록, NullRef 0) 실증. 전역 매니저 5종 디커플링(Audio만 남음, 사운드 작업과 함께).
  - 0.1a ✅: `GameServices`·`SystemsEnsurer`·`GameBootstrap.EnsureSystems()`(Resources/Systems 프리팹, 멱등·DontDestroyOnLoad).
    `IClockService`(TimeManager)·`IMissionService`(MissionManager) 배선(자기등록, `.instance` 유지). **EditMode 11 + PlayMode 3 green.**
  - GameManager(`IGameStateService`)·ObjectPool(`IObjectPoolService`, `Start` null-guard로 단독 안전화) 배선 ✅ → Systems 프리팹 4종, PlayMode **4 서비스 green**. SampleScene 회귀 없음 확인.
  - ControlsManager(`IControlsService`, `Start` null-guard) 배선 ✅ → Systems 프리팹 **5종**, PlayMode **5 서비스 green**.
  - 0.1a 남음: **AudioManager** — `Start`가 bgm 소스에 의존 + 사운드 작업(Phase D, 보류)과 묶임 → 후속. 그 외 전역 매니저는 모두 배선됨.
  - **다음 큰 작업 = 0.2 컴포지션 루트**: ControlsManager/GameManager가 "플레이어/UI 준비 시" 실제 배선되도록(현재는 guard로 스킵). 맵 씬 진입점에서 `EnsureSystems()` 호출 연결.
- ✅ **Phase B — 맵 비주얼 + 엄폐 실작동** (2026-06-21):
  - **엄폐 배선** (`d87ace3`): `MapGenerator`가 엄폐물에 `Cover` 컴포넌트 + `CoverPoint` 마커 자동 부착. `Cover.cs` 회복력(플레이어 지연 조회). **Cover 변형 prefab의 coverPerk가 Unavalible였던 것**을 `CanTakeAndChangeCover`로 활성화(엄폐 시스템 코드는 있었으나 한 번도 안 켜짐). `Enemy.FaceTarget` 0벡터 가드, CoverPoint 디버그 렌더러 비활성. → 원거리 적이 실제로 엄폐로 달려가 사격(in-game 검증).
  - **사막 비주얼** (`7731074`): 회색 박스 → 임포트 사막 아트(`MapConfig_Default`). 장애물 풀(차/탱크/바위/돌/선인장), 엄폐=sea_container, 바닥=`Mat_DesertSand`. 프리팹 y=0 배치, 정적 MeshCollider convex=false. navmesh 정상 베이크(적 4/4 onNavMesh), 콘솔 클린.
- 🔧 **Phase C — 몬스터 스폰 (동작)**: `SpawnSelection`(순수 가중선택, EditMode 5 green) + `MonsterDef`/`SpawnTable` SO + `MonsterSpawner`(플레이어 생성 후 navmesh 링에 시드 스폰). 데이터 `MD_Melee`/`MD_Range`/`ST_Basic`. Map_Generated에서 **적 5마리 스폰 확인**(가중치 반영, navmesh 위, 크래시 0). `PlayerSpawner`가 인스턴스를 "Player"로 명명(적 AI `Find("Player")`), `Player.playerBody`=Bip001 Spine2 배선(Range 조준).
  - **전투 루프 검증 ✅**: `EnemyCombatTests` — 적 히트박스 `IDamagable.TakeDamage`(총알이 호출하는 경로)→`GetHit`→체력감소→**사망**. navmesh 베이크한 테스트 환경. `Enemy.Die`의 off-navmesh `agent.isStopped` 가드. (ST_Basic은 테스트 로드용으로 Resources 이동). **PlayMode 14 green.**
  - **in-game 전투 검증 ✅ (2026-06-21)**: Map_Generated Play → 총알 명중 시 적 사망+랙돌, 총알 임팩트 확인. 빗나감은 적이 접근해 옛 위치로 발사된 것(조준=마우스).
    - 🐛 **melee 공격 NullRef 수정** (`006dff9`): `Enemy_AnimationEvents.BeginMeleeAttackCheck`의 `enemy?.audioManager...`가 null `audioManager`(사운드 보류, 씬에 AudioManager 없음) 역참조. audioManager/meleeSFX 양쪽 가드(사운드 연결 시 자동 재생).
    - 🐛 **보스 화염 ParticleSystem Assert 수정** (`e4153b0`): `Enemy_Boss.ActivateFlamethrower`가 재생 중 `.duration` 설정 → 정지(StopEmittingAndClear) 후 설정.
  - **적 변형 다양화 ✅ (검증)**: 고유 변형 prefab 13종(근접5/원거리6/보스2) 이미 존재 + 8 MonsterDef가 올바른 변형 가리킴 + 5 테마 테이블. 13종 동시 난전 in-game → **NullRef 0**.
- ✅ **Phase C2 — SpawnDirector 다중 웨이브** (`3739ec5`): `WaveSequencer`(순수, EditMode 9) + `SpawnDirector`(TDS.Game 글루, PlayMode 1 통합). 전멸/타임아웃 진행. Map_Generated에 5웨이브(Basic4→MeleeRush5→RangedDefense5→Mixed6→Boss4) 배선. 씬 Play로 W1→W2 진행 검증. **EditMode 32 / PlayMode 15 green.** 긴장도(intensity)는 §6.1 추후.
- ⏸️ Phase D — 사운드 (보류) · 트레일 수정

### D5. 통합 전 모듈화 = 실용적 디커플링 ✅ (확정 2026-06-20)
기존 코드를 새 맵/스폰 시스템에 얹기 전에 **점진 디커플링**(게임 동작 유지하며). 풀 SOLID 재작성 아님.

---

## 7. Phase 0 — 실용적 디커플링 (상세)

### 7.1 왜 (결합도 스캔 결과)
| 지표 | 수치 |
|---|---|
| 전역 싱글톤(`public static instance`) | **9** — GameManager·UI·AudioManager·CameraManager·ControlsManager·MissionManager·TimeManager·ObjectPool·LevelGenerator |
| `.instance` 전역 참조 | **91곳 / 31파일** (UI.instance 11곳, GameManager 7곳…) |
| 런타임 씬 스크래핑(`FindObjectOfType`) | GameManager→Player, Cover→Player, 미션들→씬 오브젝트, UI→버튼 등 |

→ **맵 씬을 독립 실행 불가** (싱글톤·씬오브젝트 부재 시 NullRef). 새 시스템이 전역상태와 충돌. **선(先)디커플링 필요.**

### 7.2 단계 (각 단계 후 게임 동작 검증)
- **0.1 시스템 부트스트랩 = 영속 프리팹 + ensure-exists (키스톤, 결정 D6)**:
  전역 시스템을 **`Systems` 프리팹**(DontDestroyOnLoad)으로 만들고, **어느 씬에 진입하든** 엔트리포인트가
  "있으면 재사용 / 없으면 생성"(멱등)으로 보장. → **Boot 씬은 선택**이고, `Map_Generated` 등 아무 씬이나
  단독 Play 가능 → **씬별 단독 테스트 지원**(개발 진입점 자유). 미래 "동굴=맵 씬 교체"·"전리품 반출=시스템 영속"에 직결.
  - 트리거: (a) 씬마다 `SceneEntryPoint` 컴포넌트(명시적) 또는 **(b) `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 자동(권장)**.
  - **분리**: 전역 로직 매니저(GameManager·ObjectPool·TimeManager·ControlsManager·Audio·Mission)만 Systems 프리팹.
    **씬 종속 프레젠테이션(UI Canvas·카메라 Cinemachine)은 씬에 두고** 부트스트랩이 배선. (LevelGenerator는 맵 시스템으로 교체 예정.)
- **0.2 컴포지션 루트**: `GameBootstrap`이 `Systems` 프리팹 보장 + 레지스트리 노출 + 씬 종속물 배선(SpawntableGenerator `HudBootstrap` 패턴).
- **0.3 서비스 접근 시임**: `.instance` 직접호출 → `ServiceRegistry`/인터페이스 경유로 점진 교체(테스트·교체 가능). 싱글톤은 호환 위해 당분간 유지.
- **0.4 맵/스폰 경계 디커플**: `MapGenerator.onGenerated` 구독으로 스포너·플레이어 배치(이미 이벤트 기반).

> **D6 (확정 2026-06-20)**: Boot 씬 강제 ❌ → **영속 Systems 프리팹 + ensure-exists 엔트리포인트** ✅.
> 이유: 각 씬 단독 진입/테스트 가능해야 함. 멱등 보장으로 중복 초기화 방지.

> **위험관리**: 0.1은 동작 게임을 건드리므로 작은 단위로 쪼개 매 단계 플레이검증(콘솔 0에러). 매니저 이동 전후로 커밋.

### 7.3 TDD 하니스 (✅ 구축 — 테스트=명세)
형제 프로젝트 워크플로우 도입. **테스트가 0.1a~c의 완료 기준(명세)이자 회귀 방지**.

| 어셈블리 | 경로 | 용도 |
|---|---|---|
| `TDS.Core` | `Assets/Scripts/Core/` | 테스트 가능한 디커플링 원시 코드(`ServiceRegistry`·`BootSequence`). autoReferenced → 기존 Assembly-CSharp이 사용 가능, 역참조는 불가(DIP 강제) |
| `TDS.Tests.EditMode` | `Assets/Tests/EditMode/` | 순수 로직 단위 테스트 |
| `TDS.Tests.PlayMode` | `Assets/Tests/PlayMode/` | 씬/부트 통합 테스트 |

- **현재 상태**: EditMode **11 green**(ServiceRegistry 4 + BootSequence 2 + GameServices 2 + SystemsEnsurer 3),
  PlayMode **3 green**(`ServiceRegistry_resolves_across_a_frame` + `Bootstrap_registers_global_services` ✅0.1a + `EnsureSystems_is_idempotent`) + **2 Ignored = 남은 명세**:
  - `Services_persist_after_map_scene_swap` → 0.1b
  - `Map_scene_runs_standalone_without_nullrefs` → 0.1c
- 구현하며 `[Ignore]`를 제거해 green으로 전환. 실행: MCP `run_tests(mode, assembly_names=["TDS.Tests.EditMode"])` 또는 Test Runner 창.

## 8. Phase 0.2 — Player 추출 + 부트 스폰 (분해, 각 시임 독립 TDD)

> 원칙(사용자): **각 조각이 독립적으로 동작 → 문제 나면 그 조각만 대응.** 빅뱅 추출 금지.
> Player 생태계 결합: `ControlsManager.instance`(✅Systems가 제공)·`UI.instance`(❌맵에 없음, OnEnable NullRef)·`Camera.main`+Cinemachine 리그(❌)·직렬참조 `aim`/`cameraTarget`/`aimLaser`/`aimLaserEnd`(❌씬 오브젝트).

- **0.2.1 순수 시임 ✅**: `AimRotation.FaceHorizontal`(0벡터 가드 → "Look rotation" 경고도 해결), `PlayerSpawnPoint.Resolve`. EditMode 5 green.
  - 후속: `Player_Movement.ApplyRotation`을 `AimRotation` 사용으로 교체(경고 제거, 작은 독립 변경).
- **0.2.2 Player 회복력**: Player/Player_AimController가 `UI.instance`·`Camera.main` 없이도 안 죽도록 guard(각 guard 독립, ControlsManager 패턴).
- **0.2.2/0.2.3 🔧 진행**: `Resources/Player.prefab` 생성(비파괴 스냅샷). **TDD 스폰 테스트 green**(`Player_prefab_spawns_without_nullrefs`) — 시스템만 있는 컨텍스트에서 **NullRef 0**.
  - 스폰 테스트가 드러낸 결합 3건을 격리 수정: `Player_FogController`(fogVolume null 가드)·`Player_AimController.GetMouseHitInfo`(Camera.main null 가드)·MagicaCloth 비활성(cape 잔재, 딜레이 합의).
  - 남음: 끊긴 외부참조 배선(`aim`=Aim_Target·`aimLaserEnd`=Aim_EndPoint·`cameraTarget`=CameraFollow_Target → 프리팹 내부화 or 스포너 배선), 카메라 리그, 무기 부여(walk-first는 후순위).
- **0.2.4 카메라 리그**: Cinemachine(Brain+vcam+CameraManager)을 맵 씬/시스템에 제공.
- **0.2.5 PlayerSpawner ✅**: `PlayerSpawner`(시스템 보장→중앙 스폰, Resources/Player 폴백). PlayMode 2 green(스폰·위치). Map_Generated에 배치 → **플레이어가 절차 맵 중앙에 스폰(스크린샷 확인, NullRef 0)**.
- **0.2.4 카메라 추적 + 입력 회복력 ✅**: `CameraFollow`(태그로 스폰 플레이어 자동 추적, 3/4 뷰) + `FollowPosition` 순수 시임(EditMode 2 green). Map_Generated 카메라 원근+추적.
  - **입력 크래시 가드 5건**: `EnablePrecisesAim`(CameraManager null)·`Player.OnEnable`(UI null ×2)·`UpdateWeaponUI`(UI null)·`Reload`/`ToggleWeaponMode`(currentWeapon null) → **조작 시 에러 0** (SampleScene 동작 보존).
  - **이동(WASD)은 `controlsEnabled` 게이트 없이 동작 → 걷기 가능**(스크린샷에서 카메라 추적 확인). MCP로 키입력 시뮬 불가 → 사용자가 Play+WASD로 최종 확인.
- **0.2.6 aim/무기 풀배선 ✅ — 빈 맵에서 사격 가능**: 프리팹에 `Aim_Target`/`Aim_EndPoint` 자식 배선, `UpdateCameraPosition` cameraTarget null 가드.
  `PlayerMapBootstrap`(Assembly-CSharp)가 스폰 플레이어에 **컨트롤 활성화 + 기본무기(Pistol+AutoRifle) 부여**(UI 없이). 검증: `FireSingleBullet` 호출 시 **총알 스폰(ObjectPool), NullRef 0**. controlsEnabled=True·weaponReady=True·currentWeapon=Pistol.
- 남음: 끊긴 `cameraTarget`(레거시, CameraFollow로 대체됨) 정리, 무기 UI/HUD(선택), 그 후 동굴/스티칭/미션/몬스터.
- **0.2.7 게임 코드 asmdef화 ✅ — Player 통합 테스트 가능**: 게임 스크립트 132개를 **`TDS.Game` asmdef**(Assets/Scripts/)로, `PlayerControls`를 **`TDS.Input` asmdef**(Assets/Input Manager/)로 묶음. 스크립트 GUID 불변 → 씬/프리팹 참조 보존. 컴파일 클린, SampleScene 정상 부팅.
  - `TDS.Tests.PlayMode`가 `TDS.Game` 참조 → **Player 통합 테스트 작성 가능**. `PlayerCombatTests`: ① 스폰 시 서브시스템(movement/weapon/aim/health/controls) 배선 ② 기본무기 장착 후 `FireSingleBullet`→총알 스폰. **PlayMode 9 green**.
  - `Player_AimController.UpdateAimVisuals` Camera.main null 가드 추가(테스트/카메라 없는 컨텍스트 안전).
- **IK**: 0.2.3에서 aim 리그 유지 시도. 포지션 잡기가 과하면 딜레이(탑다운이라 티 적음 — 사용자 동의).

---

## 6. 워크플로우 규칙
- 기능 추가/변경은 **이 문서에 먼저 기록** → 코드.
- 모호/충돌 시 **구현 전 확인**(추측으로 진행 금지).
- 큰 구조 변경은 작은 슬라이스로 쪼개 검증(플레이/콘솔 0에러)하며 진행.
- 기존 자산 최대 재사용(적 FSM·ObjectPool·Cover·Cinemachine3는 그대로 활용).
