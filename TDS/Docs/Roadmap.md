# TDS 개발 로드맵 & 현재 코드 지도 (계획 문서, Living Document)

탑다운 슈터(TDS)를 **데이터 기반 절차적 맵 + 분리된 몬스터 스폰 시스템**으로 재구성하고,
최종적으로 **생존 / 광역 맵 / 동굴 전환 / 수송선 탈출 + 전리품 반출 / 인벤토리·파밍**까지 확장한다.

> **룰**: 기획이 바뀌면 이 문서를 먼저 고치고 코드는 그다음. 충돌/모호하면 구현 전에 사용자에게 확인(추측 금지).
> 상태표기: ✅완료 · 🔧진행중 · 📋미착수 · ❌보류
>
> 참고 설계: 형제 프로젝트 `SpawntableGenerator/Docs/` (SpawnDirector·SpawnTable·MonsterDef·예산/긴장도 스폰).
> 이 프로젝트(TDS)에 **적용 가능한 개념만 골라** 이식한다(§3).

---

## 🗺️ 맵 발전 방향 (기획 2026-06-24, 사용자) — "지금 너무 휑함"

> 목표: 맵이 **충분히 복잡**하고 풍성하게. 아래 4갈래. 큰 것은 구현 전 합의.

1. ✅ **맵 복잡도 ↑ (군집/구조물)** (2026-06-24) — 균일 산포에 더해 **장애물 군집**(`clusterCount`×`clusterSize`, 반경 내 밀집 포켓) + **내부 벽 세그먼트**(`interiorWallCount`, 초크포인트). 시드 결정적. `MapConfig_Default`=군집 30×10 + 내부벽 25. (밀도는 `obstacleCount`/`cluster*`로 조절 — 컬링이 렌더 비용 잡아줌.)
2. ✅ **폭발물(=부서지는 것)** (2026-06-24) — 배럴이 부서질 때 **폭발**: 범위 피해(순수 `ExplosionModel.DamageAt` 거리 falloff, EditMode 6) + §6.2.1 `Explosion` 소음 90m(발생자=플레이어) + **자연 연쇄**(폭발 범위피해가 옆 배럴 Breakable을 깸). `Breakable.Broken` 이벤트 + `Explosive` 글루(`MapGenerator`가 배럴에 부착). PlayMode 2(falloff/노이즈, 연쇄). **EditMode 211 / PlayMode 53 green.** (추후: 폭발 FX 프리팹·플레이어 피해 밸런스.)
3. **🆕 절벽 지형 + 자연 텍스쳐 (기획 확정 2026-06-24)** — 굴곡이 아니라 **절벽으로 갇힌 맵** + 단조로운 사막 바닥을 **텍스쳐로 풍성하게**.
   - **걷는 영역은 절대 평평** (굴곡 X) → **기존 Y-lock 그대로 유지**(충돌 없음). 시각 복잡도는 **텍스쳐로만**(평평하되 자연스러운 지면 느낌).
   - **절벽 = 못 올라가는 가파른 지형**: **경계(테두리) + 내부 군데군데**(메사/협곡 같은 임패서블 구역). navmesh 경사 한도로 제외 → 엔티티 가둠/우회.
   - **절벽 위엔 돌** 장식, **나중에 나무**(숲 느낌).
   - **방식 = 메쉬 기반**(Unity Terrain은 런타임 비용 큼 → 평면 바닥 메시 유지 + **절벽 메시** 추가 + **바닥 머티리얼 텍스쳐 다양화**). Terrain heightmap/splat은 비용 때문에 지양, 필요 시 메시 베이크.
   - 구현 슬라이스: ① ✅ **바닥 텍스쳐 + 노이즈 블렌딩 (2026-06-25)** — 통짜 타일이 단조로워서 커스텀 셰이더 `TDS/DesertGroundBlend`(`Mat_DesertGroundBlend`)로 교체: 모래(Ground098) 풀디테일 + 월드 노이즈로 바위 패치(Rocks012)·명암 변주를 부드럽게 블렌딩(반복 티 제거, ~158fps). `[MainTexture]` 태그/`SetTextureScale("_BaseMap")`로 타일링 버그(1024 전체 stretch) 해결. `Mat_Cliff`(Rock029)/`Mat_RockProp`(Rocks012)도 생성. ② ✅ **절벽 (2026-06-25)** — 내부 절벽(메사) `interiorCliffCount`(메사=1~3 지터 블록, `cliffHeight`/`cliffMinFootprint`~`cliffMaxFootprint`) + `boundaryAsCliff`로 경계 벽을 절벽 높이/`cliffMaterial`로 올림. 높아서 CC/navmesh가 자동 제외 → 엔티티가 못 올라가고 우회(테스트 `CliffTests.Cliff_blocks_ground_path_forcing_detour`로 우회 강제 검증). 시드 결정적, cullable. ③ ✅ **바위 장식 (2026-06-25)** — `capCliffsWithRocks`로 절벽 위를 작은 바위로 덮고(탑다운서 윗면 보임), `rockPropCount`로 바닥에 작은 바위 산재(`Mat_RockProp`/Rocks012). 둘 다 **콜라이더 제거 = 순수 시각**(네브메시/이동 영향 0), 비균일 스케일+회전으로 각진 돌 느낌. ④ 📋 (추후) 나무.
4. ✅ **주변만 렌더링 (거리 컬링)** (2026-06-24) — `MapConfig.cullRadius`(기본 70). `MapGenerator.Update`가 0.4s마다 플레이어 반경 밖 맵 오브젝트를 `SetActive(false)`(렌더+물리 절약, 바닥 제외). navmesh는 베이크돼 있어 비활성해도 경로 영향 없음. in-game: 919개 중 7개만 활성(반경 안), 130fps. (프러스텀 컬링은 렌더만, 이건 물리까지 + 밀도 상향 여지. 대형 스티칭은 추후 청크 스트리밍.)

## 🌿 자율 보강 — 브랜치 `feature/map-feel-and-extras` (2026-06-25)

> 사용자 부재(8~9h) 중 "보강할 수 있는 것" 자율 작업 브랜치. **기획 방향(생존/탈출/파밍)을 바꾸는 큰 P2/P3 시스템은 손대지 않고**(룰: 큰 변경은 합의 후), 자족적·테스트 가능한 **항법 보조/연출/하드닝**만 추가. 다른 게임 리서치로 선택 근거 마련.
> 포함: ① 바닥 노이즈 블렌딩 셰이더 ② 절벽(경계+내부) ③ 바위 장식 (위 §맵 발전 1~3 슬라이스, 이 브랜치서 구현) ④ 적 레이더.

