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

> 다이어그램(스폰 파이프라인): `입력(난이도/시간/어그로) → 스폰 디렉터(언제·얼마나) → 스폰 테이블(무엇·가중치) → 스폰 지점(어디) → 군집 스포너(어떻게) → navmesh 활성 군집 → (개체수 피드백)`. 현재는 **스폰 테이블 + 스포너 + 웨이브 디렉터(클리어/타임아웃) + 군집 분대 스폰(Squad) + 시야/소음 인지(PerceptionFsm)** 까지 구현(§6.3.1), **긴장도(intensity) 페이싱** 은 §6 추후.

---

## 3. 맵 생성 (TDS.Game)

- **`MapGenerator`** + **`MapConfig`**(SO): 시드 결정적 그리드. 바닥/경계벽/장애물/엄폐물 + NavMesh 베이크. 프리팹 비면 프리미티브 폴백. 전용 `System.Random(seed)`(전역 Random 비오염).
- **대형 맵 1024×1024 (2026-06-24)**: `MapConfig_Default` = grid 256×256 × cellSize 4 = **1024 월드**. 깔끔한 성능을 위해:
  - **장애물 카운트 상한**: `obstacleCount`(>0이면 셀별 확률 대신 그 수만큼만, 같은 셀 중복 방지). 셀 순회 폭발(256²×density ≈ 수천 객체) 방지. 현재 장애물 500 + cover 60 + 배럴 30 = ~595 객체.
  - **NavMesh = 콜라이더 베이크**: `NavMeshSurface.useGeometry=PhysicsColliders`. 정적배칭 결합 렌더메시는 Read/Write가 꺼져 **빌드에서 런타임 베이크 실패**(`does not allow read access`) → 콜라이더로 베이크해 빌드 안전. 적 18/18 navmesh 위.
  - **바닥 타일링**: `floorTileWorldUnits`(예 8) — 큰 바닥에서 텍스처 늘어남 방지(머티리얼 인스턴스에만).
  - **복잡도(2026-06-24)**: 균일 산포 + **장애물 군집**(`clusterCount`×`clusterSize`/`clusterRadius`, 밀집 포켓) + **내부 벽**(`interiorWallCount`/`interiorWallLength`, 초크포인트). 시드 결정적.
  - **주변만 렌더링(거리 컬링, 2026-06-24)**: `cullRadius`(기본 70) — `MapGenerator.Update`가 0.4s마다 플레이어 반경 밖 맵 오브젝트를 비활성(바닥 제외, navmesh는 베이크라 무관). 밀도 올려도 렌더+물리 저렴. in-game: 919개 중 7개만 활성, 130fps.
  - **실측(에디터 플레이)**: 런타임 ~95~130fps, draw call 50 / batches 49(사막 props 머티리얼 공유로 정적배칭), tris ~95k. 생성+베이크 일회성 ~2.2~2.9s.
