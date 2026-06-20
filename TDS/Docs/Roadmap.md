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
- ✅ **Phase A** — 시드 기반 그리드 `MapGenerator` + "맵만 있는 씬" **완료·검증**
  - `Assets/Scripts/LevelGeneration/MapGenerator.cs` + `MapConfig.cs`(SO, `TDS/Map/Map Config`).
  - `Assets/Scenes/Map_Generated.unity` — 루트 3개만(Light·Camera·MapGenerator). 시드 7로 바닥+경계벽+장애물26+엄폐12, 중앙 스폰존 비움, **NavMesh 베이크 확인**(collectObjects=Children).
  - 결정성: 전용 `System.Random(seed)`로 모든 무작위 처리(전역 Random 비오염). 프리팹 비면 프리미티브 폴백.
  - 남은 다듬기→Phase B: 실제 프리팹 주입, recipe-only 저장 여부, 시드 결정성 EditMode 테스트.
- 🔧 Phase B — 맵 콘텐츠 카탈로그 SO(실제 프리팹) + `Cover` 컴포넌트 배선(엄폐 실작동)
- 📋 Phase C — `MonsterDef`/`SpawnTable`/`MonsterSpawner` 데이터 스폰
- ⏸️ Phase D — 사운드 (보류) · 트레일 수정(소슬라이스로 끼워넣기 가능)

---

## 6. 워크플로우 규칙
- 기능 추가/변경은 **이 문서에 먼저 기록** → 코드.
- 모호/충돌 시 **구현 전 확인**(추측으로 진행 금지).
- 큰 구조 변경은 작은 슬라이스로 쪼개 검증(플레이/콘솔 0에러)하며 진행.
- 기존 자산 최대 재사용(적 FSM·ObjectPool·Cover·Cinemachine3는 그대로 활용).