- ✅ **적 레이더/미니맵 (2026-06-25)** — 1024 대형 맵 항법 보조(리서치: 탑다운/추출 슈터 표준 = 코너 레이더, 플레이어 중심·주변 한정 시야, 적 핑). 순수 `MinimapProjection`(월드 XZ→플레이어 중심 북-업 픽셀, 범위 밖 가장자리 클램프+방향 유지, EditMode 7) + 글루 `Minimap`(TDS.Game, 자족 캔버스: 원형 레이더 우상단, 플레이어=초록 중심+진행방향 노즈, 적=빨강 블립, 범위 밖=반투명 가장자리). `worldRange`(90) 안의 적 표시, M 토글, 런타임 생성 원형 스프라이트(빌트인 Knob.psd 부재 대응). `Map_Generated` HUD에 부착. (추후 튜닝 여지: 적 가시/소음 게이트, 지형 표시.)
- ✅ **폭발 FX + 카메라 셰이크 (2026-06-25)** — 배럴 폭발 연출(§맵 발전 2의 "추후: 폭발 FX 프리팹" 처리). `Explosive.ExplosionFX`(public 주입) + `MapConfig.explosionFXPrefab`(`CFXR3 Fire Explosion B`) → `MapGenerator`가 배럴에 주입. 폭발 시 화염 FX 스폰(3s 후 정리) + **`ICombatFeedbackService.ReportExplosion`**(거리 강도 스케일 셰이크 + 아주 가까우면 미세 히트스톱, 자체 FX 있어 피격 FX는 생략). PlayMode `ExplosiveTests.Explosion_spawns_configured_fx`. in-game: 화염+연기 확인.
- ✅ **저체력 비네트 (2026-06-25)** — 생존 긴장감: 체력이 `startRatio`(35%) 이하로 떨어지면 화면 가장자리가 빨갛게 맥동. 순수 `HealthVignette`(`Intensity`(체력비율→0..1)·`Pulse`, EditMode 8) + 글루 `LowHealthVignette`(TDS.Game, 자족 캔버스: 런타임 생성 방사형 비네트 스프라이트, alpha=강도×펄스, 풀HP면 꺼짐). `Map_Generated` HUD 부착. in-game: HP 12%서 빨간 가장자리 확인.
- ✅ **절벽/바위 따뜻한 틴트 (2026-06-25)** — Rock029/Rocks012가 사막 대비 차가워서 `Mat_Cliff`/`Mat_RockProp` `_BaseColor`에 미세 웜 틴트 → 사암 느낌으로 팔레트 정합.

### 🚁 탈출 루프 (사용자 승인 2026-06-25) — "적 죽이면 떨구고, 주울 수 있고, 수송선 타면 탈출"
> P2 "수송선 탈출 + 전리품 반출"을 사용자가 명시 승인 → 구현 착수. 게임명(Escape From Desert Planet)의 핵심 루프.
- ✅ **전리품 드랍 + 픽업 (2026-06-25)** — 적 처치 시 전리품을 떨구고 플레이어가 주워 모은다. 순수 `LootWallet`(Carried/Bank/DropCarried)·`LootDrop`(ShouldDrop/Amount, EditMode 6) + 글루 `LootDropper`(적에 부착, `Enemy.Die`가 `DropLoot` 호출 — `SpawnDirector`가 스폰 시 부착·확률/수량 설정, 기본 70%/1~2)·`LootPickup`(프리미티브 금색 코인, **근접 반경 1.3m 자동 획득** — CC 트리거 물리 비의존)·`PlayerLoot`(플레이어 지갑, 런타임 부착). `MapHUD` "SALVAGE N" 표시. PlayMode 3(드랍/획득/중복방지). in-game: 처치→코인→근접 자동획득→HUD 증가.
- ✅ **수송선 탈출 존 = 승리 (2026-06-25)** — 맵 고정 위치(`extractionOffset` 비율, 기본 0.6,0.6) 수송선에 도달해 `extractionBoardTime`(4s) 머물면 탈출(승리) + 휴대 전리품 반출(Bank). 순수 `ExtractionProgress`(머문 시간 누적/완료) + `GameOutcome` 탈출 승리 인자(패배 우선 유지, 하위호환) EditMode 8 + 글루 `ExtractionZone`(근접 감지, 완료 시 `LootWallet.Bank`)·`MapGenerator.PlaceDropship`(프리미티브=시안 착륙패드+비콘, 콜라이더 없음=항법/시각만, 컬링 제외 랜드마크, `HasExtraction`/`ExtractionPosition` 노출)·`MapHUD`("EXTRACTING N%" + "EXTRACTED — N salvage" 승리)·`Minimap` 수송선 시안 블립(항상 표시, 가장자리 클램프로 방향 안내). PlayMode 2(탑승 탈출+반출 / 존 밖 미탈출). `MapConfig_Default.spawnExtraction=true`. in-game: 패드·비콘·레이더 블립·EXTRACTED 승리 확인.
  - **로밍 모드 승리 조건 확립**: 상시 로밍(웨이브 없음)은 클리어 승리가 없었음 → **탈출이 곧 승리**. 게임명대로 "수송선 타고 탈출".
  - 추후: 실제 수송선 3D 모델/이착륙 연출, 반출 메타 저장(집/영속), 탈출 중 적 압박(존 근처 스폰), resetOnLeave 긴장 옵션.
- 🐛 **버그 수정 3종 (2026-06-25, 사용자 리포트)**:
  - **수송선 파묻힘** → `PlaceDropship`를 맵 배치 맨 뒤로 옮기고 착륙 지점 반경(`extractionRadius+6`) 안의 맵 오브젝트를 `DestroyImmediate`(베이크 전)로 비움 + 프리팹 `SnapToGround`(렌더러 바운드 바닥을 y=0에 — 피벗 중앙이어도 안 파묻힘). 사용자가 `dropshipPrefab=airplane` 할당함(프리미티브 패드 대체).
  - **분대가 맵에 끼임/방향 못 따라감** → 근본 원인: navmesh 연결성 66%(영역이 절벽·고밀도로 분리돼 가장자리 스폰 분대가 앵커로 경로 못 찾음). **절벽이 주범**(91%→77%). + `MapConfig_Default` 격자가 32×32(128m)로 깨져 있었고 장애물은 그대로 고밀도여서 악화. 수정: 격자 256×256(1024) 복원 + 밀도 튜닝(장애물 800→600, 절벽 14→10 풋프린트 14→4~8, 내부벽 45→15) → **연결성 85%**(적 17/18가 플레이어로 경로 가능). 회귀 가드 PlayMode `NavMeshConnectivityTests`(연결성 >80% 단언).
  - **시드 변경해도 같은 맵** → 씬 `MapGenerator.seedOverride=100`이 `config.defaultSeed`를 무시하던 것 → `seedOverride=-1`로(이제 config.defaultSeed가 맵 결정).
  - **"incompatible keyword space" assert** (스택: `BuildFloor`의 `rend.material` + 렌더 컬링) → 커스텀 셰이더 `TDS/DesertGroundBlend`의 `FallBack "Universal Render Pipeline/Lit"`가 폴백과 **로컬 키워드 스페이스 충돌**을 일으킴. **폴백 제거 + ShadowCaster/DepthOnly/DepthNormals 패스를 셰이더에 직접 추가**(자족적, 포워드 렌더 확인 — 모든 렌더러 `m_RenderingMode:0`). assert 사라짐 + 바닥 그림자 캐스팅/뎁스·노멀 prepass 정상(볼류메트릭/시야 포그 뎁스에도 이로움). 블렌드 비주얼 그대로. **EditMode 240 / PlayMode 64 green.**

