# TDS 테스트 하네스 (TDD 가이드)

> 이 문서는 **작업 전에 읽는 하네스 가이드**다. 새 기능은 여기 규칙대로 시임을 뽑고 테스트를 먼저(또는 함께) 짠다.
> 시스템 레퍼런스는 [Wiki.md](Wiki.md), 진행/결정 이력은 [Roadmap.md](Roadmap.md), 작업 루프는 [Workflow.md](Workflow.md).

---

## 1. 어셈블리 & 폴더

| asmdef | 위치 | 용도 |
|---|---|---|
| `TDS.Tests.EditMode` | `Assets/Tests/EditMode/` | **순수 로직** (유니티 객체/씬 없이 도는 것) |
| `TDS.Tests.PlayMode` | `Assets/Tests/PlayMode/` | **통합** (씬·프리팹·navmesh·입력 필요) |

PlayMode asmdef 참조: `TDS.Core`, `TDS.Game`, `TDS.Input`, `Unity.InputSystem`, `Unity.AI.Navigation`, TestRunner. EditMode는 `TDS.Core` + nunit.

## 2. 실행 (MCP)

```
run_tests(mode="EditMode", assembly_names=["TDS.Tests.EditMode"], include_failed_tests=true)
run_tests(mode="PlayMode", assembly_names=["TDS.Tests.PlayMode"], init_timeout=120000, include_failed_tests=true)
get_test_job(job_id, wait_timeout=60)   # 폴링
```
또는 에디터 Test Runner 창. **현재 baseline: EditMode 98 / PlayMode 29 green (총 127).**

> 새 테스트 파일을 추가하면 **`refresh_unity` 후 `editor/state`로 컴파일 완료 확인** → 그래야 Test Runner가 발견한다. (스크립트만 refresh로는 새 파일 import가 안 될 때가 있어 풀 refresh 권장.)

## 3. 핵심 규칙 — "시임 먼저"

**유니티 의존 로직은 순수 클래스로 뽑아 `TDS.Core`에 두고 EditMode로 테스트한다.** MonoBehaviour는 그 순수 시임을 호출하는 얇은 글루로 만든다.

- 예: 웨이브 진행 결정 = `WaveSequencer`(순수, EditMode 9) ← `SpawnDirector`(글루)가 호출.
- 예: 조준 회전 = `AimRotation.FaceHorizontal`(순수) ← `Player_Movement`가 호출.
- 글루(스폰·캔버스 생성·navmesh)는 PlayMode 통합 1~2개로 "실제로 도는지"만 확인.

## 4. PlayMode 통합 패턴 (이 프로젝트 고유)

- **부트:** `GameServices.ResetForTests(); GameBootstrap.EnsureSystems();` 로 시작, teardown에서 `GameBootstrap.Instance` 파괴 + `GameServices.ResetForTests()`.
- **navmesh 필요한 적 AI:** 테스트 바닥 Cube(40×40) + `NavMeshSurface.BuildNavMesh()` 베이크 후 적 스폰. teardown에서 surface 파괴. (`EnemyCombatTests` 참고.)
- **플레이어:** `Object.Instantiate(Resources.Load<GameObject>("Player"))` + `name="Player"`(적 AI가 `Find("Player")`). teardown에서 Player/Bullet 정리(풀 밖 총알 누수 주의).
- **private 필드/메서드:** 리플렉션. 직렬화 필드는 **GameObject를 비활성 생성 → AddComponent → 필드 set → SetActive(true)**(Awake가 값 읽음). 자동프로퍼티 백킹필드는 `"<name>k__BackingField"`.
- **입력 시뮬레이션은 피하고**, 상태를 직접 세팅하거나 메서드를 직접 호출해 검증.

## 5. 현재 커버리지 맵

✅ 커버됨: ServiceRegistry·GameServices·SystemsEnsurer·BootSequence·GameBootstrap·SceneEntryPoint·AimRotation·FollowPosition·PlayerSpawnPoint·SpawnSelection·WaveSequencer·GameOutcome(승패)·HitStop·CameraShake(전투 연출)·LocomotionAnim(이동 재생속도)·BattleMover(시야-회피·그레이스 회피)·**CameraZoom(휠 줌)**·Player(스폰/컨트롤/이동/무기/사격/피해)·Enemy(피해→사망·**사망 후 래그돌 고정**)·SpawnDirector(웨이브 진행)·MapGenerator(결정성/중앙비움/경계)·Cover(엄폐 획득)·ControlsManager.RecreateControls·CombatFeedback(처치 히트스톱)·Locomotion(anim 속도)·**BattleMove(근접/원거리 교전 이동)**.

## 6. 갭 백로그 (우선순위순) — 채우면 체크

- [x] **MapGenerator** — 시드 결정성·중앙 스폰존 비움·MapBounds. (`MapGeneratorTests`, PlayMode 4. play 모드 `DestroySafe`=지연 Destroy라 `Find("MapRoot")`가 옛 루트를 주는 함정 → private `mapRoot` 필드 리플렉션으로 최신 루트 읽음. `generateOnStart`는 끄고 명시 시드.)
- [x] **Cover 시스템** — 엄폐 perk 적이 Cover를 찾아 유효 지점 점유. (`CoverTests`, PlayMode 1. CoverPoint 마커는 런타임 생성, 변형 적은 `Resources/ST_RangedDefense`에서 로드.)
- [x] **GameOutcome (HUD 승패)** — `MapHUD` 판정을 순수 `GameOutcome.Evaluate`로 추출. (`GameOutcomeTests`, EditMode 7. 패배 우선.)
- [x] **ControlsManager.RecreateControls** — 호출 시 새 인스턴스로 교체. (`ControlsRecreateTests`, PlayMode 1.)
- [ ] (낮음) SpawnTable.Pick 직접, MonsterSpawner 스폰 수, CameraFollow 추적, Enemy.FaceTarget 0벡터 가드.

## 7. 새 테스트 추가 체크리스트

1. 유니티 의존 로직이면 → 순수 시임을 `TDS.Core`에 뽑는다.
2. 순수면 EditMode, 씬/프리팹/navmesh 필요하면 PlayMode.
3. 파일 작성 → `refresh_unity` → `editor/state`로 컴파일 확인 → `read_console`로 에러 0 확인.
4. `run_tests` → green 확인.
5. **기능이 바뀌었으면 [Wiki.md] + [Roadmap.md] 갱신** (이건 필수, [Workflow.md] 참고).
6. 커밋(내 변경만).