- **데이터 기반 콘텐츠** (`Assets/GameData/Map/MapConfig_Default`): 사막 황무지 테마 — 바닥 머티리얼(`Mat_DesertSand`), 장애물 풀(부서진 차/연료탱크/콘크리트관/사막바위/돌/선인장), 엄폐물(`sea_container`). 임포트 프리팹은 바닥(y=0) 배치, 정적 MeshCollider는 `convex=false`(navmesh 카빙 + 충돌).
- **엄폐 실작동**: 배치된 엄폐물에 `Cover` 컴포넌트 + `CoverPoint` 4지점(오프셋 `coverPointOffset`로 풋프린트 밖, **NavMesh.SamplePosition으로 스냅 → 도달 가능한 지점만 생성**, 못 닿아 비비는 버그 방지). 원거리 적(coverPerk)이 `OverlapSphere`로 찾아 엄폐 → §4 적 AI와 연결. CoverPoint 마커 렌더러는 비활성(디버그용).
- **Cover 높이 가중치** (기획 2026-06-21): `Cover.CoverHeight`(렌더러 bounds) 기준 — **낮은 단상(≤0.8)=사격 가능(`IsShootable`, 교전 시 선호), 높은 것=은폐 전용**. 순수 `CoverEvaluation`(ShootFrom/HideOnly/Unusable). 교전 중 `Enemy_Range.AttemptToFindCover`는 **사격 가능한 cover만** 수집. 맵 생성은 낮은 단상(`lowCoverRatio`, 높이 ≤0.8)+높은 cover를 섞음. `Cover.AuditForRange()`로 검증(PlayMode `CoverAuditTests`).
- **폭발물 `Explosive` (2026-06-24)**: 배럴 등 `Breakable`이 부서지는 순간(`Breakable.Broken` 이벤트) 폭발 — `Physics.OverlapSphere`로 범위 내 `IDamagable`에 `ExplosionModel.DamageAt`(거리 falloff, 순수) 피해 + `NoisePing.EmitExplosion`(§6.2.1, 90m, 발생자=플레이어) + FX. 범위 피해가 옆 배럴 Breakable을 깨 **자연 연쇄**. `MapGenerator`가 배럴에 부착(radius 6 / maxDamage 80 기본).
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
- **적/낮은 prop 타고 솟구침 방지 (2026-06-25)**: ① `Player.Awake`가 `Physics.IgnoreLayerCollision(Player, Enemy)` — 적 래그돌 콜라이더를 안 탐(총알=Bullet 레이어·근접=오버랩이라 영향 없음). ② `Player_Movement`의 **Y-lock** — 평면 탑다운이라 이동 후 위로 오른 수직분을 되돌림(중력 하강 허용, 점프 없음) + `stepOffset` 0.1. CC 둥근 바닥이 낮은 장애물(맵 최저 ~0.38)을 굴러 넘던 것 차단.
- **이동 중 사격 페널티 (2026-06-25)**: 순수 `MovingSpread`(`TDS.Core`, EditMode 7) — 이동하며 쏘면 ① 이동속도 감소 ② 탄퍼짐 증가(정조준하려면 멈춰야 함). 글루: `Player_WeaponController.FireSingleBullet`이 `MovingSpread.SpreadMultiplier(현재속도, runSpeed, movingSpreadPenalty=2)`로 `Weapon.ApplySpread(dir, mult)` 호출(전속 이동=기본×3 탄퍼짐) · `Player_Movement`가 `MoveSpeedFactor(IsShooting, shootingMoveFactor=0.5)`로 사격 중 감속. `CurrentPlanarSpeed`/`MaxSpeed`/`IsShooting()` 노출.
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

#### 6.2.1 소음 테이블 — 데이터 기반 (재설계 2026-06-25)
> **수치 = 최소 가청 거리(m).** 동시에 여러 소리가 들리면 **가장 큰 소리(loudness 최대)가 이긴다** →
> 발포음(35)이 피격음(9)을 이겨, "발포음 들리는데 피격음 따라가던" 문제 해결.
> **플레이어가 내는 소리만 적이 반응** — 적끼리는 소리에 반응 안 함(적 무기/총알은 발신 안 함).

| 소리 | loudness(가청 m) | 조사 위치(revealsSource) | 발신 |
|---|---|---|---|
| 발포음 `Gunshot` | 35 | **발생자=플레이어**(총구가 플레이어에) | `Player_WeaponController` 발사 시 |
| 피격음 `BulletImpact` | 9 | 박힌 위치(플레이어 안 알림) | `Bullet`이 비-적(땅/벽) 충돌 시(플레이어 총알만) |
| 폭발음 `Explosion`(유탄) | 90 | **발생자=플레이어**(던진 사람) — 폭발은 사실상 플레이어가 자기 위치를 광역 광고 | 미구현(유탄 추가 시) |
| 발소리 `Footstep` | 8 | 플레이어 | 미구현(추후) |
| 재장전 `Reload` | 12 | 플레이어 | 미구현(추후) |