### 🚗 차량 재통합 + 모듈화 (사용자 승인 2026-06-25) — "기존 차량을 지금 시스템에서도"
> P3 "차량/운전 재통합". 핵심 = Car 시스템(이미 구현됨)을 **옛 SampleScene 싱글톤 4종에서 디커플링**해 현재 맵(태그 플레이어·MapHUD·CameraFollow·Systems ControlsManager, AudioManager 없음)에서 동작시키기. **모듈화 = 디커플링**. SampleScene 회귀 없음(전부 null-가드).
- ✅ **싱글톤 디커플링** — `UI.instance`(속도/차HP UI · Car_Controller/Car_HealthController), `GameManager.player`(Car_Interaction/ControlsManager → **태그 "Player" 해석**), `CameraManager`(Car_Interaction → **null-가드**; 탑승 시 플레이어를 차에 parent → `CameraFollow`가 차 자동 추적), `AudioManager`(Car_Sounds → null-가드). `ControlsManager.Switch*`는 태그로 플레이어 해석 + UI 가드. 프리팹 미할당 FX/오디오/트레일 가드(`fireFx`/`explosionFx`/`workingEngine`/`Car_Wheel.trail`).
- ✅ **데이터 기반 배치** — `MapConfig.carPrefabs` 풀 + `carCount`. `MapGenerator.PlaceCars`(베이크 후, `Physics.OverlapBox`로 빈 셀만 선택, `SnapToGround`+0.5 리프트, **스폰 즉시 `FreezeAll`**로 떨어짐/터널링 방지 — 주차=고정, 탑승 시 `ContinuousDynamic`로 주행). `Car_Controller.ActivateCar`가 비활성=FreezeAll/활성=None. `MapConfig_Default`=풀(PickUp/Jeep/Truck) 3대. cullable(주차 차량은 멀면 컬링, 주행 차는 플레이어 동승이라 안 컬링).
- ✅ **모듈화(순수 시임)** — 하차 지점 선택을 순수 `CarExit.PickClearPoint`로 추출(EditMode 4).
- ✅ **HUD** — `MapHUD`가 탑승 중 속도(km/h)+차 HP 표시(활성 차량 스로틀 스캔).
- **검증**: in-game(1024 맵) 탑승→주행(16m, 바닥 유지)→하차 전 흐름 **NullRef 0**, 카메라 차 추적, 차량 3대 정상 배치(고정). SampleScene 회귀 0(싱글톤 경로 보존). PlayMode `CarIntegrationTests`(맵 컨텍스트 컨트롤 전환 NullRef 0) + EditMode `CarExitTests`. **EditMode 244 / PlayMode 65 green.**
- ✅ **차량 폴리시 (2026-06-25, 사용자 피드백)** — ① 속도 ↑(prefab maxSpeed 7→14, motorForce/accel ↑, Range [7,30]) ② 피격 들썩임 수정(주행 중 `FreezeRotationX|Z`로 똑바로 유지) ③ **레이더에 차량 주황 블립**(`MapGenerator.CarTransforms` 노출 → `Minimap`, 컬링돼도 위치 유효) + `carCount` 5 ④ **차량/대형맵 FoV 수정**: `VisionMaskCpu`의 fog 쿼드가 고정 원점이라 멀리 가면 전맵이 보이던 것 → **fog 쿼드를 플레이어(차량 탑승 시 차) 따라 이동**(마스크는 이미 따라옴). in-game: fog가 플레이어 추종, 차량 블립 표시, maxSpeed 14, 똑바로 유지 확인.
- ✅ **차량 폭발 = 불 + 잔해 날림 (2026-06-25, 사용자 피드백)** — "폭발하면 불이라도 좀 나있어야". 프로젝트에 Chaos/프랙처 에셋 없음 → 진짜 파쇄는 보류, 대신: `Car_HealthController`에 **스폰형 FX 프리팹 필드**(`fireFxPrefab`/`explosionFxPrefab`, 프리팹 차일드 미할당 대응) 추가 → 부서질 때 차에 **CFXR Fire**(타오름) + 폭발 순간 **CFXR3 Fire Explosion B**. `maxHealth` 0→150(첫 피격에 즉사 방지). 폭발 시 **rb 구속 해제 + 질량 비례 임펄스**로 잔해를 날림(직립/고정 구속이면 안 날아감). 컬링된 차는 콜라이더도 비활성이라 실제 플레이서 피격 불가 → 폭발 코루틴 항상 활성서 시작. in-game: 불 타오름 확인(폭발 fuse는 에디터 sim-time 정지로 MCP 검증 불가 — 배럴 `Explosive`와 동일 배선).
  - 추후: **진짜 카오스/프랙처 파괴**(에셋 필요), 차량 HP/파괴 연출 밸런스, 엔진 사운드(사운드 패스와 함께), 차량 탑승 시 CC 경고(스케일 0.01 → step offset) 정리, 차량용 넓은 시야(현재는 일반 콘 추종).
- ✅ **차량 탑승 "한번에 안 탐" 수정 (2026-06-25, 사용자 피드백)** — 두 원인:
  - **(핵심) F 키 동시 바인딩 + 액션맵 미정리**: `Character.Interaction`(탑승)과 `Car.CarExit`(하차)가 **둘 다 `<Keyboard>/f`**. 맵 씬은 `Player.OnEnable`의 `controls.Enable()`이 모든 맵을 켜는데 **`SwitchToCharacterControls` 최초 호출이 없어** Car 맵이 도보 중에도 활성 → F 한 번에 `Interaction`(탑승)+`CarExit`(하차)가 같이 울려 보드 직후 즉시 하차. (SampleScene은 `ControlsManager.Start`가 SwitchToCharacter 호출 → 정상.) **수정**: `PlayerMapBootstrap`가 배선 시 `ControlsManager.SwitchToCharacterControls()` 호출 → 도보 스킴 확정(Car/UI 맵 off). in-game: 실제 F 입력 주입 1회로 탑승**유지**(carActive True·parent=Car), 다시 F로 하차 확인. PlayMode 회귀 `Control_schemes_are_mutually_exclusive_so_shared_F_key_is_safe`.
  - **(부수) 트리거 협소**: 상호작용 트리거가 차체와 거의 같은 크기라 플레이어가 차체에 막히는 지점이 트리거 경계와 일치 → 등록 불안정. 각 차 프리팹 루트 트리거를 **차체보다 ~1.1m 바깥으로 확장**(PickUp 2.55×4.62→4.75×6.82, 높이 2.2). 다가서면 안정적으로 `interactables`에 등록.
- ✅ **테스트 차 스폰 (2026-06-25, 사용자 편의 요청)** — `MapConfig.spawnCarNearPlayer`(기본 true) → `MapGenerator.PlaceCars`가 풀에서 랜덤 1대를 **플레이어 스폰(중앙 스폰존, 원점) 옆 반경 ~clearR-1.5(≈3.5m)**에 추가 배치(스폰존 안은 장애물 비워져 있어 안전, 차체 반길이 고려해 겹침 없음). 차 스폰 로직은 `SpawnCar` 헬퍼로 추출(랜덤/테스트 차 공용). in-game: 플레이어 원점·차 3.5m 옆 확인(스샷), 충돌·파괴 없음. 완료 후 Inspector에서 끄면 됨.

### 🎒 그리드 인벤토리 (사용자 승인 2026-06-25) — "타르코프/디아블로식 멀티셀, I키로 수납"
> P3 "인벤토리/파밍"을 사용자 명시 승인. 파밍한 루트를 **격자 인벤토리**(아이템이 W×H 셀 차지)에 수납. 사용자 지시: **md 추가 + 테스트 우선**(TDD) → 순수 로직+테스트 먼저, UI 글루는 다음 슬라이스.
- ✅ **순수 시임 (TDS.Core, EditMode 15)** — `InventoryItem`(Id/DisplayName/Width/Height 풋프린트) + `PlacedItem`(앵커 X,Y + 회전 → 유효 W/H swap) + `InventoryGrid`(W×H, `CanPlace`/`Place`/`TryAutoPlace`(첫 빈자리 행우선, 필요시 회전)/`Remove`/`ItemAt`/`IsOccupied`/`FreeCellCount`/`Clear`). 경계·겹침 거부, 회전 배치. `InventoryGridTests` 15(빈그리드/1×1·2×2 점유/경계·겹침 거부/제거 복원/자동배치·만석실패/회전 적합·자동회전/Covers/다중·Clear/생성자 검증).
- ✅ **글루 (TDS.Game, 2026-06-25) — I키 패널 + 루트 수납** — `PlayerInventory` MonoBehaviour(10×6 `InventoryGrid` 보유, `PlayerMapBootstrap`서 스폰 시 `Ensure` → 처음부터 I키 열람). **I키 토글 패널 UI**(코드 생성 캔버스, MapHUD 패턴; 격자 셀 + 아이템을 풋프린트만큼 색칠/라벨, 빈칸 카운트; 캔버스는 별도 루트라 차 탑승 스케일 영향 없음). **루트→수납**: `LootPickup.Collect`가 `PlayerInventory.TryStore(LootItem(value))` 호출(value→풋프린트: 1=Scrap 1×1·2=Parts 2×1·3+=Salvage 2×2), **가득 차면 픽업 보류**(지갑·제거 안 함). 지갑(SALVAGE)은 유지(탈출 카운트). PlayMode `InventoryGlueTests` 2(수납+지갑 / 만석 보류). in-game: 7개 수납 → 패널에 금/청/회 멀티셀 배치 확인. **EditMode 259 / PlayMode 67 green.**
  - 추후: 드래그/수동 배치·회전, 스택, 무게, 탈출 시 인벤토리 반출(메타 저장), 정렬/빠른수납, 아이콘.

## ⚠️ 빠른 시일 내 해결할 것 (최우선 — 사용자 지정 2026-06-26)

> 장르 = **익스트랙션 루터**(탈출시켜 전리품 반출). (현재 최우선 항목 없음 — 아래 해결 기록 참조.)

> ✅ **해결: 인벤토리 자유 배치 — 드래그/회전/스왑 (2026-06-30)**
> - 순수 `InventoryGrid`(grid-inventory 패키지)에 추가: `CanPlaceIgnoring`(자기/상대 셀 무시), `TryMove`(겹쳐 옮기기 허용·실패 시 불변), `TrySwap`(두 아이템 자리 교환, 각자 회전 유지, 안 맞으면 불변). 모두 변경 전 검사 → **실패 시 그리드/인스턴스 불변**. EditMode 8개 추가.
> - `PlayerInventory`(글루): 패널 열렸을 때 마우스 드래그 — `ScreenToCell`(RectTransformUtility로 화면→셀, 검증됨)로 커서 셀 계산, 좌클릭으로 아이템 집어 고스트(초록=가능/빨강=불가) 따라다님, **R로 회전**(비정사각형만), 놓으면 빈자리면 `TryMove`·다른 아이템 위면 `TrySwap`. 공개 `MoveItem`(테스트 훅, PlayMode 1). 패널 제목에 "drag to move · R rotate · I close".
> - 검증: 화면→셀 매핑 in-game 정상(셀(3,2) 왕복), 패널 렌더 확인. 마우스 드래그 손맛은 실플레이 확인 권장(harness가 멀티프레임 클릭 정밀 주입 어려움). 추후: 인벤토리 열린 동안 전투입력 비활성(클릭이 사격도 됨), 스택/무게/반출.

> ✅ **해결: 차량 전진 안 됨 + 속도 제어 (2026-06-29)**
> - **진단**: 탑승·입력(`moveInput=1`)·`motorForce`는 정상인데 차가 안 나갔다. `ApplyDrive`가 `motorTorque = motorForce × accel × Time.deltaTime`로 토크를 ~50배 작게 만든 게 1차 원인이라 키워봤더니, 이번엔 토크가 트랙션을 압도 → **앞바퀴 휠스핀**(rpm 115,668·forwardSlip 1.0)만 나고 무거운 차(트럭 790kg, 앞바퀴구동)는 제자리. WheelCollider 토크 방식은 차/질량마다 트랙션 튜닝이 필요해 취약.
> - **수정(아케이드 구동)**: 휠 모터 토크를 0으로 두고 **차체에 직접 `AddForce`(ForceMode.Acceleration)**로 추진 → 질량·트랙션 무관하게 확실히 전진/후진, `maxSpeed`로 상한, 후진 절반 속도, 스로틀 없으면 코스트다운. 조향도 **직접 yaw**(속도 비례, 후진 반대, 속도벡터 헤딩 정렬)로 확실히 회전. 휠은 굴러가며 시각/서스펜션만.
> - **진짜 근본 원인 (사용자 "여전히 안감" 재보고 후 발견)**: 아케이드 추진으로 바꿔도 **스폰 위치 그대로 타면 안 움직였다**. 빈 곳으로 옮겨 띄우면 13.9m/s로 잘 나감 → 차이는 **높이**. WheelCollider **서스펜션 스프링이 약해 무거운 차가 가라앉아** 차체가 바닥에 닿고 바퀴가 파묻혀(끌림) `AddForce`로도 못 움직였다(y=0.04, vel=0). +0.7 들어주면 즉시 13.9m/s. **수정**: `SetupDefaultValues`에서 서스펜션 스프링을 **질량/바퀴수 비례**(`carMass*150/wheelCount`)로 단단하게 + 댐퍼/forceAppPointDistance 보정 → 차가 바퀴 위로 떠 끌리지 않고 주행. in-game: 스폰 위치서 탑승 후 W → **71km/h(=maxSpeed)** 즉시 도달, 직립 안정, 콘솔 0.
> - **최종 강건화 (사용자 "WASD 눌러도 안 감" 또 재보고)**: 직접 Interaction 탑승 땐 주행됐는데 실제 F 탑승 땐 안 가는 경우가 있었다 — 가라앉아 차체가 끌리면 `AddForce`로도 안 움직임. → `ApplyDrive`를 **전진 속도 직접 설정**(`rb.linearVelocity`의 전진분을 `MoveTowards`로 목표까지)으로 바꿔 **바닥 끌림·질량·트랙션과 무관하게 항상 전진/후진**. 벽 충돌은 물리(ContinuousDynamic)가 막음. **검증(전체 실제 흐름)**: 옆에 서서 → interactables=1 → 실제 F 탑승 → 실제 W → **70km/h(=maxSpeed)** 주행 확인. `steerInput=+1`(D)→heading 0→94.7(우회전 정상). EditMode 259 / PlayMode 68 green. (손맛·회전속도는 `maxSpeed`/`accleerationSpeed`/`turnSensetivity`로 튜닝.)