- **`NoiseCatalog`**(`TDS.Core`): 위 테이블(`Profile(type)→{loudness, revealsSource}`). 게임플레이 상수 — 추후 SO로 빼도 됨.
- **`NoiseModel.Resolve`**(순수, EditMode): `NoiseReading[]`(종류·거리·나이·소음위치·발생자위치) → 들리는 것 중 **loudness 최대** 선택 → revealsSource면 발생자(플레이어), 아니면 소음 위치 반환. `Heard`(거리≤loudness + 최근)로 가청 판정.
- **`NoisePing`**(전역, 플레이어 전용): 종류별 최근 1건(`Emit(type,noisePos,sourcePos)` + `EmitGunshot`/`EmitImpact`/`EmitExplosion`…). `ActiveChannels`로 적이 읽음. **폭발음은 발신 위치(폭발 지점)와 발생자(플레이어) 위치가 다름** → 폭발음 들은 적은 폭발 지점이 아니라 **플레이어**로 조사.
- **`Enemy.HeardNoise`**: `NoisePing.ActiveChannels` → `NoiseReading` 빌드 → `NoiseModel.Resolve`로 조사 위치 결정(가장 큰 소리). 솔로·분대 동일(분대 청각 부스트 제거 — loudness가 곧 사거리).
- **분대 그룹 조사 (2026-06-25)**: 분대원이 소음을 들으면 개별 수색 대신 **분대가 함께** 그 지점을 조사한다 — `Enemy.UpdateAggro`가 분대원이면 `Squad.OnMemberHeardNoise(pos)`를 호출(개별 `OnEnterAlert` 수색은 솔로만). `Squad`가 **앵커를 소음 지점으로 옮겨**(멤버가 대형으로 따라 이동) → **도착하면 `investigateDwell`(기본 4s) 동안 머물며 살펴봄** → 없으면 **순찰 복귀**(현재 위치에서 `patrolDir` 그대로). 도달 실패는 `investigateMaxTravel`(25s)로 포기. 교전(시야/피격)이 조사보다 우선. `Squad.Investigating` 프로퍼티.
  - **경계 중 새 소음 → 조사 지점 갱신**: 경계 상태에서도 새 소음이 들리면(`Enemy.UpdateAggro`의 `heard && Squad`) `OnMemberHeardNoise`가 `investigatePoint`를 최신 위치로 덮어쓴다(1m 이상 변할 때). 그래서 플레이어가 계속 쏘면 분대가 최신 총성 위치로 방향을 계속 따라간다.
  - **멤버가 갱신을 즉시 추종 (버그 수정 2026-06-25)**: `MoveState`는 진입 시 목적지를 1회만 잡아 앵커가 갱신돼도 멤버가 첫 목적지까지 다 가던 문제 → `MoveState_Melee/Range`가 **분대원일 때 이동 중 0.2s마다 `GetPatrolDestination`(대형 목표)으로 목적지 재설정**. 솔로는 제외(순찰 인덱스 부수효과).
- **검증**: EditMode `NoiseModelTests`(테이블 값·revealsSource·loudest 우선·폭발음=플레이어). PlayMode `SquadTests` — `Impact_noise_alone_makes_member_investigate`(근거리 impact→경계), `Impact_noise_is_close_range_only`(12m 피격음 무시), `Squad_member_hears_gunshot_within_table_radius`(30m 발포음 청취), `Distant_gunshot_is_ignored`(60m 발포음 무시), `Squad_targets_heard_noise_for_investigation`(앵커→소음), `Squad_members_follow_updated_investigate_target`(갱신 추종). (NoisePing은 static이라 `BuildSquad`에서 `ClearForTests()` — 테스트 간 오염 방지.)

### 6.3 3상태 FSM + 공용 칠판(blackboard)
```
순찰(패트롤 전진) --소음--> 경계(자극원 조사) --시야 확인--> 교전(추격+증원)
        <--조사 실패(타임아웃)--          <--시야 상실--
순찰 --직접 시야(근거리)--------------------------> 교전
```
- 멤버는 각자 콘으로 감지(분산 센서). 한 놈이라도 발각 → 공용 칠판 `knownPlayerPosition`에 기록 → **전원 교전**.
- 전원 시야 상실 + T초 경과 → 위치 낡음 → 경계(마지막 위치 수색) → 순찰 복귀.