> ✅ **해결: 플레이어가 적/낮은 prop 타고 Y축으로 솟구침 (2026-06-25)**
> - **적 끼임**: 적이 **Enemy 레이어 래그돌 본 콜라이더**(non-trigger)를 갖고 있어 플레이어 `CharacterController`가 타고 솟구침 → `Player.Awake`에서 **`Physics.IgnoreLayerCollision(Player, Enemy)`**(총알=Bullet 레이어·근접=오버랩이라 영향 없음).
> - **낮은 prop 타고 오름**: CC 둥근 바닥(radius 0.2)이 낮은 장애물(맵 최저 ~0.38)을 stepOffset과 무관하게 굴러 넘음. 평면 탑다운이라 수직 상승이 불필요 → `Player_Movement`에 **Y-lock**(이동 후 위로 오른 수직분 되돌림, 중력 하강은 허용) + stepOffset 0.1. 점프 없으니 안전.
> - PlayMode: `Player_ignores_collision_with_enemy_layer`, `Player_does_not_climb_enemy_layer_body`, `Player_does_not_climb_low_prop`(실제 이동 경로로 0.4 prop 비등반). in-game: 적 8마리 0.4m 겹침에도 playerY=0. **EditMode 205 / PlayMode 50 green.**

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
- ✅ **차량/운전 시스템 재통합 + 모듈화 (2026-06-25, 사용자)** — 기존 Car 시스템을 옛 싱글톤에서 디커플링해 현재 맵에서 동작. 자세히 아래.

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
- **D7. 스폰 페이싱 = WAVE → 상시 로밍 분대** ✅ (확정 2026-06-22). 웨이브(클리어/타임아웃)를 버리고, 분대가 맵 가장자리에서 스폰돼 플레이어 쪽으로 순찰하다 반대편 가장자리에서 디스폰·리스폰하는 상시 흐름. 시임/테스트 완료, 글루는 다음 슬라이스(§ 진행 상태의 "상시 로밍 분대 디렉터", [Wiki §6.3.2](Wiki.md)).
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
- ✅ **Phase D1 — 맵 HUD + 승리/패배 + 재시작** (`2d5a28c`, 2026-06-21): `MapHUD`(자족형, 캔버스/TMP 코드 생성) — 체력/탄약/웨이브 표시, 승리(전 웨이브)·패배(체력0) 종료 패널 + R 재시작. 씬 빌드세팅 등록. **재시작 노출 버그 수정**: 플레이어가 영속 `ControlsManager.controls`에 람다 구독만 하고 해제 안 해 리로드마다 죽은 콜백 누적 → `IControlsService.RecreateControls()`(PlayerSpawner가 재스폰 전 호출). 2회 리로드 MissingRef 0, 중복 0.
- ✅ **테스트 하네스 강화** (2026-06-21): 커버리지 감사 후 갭 충원 — `GameOutcome`(HUD 승패 순수 추출, EditMode 7), `MapGenerator`(결정성/중앙비움/경계, PlayMode 4), `Cover`(엄폐 획득, PlayMode 1), `ControlsManager.RecreateControls`(PlayMode 1). **EditMode 39 / PlayMode 21 green.** TDD 하네스 문서 [Testing.md](Testing.md) + 작업 루프 [Workflow.md](Workflow.md) 추가(작업 전 읽는 가이드).
- ✅ **전투 연출 심화(손맛)** (`3f72711`, 2026-06-21): 순수 시임 `HitStop`(타임스케일, EditMode 5) + `CameraShake`(trauma 모델, EditMode 6) → 글루 `CombatFeedback`(Systems, `ICombatFeedbackService`) + `CameraFollow` 셰이크 통합. 트리거: `Enemy.GetHit/Die`·`Player_Health.ReduceHealth`. 피격 FX=CFXR. 처치 시 히트스톱+강한 셰이크, 피격 시 약한 셰이크. **EditMode 50 / PlayMode 23 green.** in-game: 처치 시 trauma 0→0.42 + FX 스폰 확인. (sandrock convex 경고도 프리팹에서 근본 수정 `ad7ab32`.)
- ✅ **조준 크로스헤어 + 월드 레티클** (`7447ea7`, 2026-06-21): `AimReticle` — 시스템 커서 숨김 대체(마우스 스크린 크로스헤어) + 에임 타겟 바닥 링. in-game 확인.
- 🐛 **GameOver NullRef 수정** (`d4cfe55`): 플레이어 사망 시 `GameManager.GameOver`가 맵에 없는 `UI.instance`/`CameraManager.instance` 호출 → 가드. 사망 시 NullRef 0 + MapHUD DEFEATED 정상.
- ✅ **이동 애니 폴리시(제자리걸음 제거)** (`d4361c4`): 순수 `LocomotionAnim.PlaybackSpeed`(평면속도/기준속도→재생속도, EditMode 5) + `EnemyState.IsLocomotion` + `Enemy.Update` 구동. 이동 클립 재생속도가 실제 agent 속도 추종(정지 0.15, 풀런 ~1, 공격 1.0). PlayMode 1. in-game 확인.
- ✅ **BattleMover 1차 — 시야-회피 교전 이동** (`6d19f06`): 순수 `BattleMover`(FrontExposure·Score·PickEngagePosition, EditMode 9) + `ChaseState_Melee` 글루. in-game: melee가 플레이어 정면 회피 → 창발 포위(9/11 플랭크/뒤). PlayMode 1.
- ✅ **BattleMover 멜레 수정 + 원거리 적용** (`feature/enemy-engage-movement`: `13d981f`·`3f535ac`): 근접은 **공격 사거리까지 근접해 둘러싸 공격**(포위=근접), **최근 피격(그레이스) 시에만** 시야 회피 재배치. 원거리는 **피격 시 사거리 유지하며 strafe 재배치**(굳어있음 해소). `Enemy.LastTimeDamaged` + `BattleMover.ViewAvoidWeight`. `Player_Movement` controlsEnabled 가드(Move on inactive controller 수정).
- ✅ **죽은 적 고정** (`72b8f5d`): 사망 5s 뒤 `Ragdoll.Freeze`(끝없는 슬라이딩 방지).
- ✅ **마우스 휠 줌** (`e61a135`): `CameraZoom`(순수) + `CameraZoomInput`. 무기별 에임-방향 카메라 오프셋은 Wiki §6.7 추후.
- 📋 **main 병합 완료** (FF: 45커밋). 이후 작업은 `feature/enemy-engage-movement`.
- ✅ **🐛 총 팽글팽글 + 이상한 사격 수정** (`fefbb14`): 탑다운에서 무기 LookAt이 발밑 조준 시 거의 수직→up 모호성으로 회전 + BulletDirection 0/랜덤. `AimDirection.ResolveHorizontal`(수평+0벡터 가드)로 무기·총알 수평화 + 프리팹 `isAimingPrecisly` 기본 false. EditMode 9 + PlayMode 1, in-game 검증.
- ✅ **원거리 엄폐 행동** (`ffa7cdd`): 피격/발각 시 근처 coverPoint 우선→없으면 BattleMover strafe 폴백. 순수 `RangedEngageDecision`(EditMode 6) + `BattleState_Range` 통합(전투 중 `updateRotation=false`로 플레이어 보며 이동) + `coverPerk` 기본화(Range·Sniper) + `RunToCoverState` PathPartial 무한대기 버그 수정. in-game 검증(피격→엄폐 주행→사격).
- ✅ **원거리 사선뛰기 애니 (풀 strafe 블렌드, TDD)**: 엄폐 없는 BattleMover 재배치 중 적이 **플레이어 조준한 채 다리만 옆/대각 달림**. 순수 `StrafeBlend.Compute(velocity, facing)`(EditMode 10) + `BattleState_Range`가 `Strafing`/StrafeX/Y 구동 + `Enemy_Range.controller`에 2D 블렌드(`Strafe`, FreeformCartesian) 코드 구축. Pro Rifle Pack 방향 달리기 8종 Humanoid 재임포트(Mixamo→적 아바타 리타게팅, in-game 포즈 검증 OK). 상체 조준은 Rifle 레이어 유지.
- ✅ **🆕 기획 변경: Cover 높이 가중치 (2026-06-21, 구현 완료)** — BattleMover 2차 전 처리. 순수 `CoverEvaluation`(EditMode 7)+`CoverApproach`(EditMode 5) + cover point NavMesh 샘플(도달 보장) + RunToCover 견고화(비비기→BattleState) + range가 낮은 cover만 교전용으로 선택 + 맵 생성 낮은/높은 cover 혼합 + strafe 게이트 + 검증 툴 `CoverAuditTests`(PlayMode). in-game: 낮은 11/높은 3, 비비기 0.
  - **문제**: range 적이 cover에 제대로 못 닿고 그 자리에서 비빔(처음부터). 원인 = cover point가 도달 불가 위치(긴 container 안쪽/navmesh 밖) + RunToCover arrival이 grinding을 못 잡음. 또 cover가 전부 고층 container라 적이 그 위로 못 쏨.
  - **기획**: cover를 **높이로 분류**:
    - **낮은 cover(단상, 높이 ≤ 0.8)** = *shoot-from-cover*. 적이 뒤에 서서 그 위로 조준·사격. **교전 시 선호**(가중치 높음, 낮을수록 선호).
    - **높은 cover(container 등)** = *full-hide*. 적도 못 쏨. "공격도 안 하고 공격도 안 받고 싶을 때"(완전 은폐) 의도일 때만 선택.
    - **0.8 상한**: 땅에 붙은 단상 높이가 0.8을 넘으면 총구가 cover에 박혀 못 쏨 → 단상은 ≤0.8.
  - **구현(TDD)**: 순수 `CoverEvaluation`(높이·도달성 → ShootFrom/HideOnly/Unusable) + `CoverApproach`(arrival/stall로 비비기 방지) + cover point **NavMesh 샘플**(도달 보장) + RunToCover 견고화 + range가 낮은 cover 선호 + 맵 생성에 낮은 cover 배치 + **strafe 게이트**(이동 안 하면 제자리뛰기 금지).
  - **검증 툴**: 생성된 맵의 각 cover가 range에 적당한지(도달 가능 cover point + 높이 분류) 확인하는 PlayMode 테스트(`CoverAuditTests`).
- ✅ **BattleMover 2차** (strafe/backstep/flee 능력게이트 + 몹 간 소프트 간격): 순수 `EvasionPlanner`(EditMode 8) + `BattleMover.SpacingPenalty`(EditMode 4) + `Enemy.NearbyAllyPositions` 글루(멜레·원거리). 원거리 회피행동(체력/플래그 게이트), 멜레·원거리 겹침 완화(in-game 최소 쌍거리 7.7, 뭉침 없음). EditMode 122/PlayMode 30.
- 📋 그 다음(사용자 우선순위, 2026-06-21):
  1. **맵 오브젝트 용도/범위** —
     - ✅ **적 끼임 방지(안전망)**: 순수 `StuckTracker`(EditMode 5) + `Enemy.UpdateStuckRecovery` — 일정 시간(1.5s) 진전 없으면 가까운 navmesh 바닥으로 `agent.Warp` + 재경로. 원인(낮은 장애물 위 베이크/회피 교착) 불문. in-game: y=1.5로 올린 적을 y=0으로 복구. (참고: 낮은 cover/장애물은 cover 높이 작업 + navmesh 카빙으로 대부분 해소, 복구는 잔여 케이스 안전망.)
     - ✅ **오브젝트 종류 분류 (구현 완료)**: 순수 `MapObjectClassifier`(높이·속성→역할 플래그, EditMode 4) + `MapObjectRole`(Blocking/Cover/Hide/Breakable/Movable). `MapObject` 태그(Cover가 측정 높이로 자동 분류, 맵 생성이 장애물·배럴 태깅). **breakable**=`Breakable`(IDamagable, 순수 `BreakableHealth` EditMode 4, 누적 피해→파편+제거), **movable**=`Movable`(Rigidbody+NavMeshObstacle carving, Bullet이 밀기). 맵 생성에 배럴(movable+breakable) 추가. PlayMode 2(파괴/밀림). in-game: 배럴 +4.9 밀림 + 누적 45피해 파괴, 분류 Cover11/Hide3/Breakable6/Movable6.
       - (잔여) 배럴이 다른 오브젝트와 겹쳐 스폰 시 물리로 튕겨 정착(시작 시 살짝 떠올랐다 내려옴) — 배치 겹침 회피는 추후 폴리시.
  2. ✅ **적 패트롤 + AI 고도화 (인지 §6.2 + FSM §6.3 + 패트롤 스폰)** (`1c0b8f0`·`805e5c6`, 2026-06-21): 거리 기반 aggro → **시야/소음 인지**로 전환. 순수 `PerceptionFsm`(순찰→경계→교전, 히스테리시스+ForceEngage, EditMode 14) + `NoiseModel`(EditMode 5). `Enemy.SeesPlayer`=ViewCone+LoS레이캐스트+근접반경, `UpdateAggro`가 FSM 구동(Engage=교전/그외=이탈, Melee/Range 순찰복귀, **Boss는 거리 aggro 격리**). 발사 시 `NoisePing` 발신 → 적이 총성 듣고 조사. 경계 진입 시 마지막목격/소음 위치로 수색 이동(MoveState 재사용) 후 순찰 복귀. **버그 수정**: 교전 중 멈춘 agent(`isStopped`)가 이탈 후 안 풀려 얼던 것; ObjectPool/Bullet teardown 가드(풀 파괴 후 접근 MissingReference/KeyNotFound). in-game: 연사 시 교전 3→10 수렴, 조용하면 순찰; 플레이어 시야차단 시 이탈→마지막위치 수색→순찰 사이클 검증. EditMode 166/PlayMode 38. **추후(§6.6 대칭)**: 광원 적 벽뒤 가시, 낮/밤 콘 스케일, 인지 confidence(거리 게이트 점진).
  3. ✅ **FoV** (§6.6, 셰이더 마스크) — 시야 콘+사거리+눈높이 차폐로 적 숨김(`ViewCone`/`FieldOfView`) + 지면 fog 쿼드(`VisionMask`+`VisionFog` 셰이더)로 시야 밖 회색, 발사 시 확대. EditMode 9 + PlayMode 5, 마스크 텍셀·명도 검증. **추후(별도, §6.6)**: 광원 보유 적(횃불) 벽 뒤 가시, 낮/밤 콘 스케일, 플레이어 소리 인지, 적 인지(§6.2) 대칭 통합, 마스크 성능 최적화(프레임 분산).
  4. ✅ **적 분대(Squad) — 군집 스폰 + 공유 인지 + 함께 로밍** (`fd2d7ce`·`b9bab3a`·`bdc61cd`·`8cb0e21`·`e858475`, 2026-06-22): `SpawnDirector`가 군집(황금각 나선)으로 스폰 → `Squad`가 "교전 의식 공유"(한 명 발각/피격 시 전원 `SquadEngage`, hitAlert 4s) + "앵커-추종 함께 로밍"(낙오 없을 때만 앵커 전진)을 얹음. 개별 `PerceptionFsm`은 그대로(레이어만 추가). NavMesh/Quaternion 에러 플러드 수정(죽은/off-mesh agent의 perception churn), per-enemy perception gizmo, off-navmesh 적 recover. 다중 분대 위해 맵 64→104 확장. 자세히 §6.3.1.
     - **부채 정리(TDD/문서 백필)** (2026-06-22): 분대 작업이 doc-first+TDD 루프를 건너뛰어, 사후 보강. 대형/순찰 수학을 순수 `SquadFormation`(`SpiralOffset`·`SpiralPoint`·`AllGathered`)으로 추출 — `SpawnDirector` 군집 + `Squad` 순찰의 **중복 황금각 공식 제거**. EditMode 9 추가(총 **EditMode 175 green**). `Squad`/`SpawnDirector` 글루를 시임 호출로 교체. in-game 재검증(분대 1·적 4 스폰, 콘솔 0). Wiki §6.3.1 신설.
  5. ✅ **상시 로밍 분대 디렉터 (기획+구현 2026-06-22)** — WAVE 대체. 분대가 **맵 가장자리에서 스폰 → 플레이어 쪽으로 대략 전진(순찰) → 순찰 상태로 반대편 가장자리 도달 시 디스폰 → 새 가장자리에서 리스폰**(상시 `maxSquads` 유지). 자세히 [Wiki §6.3.2](Wiki.md).
     - **확정 결정(사용자)**: ① 웨이브 대체(상시 로밍) ② **첫 방향만 플레이어 쪽, 이후 방향 고정**(2026-06-25 정정 — 매 틱 재조정 X) ③ 디스폰 = 순찰 상태 + 가장자리 도달.
     - ✅ **순수 시임/테스트**: `SquadRoam`(`EdgeSpawnPoint`·`InitialPatrolDirection`·`NextPatrolDirection`·`IsAtEdge`·`ShouldDespawn`·`SquadsToSpawn`, EditMode 12).
     - ✅ **글루**: `SpawnDirector` `mode=Roaming`(가장자리 스폰+`maxSquads` 유지, `MapGenerator.LastBounds`) · `Squad.ConfigureRoaming`(첫 방향 플레이어 쪽→이후 고정 직진, 막히면 반전 + 순찰·가장자리 디스폰) · 로밍 멤버 `idleTime` 단축(프리팹 기본 60s라 순찰이 멈추던 것) · `MapHUD` 로밍 라벨(`enemies: N`) · 씬 `Map_Generated` 배선(ST_Mixed, maxSquads 3).
     - ✅ **in-game 검증**: 분대 3개 가장자리(~94)에서 스폰→플레이어 쪽으로 진입(d2p 94→~30→교전), 즉시 디스폰 없음, 강제 제거 시 리스폰, 콘솔 0.
     - ✅ **PlayMode 통합 (2026-06-22)**: `SpawnDirectorTests.Roaming_director_keeps_squads_at_map_edge`(bounds 주입 → maxSquads 가장자리 스폰 + 리스폰). **풀 반납은 보류** — 적 풀링은 재사용 시 health/FSM/agent 리셋 패스 필요(Enemy OnEnable 리셋 없음 → 죽은 상태 부활 위험), 별도 슬라이스로([Wiki §6.3.2](Wiki.md)).
     - ✅ **의사결정 기즈모 + 테스트 (2026-06-22)**: 순수 `SquadDecision.Resolve`(교전>디스폰>순찰, EditMode 5) → `Squad.Update`·`OnDrawGizmos` 공유. 기즈모 = 의도색 앵커/전진 화살표/대형 목표점/디스폰 경계/플레이어 라인/라벨. in-game(씬뷰) 검증: Patrolling=청록·Engaging=빨강 정확, 콘솔 0. **EditMode 194 green**.
  6. 🔧 **소음원 2종 — 총구음/피격음 (기획 2026-06-22)** — 순찰·경계 상태 적의 소리 반응을 2채널로. 총구음(발사 위치, 큼) 들리면 그쪽 우선 → 못 들었어도 **피격음(총알이 땅에 박힌 위치, 작음)** 들리면 그 근처로 가 플레이어 수색. 자세히 [Wiki §6.2.1](Wiki.md).
     - ✅ **순수 시임/테스트**: `NoiseModel.Investigate`(총구음>피격음 우선, target/kind 결정) + `NoiseKind`. EditMode 4.
     - ✅ **글루 (2026-06-22)**: `NoisePing` muzzle/impact 2채널(`EmitMuzzle`/`EmitImpact`) · `Player_WeaponController`가 발사 muzzle + `impactNoiseRadius` 전달 · `Bullet.EmitImpactNoise`(비-적 충돌, 플레이어 총알만) · `Enemy.HeardNoise`가 두 핑 → `Investigate`. PlayMode 2(`SquadTests`: impact만으로 경계 전환 / 먼 impact 무시).
  7. ✅ **로밍 순찰 방향 정정 + 분대 청각 (2026-06-25, 사용자)** — ① **순찰 방향**: 매 틱 플레이어 추적하던 것을 **첫 스폰 시에만 플레이어 쪽, 이후 고정 직진**으로 변경(가장자리 스폰 후 랜덤 방향이 벽에 박혀 즉시 디스폰되던 버그 수정). 순수 `InitialPatrolDirection`(첫 방향=대상 쪽)+`NextPatrolDirection`(고정, 막히면 반전), `Squad`가 경계 동안 흩어지면 앵커 재설정으로 가던 방향 이어감. ② **분대 청각 50m**: `Enemy.squadHearingRadius`(기본 50) — 분대원은 소음 크기와 무관하게 50m 안이면 무조건 들음(`HeardNoise`가 `max(반경,50)`). EditMode 5(Initial/Next/Hearing 등) + PlayMode 2(방향 고정·50m 청각). in-game: 분대가 가장자리→플레이어 쪽 진입 후 교전(즉시 디스폰 없음).
  8. ✅ **분대 그룹 소음 조사 (2026-06-25, 사용자)** — 경계 시 "확인 시간이 너무 짧아" 소리 난 곳에 도착하기도 전에 순찰 복귀하던 문제. 이제 분대원이 소음을 들으면 **분대가 함께 그 지점으로 이동(`Squad.OnMemberHeardNoise`→앵커 이동) → 도착해서 `investigateDwell`(4s) 동안 살펴봄 → 없으면 순찰 복귀(patrolDir 유지)**. 개별 수색은 솔로만(`Enemy.UpdateAggro`가 분대원은 분대로 라우팅). 도달 실패는 `investigateMaxTravel`(25s) 포기, 교전 우선. PlayMode `Squad_targets_heard_noise_for_investigation`(앵커→소음). in-game: 분대가 플레이어 총성(50m)을 듣고 그쪽으로 조사 이동.
     - 🐛 **이동 중 조사 지점 갱신 추종 (2026-06-25)**: 기즈모(앵커)는 갱신되는데 멤버는 첫 지점까지 가던 버그 — `MoveState`가 진입 시 목적지를 1회만 잡던 것. `MoveState_Melee/Range`가 분대원일 때 이동 중 0.2s마다 대형 목표로 재설정 → 갱신된 소음 위치를 즉시 추종. PlayMode `Squad_members_follow_updated_investigate_target`.
     - ✅ **피격음 청각 10m로 (2026-06-25, 사용자)**: 발포음은 분대 50m 유지, **피격음은 분대 부스트 미적용 → 발신 반경(`impactNoiseRadius=10`)만** (실탄=근거리). 폭발성 공격 등 큰 피격음은 추후. `Enemy.HeardNoise` impact는 `im.radius` 직접 사용. PlayMode `Impact_noise_is_not_boosted_by_squad_hearing`(12m 무시). **EditMode 195 / PlayMode 47 green.**