#### 6.3.1 적 분대(Squad) — **구현 완료** (커밋 `fd2d7ce`·`bdc61cd`·`8cb0e21`·`e858475`)
개별 적의 시야/이동 AI(§6.2 `PerceptionFsm`)는 그대로 두고, **"교전 의식 공유" + "함께 로밍" 레이어만** 얹은 경량 구현(형제 프로젝트 `MonsterPack`의 앵커+공유 PackState 개념을 TDS 적별 perception에 맞게 축소).
- **`Squad`**(`Scripts/Enemy/Squad.cs`, MonoBehaviour 글루): `SpawnDirector`가 군집 스폰 시 분대원을 `Register`. 매 틱 — 멤버 중 한 명이라도 `SeesPlayer()` **또는** 최근 누군가 피격(`OnMemberHit` → `hitAlertDuration` 4s) → **전원 `SquadEngage()`**(시야 밖이라도 즉시 교전 + 시야상실 타이머 리셋). 트리거 사라지면 각 적이 제 lose-sight 타이머로 개별 이탈. `health<=0` 멤버는 `PruneDead`로 제거, 전멸 시 자기 파괴.
- **함께 로밍(앵커-추종)**: 비교전 시 분대가 공용 **앵커**를 천천히 전진(`patrolAdvance` 8). 멤버는 `Enemy.GetPatrolDestination`이 `TryGetPatrolPoint`로 받은 **앵커 주변 대형 위치**를 향함. 가장 뒤처진 멤버까지 모여야(낙오 방지) 앵커가 다음 칸으로 전진 → 뭉쳐 다님. 방향은 랜덤 ±35° 틀고, navmesh 밖이면 반대로.
- **`Enemy` 연동**: `Enemy.Squad` 프로퍼티(null=단독), `SquadEngage()`(`perception.ForceEngage()`+`EnterBattleMode`), `GetHit`에서 `Squad?.OnMemberHit()`, `GetPatrolDestination`이 분대 대형점 우선. 디버그 라벨에 `[분대]`.
- **의사결정 기즈모(`Squad.OnDrawGizmos`)**: 분대가 "무엇을 하려는지" 한눈에 — 의도색(청록=Patrolling/빨강=Engaging/노랑=Despawning) 앵커 구슬 + 전진 화살표(앵커→다음 전진점) + 멤버별 대형 목표점(황금각) 라인 + (로밍) 디스폰 경계 사각·앵커→플레이어 라인 + 의도 라벨. 그리는 결정은 순수 `SquadDecision`과 동일(보이는 결정=실제 결정). 게임뷰/씬뷰 Gizmos 토글로 플레이 중 표시.
- **순수 시임 `SquadDecision`**(`TDS.Core`, EditMode 5): 분대 의도 결정 — 교전 트리거 > (로밍·가장자리 복귀)디스폰 > 순찰. `Squad.Update`가 이 결과로 분기(교전/디스폰/순찰), 기즈모도 같은 함수로 색·라벨 결정.
- **순수 시임 `SquadFormation`**(`TDS.Core`, EditMode 9): 분대 대형 수학을 한 곳으로 모음(군집 스폰·순찰 대형이 같은 공식을 쓰던 중복 제거).
  - `SpiralOffset(index, count, radius)` — **황금각 나선** 분산(겹쳐쌓임 방지, 반경 단조증가·항상 radius 미만). `SpawnDirector` 군집 스폰 + `Squad` 순찰 대형이 공유.
  - `AllGathered(positions, anchor, radius, slack)` — 가장 뒤처진 멤버까지 (radius+slack) 안인가 → 앵커 전진 게이트.
- **맵 확장**: 다중 로밍 분대 수용 위해 `MapConfig_Default` 그리드 16×16→26×26(104×104 월드), 엄폐 20·배럴 10.

#### 6.3.2 상시 로밍 분대 디렉터 — **구현 완료 (2026-06-22, in-game 검증)**
> WAVE(클리어/타임아웃) 모델을 **대체**한다(결정 D7). 맵에 분대가 상시 흐르는 "방랑 순찰대" 페이싱.
- **흐름**: 디렉터가 목표 수(`maxSquads`)만큼 분대를 유지 → 새 분대는 **맵 가장자리에서 스폰** → **처음 정한 방향 그대로 직진 순찰**(플레이어 추적 안 함, 맵 끝/네브메시 밖에서만 반전) → 지나가다 발각/피격되면 §6.3.1 분대 교전 → **순찰 상태로 맵 반대편 가장자리에 닿으면 디스폰** → 디렉터가 빈자리를 새 가장자리 분대로 **리스폰**. (교전 중이면 가장자리여도 안 사라짐.)
- **확정 결정**: ① 스폰 페이싱 = **웨이브 대체, 상시 로밍**(2026-06-22). ② 로밍 방향 = **처음 방향 고정, 플레이어 추적 안 함**(2026-06-25 — 기존 플레이어-추적에서 변경). 경계→순찰 복귀 시 `patrolDir` 유지로 **가던 방향 계속**, 경계 동안 흩어졌으면 현재 중심으로 앵커 재설정해 그 자리에서 이어감. ③ 디스폰 = **순찰 상태 + 가장자리 도달**.
- **순수 시임 `SquadRoam`**(`TDS.Core`): 정사각 맵(center+halfExtent) 기준 디렉터 수학.
  - `EdgeSpawnPoint(center, halfExtent, perimeterT)` — 경계 둘레의 한 점(항상 경계 위, t 둘레비율로 래핑).
  - `NextPatrolDirection(currentDir, blocked)` — **방향 고정**: 안 막히면 그대로, 막히면 반전(`-dir`). (플레이어-추적 `AdvanceDirectionToward`를 대체.)
  - `IsAtEdge(centroid, center, halfExtent, margin)` — 분대 중심이 안쪽 사각 밖인가.
  - `ShouldDespawn(patrolling, atEdge)` = `patrolling && atEdge`. `SquadsToSpawn(current, max)` = 부족분.