- ✅ **이동 중 사격 페널티 (2026-06-25)** — 이동하면서 쏘면 ① 이동속도 감소 ② 탄퍼짐 증가 → 정조준하려면 멈춰야 함(킬존 압박).
  - **순수 시임/테스트**: `MovingSpread`(`SpreadMultiplier`(speed/maxSpeed→1+penalty), `MoveSpeedFactor`(사격 중 감속), EditMode 7).
  - **글루**: `Weapon.ApplySpread(dir, spreadMultiplier)`(탄퍼짐 배수) · `Player_WeaponController.FireSingleBullet`이 `player.movement.CurrentPlanarSpeed/MaxSpeed`로 배수 계산(`movingSpreadPenalty=2`) + `IsShooting()` 노출 · `Player_Movement`가 사격 중 `shootingMoveFactor=0.5`로 감속. (손맛=WASD+사격은 사용자 최종 확인.)
- ✅ **소음 테이블 재설계 — 발포음/피격음 우선순위 해결 (2026-06-25, 사용자)** — 이전 "발포음 들리는데 피격음 따라가던" 문제 해결. 자세히 [Wiki §6.2.1](Wiki.md).
  - **데이터 테이블 `NoiseCatalog`**(TDS.Core): 수치=가청 거리. 발포음 35(발생자=플레이어), 피격음 9(박힌 위치), 폭발음 90(발생자=플레이어 — 폭발은 플레이어 위치 광역 광고), 발소리 8/재장전 12(추후). **플레이어 소리만 적 반응**(적끼리 X).
  - **우선순위 = 가장 큰 소리 승**: `NoiseModel.Resolve`(순수)가 들리는 것 중 loudness 최대 선택 → 발포음(35)>피격음(9). revealsSource면 플레이어, 아니면 소음 위치.
  - **글루**: `NoisePing` 종류별 채널(`Emit(type,noisePos,sourcePos)`+`EmitGunshot/EmitImpact/EmitExplosion`) · `Player_WeaponController`=`EmitGunshot` · `Bullet`=비-적 충돌 시 `EmitImpact`(플레이어 총알만, `emitImpactNoise` 플래그) · `Enemy.HeardNoise`가 `ActiveChannels`→`Resolve`. 분대 청각 부스트/`squadHearingRadius` 제거(loudness가 사거리), `gunshotNoiseRadius`/`impactNoiseRadius` 필드 제거. **EditMode 205 / PlayMode 47 green.**