- **글루(구현됨)**:
  - **`SpawnDirector` 모드** = `Waves`(기존) / `Roaming`(신규). Roaming이면 매 틱 `SquadsToSpawn`만큼 `roamSpawnInterval` 간격으로 1개씩 채움. 맵 bounds는 `MapGenerator.LastBounds`(미할당 시 자동 탐색)에서. 가장자리 스폰점 = `EdgeSpawnPoint(center, halfExtent-edgeInset, rng)` → `NavMesh.SamplePosition`. 웨이브·로밍이 분대 스폰 헬퍼 `SpawnSquadAt` 공유.
  - **`Squad.ConfigureRoaming(center, halfExtent, margin)`**: 로밍 켜기. `AdvancePatrol`이 `NextPatrolDirection`으로 **처음 방향 그대로 직진**(막히면 반전), 경계 등으로 앵커에서 `patrolAdvance*1.5` 이상 벗어나면 현재 중심으로 앵커 재설정(되돌아가지 않고 이어감). 비교전 + 가장자리(스폰 후 한 번 벗어난 뒤 `hasLeftEdge`) 도달 → `Despawn`(멤버 root + 분대 파괴).
  - **로밍 멤버 idle 단축**: 프리팹 기본 `idleTime`이 크면(예: 60s) idle↔move 순찰이 멈춰 앵커를 못 따라감 → 디렉터가 로밍 멤버 `idleTime`을 `roamIdleTime`(기본 1s)로 낮춤.
  - **HUD**: `SpawnDirector.IsRoaming`이면 `MapHUD`가 `WAVE x/y` 대신 `enemies: N`(=로밍 적 수) 표시, 승리 판정 없음(엔드리스). `Finished`는 Roaming에서 항상 false.
  - **씬 `Map_Generated`**: SpawnDirector `mode=Roaming`, `roamTable=ST_Mixed`, `maxSquads=3`, `mapGenerator` 배선.
  - **검증(in-game)**: 분대 3개가 가장자리(±90)에서 스폰→플레이어 쪽으로 거리 감소(100→89→72…)→강제 제거 시 3개로 리스폰, 콘솔 0 에러, HUD `enemies: N`. EditMode 189 green.
- **PlayMode 통합(구현됨)**: `SpawnDirectorTests.Roaming_director_keeps_squads_at_map_edge` — bounds 주입 후 roaming 디렉터가 maxSquads만큼 **가장자리에 스폰** + 전부 제거 시 **리스폰** 검증.
- **추후(선택)**: 긴장도(intensity) 연동(§6.1) · **디스폰 후 풀 반납(보류)** — 현재 `Destroy`. 적 풀링은 재사용 시 health/FSM/agent **리셋 패스**가 필요(Enemy에 OnEnable 리셋 없음 → 그냥 풀에서 꺼내면 죽은 상태로 부활). 안전한 enemy-reset 작업 후 별도 슬라이스로.

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

### 6.6 플레이어 시야 / 전장의 안개 (FoV) — **구현 완료, 현재 Off (셰이더 마스크, 2026-06-21)**
> 시야 콘+사거리 안 + 가려지지 않은 곳만 밝게, 나머지는 회색(맵은 보이되 적은 안 보임). 발사 시 밝아짐.
> **모드 선택** (`FovController`, 게임 시작 전 Inspector): **Off**(기본 — 시야 끔, 적 항상 보임) / **Realistic**(`VisionMaskCpu` — CPU 텍셀별, 사실적이나 느림) / **Fast**(`VisionMask` — GPU 폴리곤, 빠르나 일부 샘플링 이슈). 선택 모드만 Awake에서 동적 추가. fog 셰이더는 `_VisionMaskFlipY`(GPU RT는 D3D Y-flip=1, CPU Texture2D=0)로 공유. **현재 기본 Off**(가시성이 더 나아 끔; 추후 더 나은 FoV 에셋 검토). 콘이 너무 넓으면(특히 발사 Reveal +25°→190°) 플러드라이트처럼 됨 → 추후 튜닝.
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