- ✅ **대형 절차적 맵 1024×1024 (2026-06-24, 사용자)** — "1024 정도를 아주 깔끔하게". 자세히 [Wiki §3](Wiki.md).
  - **카운트 상한**: `MapConfig.obstacleCount`(>0이면 셀별 확률 대신 카운트, 같은 셀 중복 방지) — 256²×density 수천 객체 폭발 방지. `MapConfig_Default`=grid 256×256×cell4=1024, 장애물500+cover60+배럴30=~595.
  - **NavMesh 콜라이더 베이크**: `NavMeshSurface.useGeometry=PhysicsColliders` — 정적배칭 결합메시 Read/Write off로 **빌드 런타임 베이크 실패**(`does not allow read access`)하던 것 해결. 적 18/18 navmesh 위.
  - **바닥 타일링**: `floorTileWorldUnits=8`(머티리얼 인스턴스에만).
  - **실측(에디터)**: 런타임 ~95fps(10.6ms), draw call 50/batch 49, tris ~95k. 생성+베이크 일회성 ~2.9s. PlayMode `Large_map_bounds_obstacle_count`(1024서 카운트 상한). **EditMode 205 / PlayMode 51 green.**
  - **추후(선택)**: 베이크 시간 단축(voxel size 튜닝/비동기), 광역 맵 청크 스티칭(§ 미래), 바이옴/구역.
  - **추후**: 유탄/폭발 실제 발신(현재 테이블만), 발소리/재장전 발신 연결. `NoiseCatalog` SO화(디자이너 튜닝).
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
