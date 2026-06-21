# TDS 작업 루프 (읽고 따라가는 절차)

> 로드맵을 **차례대로** 진행할 때 이 루프를 돈다. 테스트 하네스 규칙은 [Testing.md](Testing.md), 시스템 레퍼런스는 [Wiki.md](Wiki.md), 진행 이력은 [Roadmap.md](Roadmap.md).

---

## 기능 1개 = 1 루프

```
1. 계획     로드맵에서 다음 항목 확인 → 무엇을/왜. 필요시 TaskCreate.
2. 시임     유니티 의존 로직을 순수 클래스로 뽑는다(TDS.Core). → Testing.md §3
3. RED      순수 시임의 EditMode 테스트를 먼저 짠다(실패 확인).
4. GREEN    시임 구현 → EditMode green.
5. 글루      MonoBehaviour가 시임을 호출하도록 배선(TDS.Game).
6. 통합     씬/프리팹 필요하면 PlayMode 통합 1~2개(실제로 도는지).
7. 검증     MCP로 Play → 스크린샷 + read_console(에러 0). 손맛/비주얼 확인.
8. 커밋     내 변경만 스테이징해서 커밋(메시지에 무엇/왜/검증).
9. 문서     Wiki + Roadmap 갱신. 테스트 갭 채웠으면 Testing.md 체크.
```

7~9는 매 기능마다. 작은 단위로 자주 커밋한다.

## 규칙 (확정)

- **커밋은 자율적으로.** 체크포인트면 묻지 말고 커밋. 단 **내가 만든 변경만** — 사용자의 무관한 미커밋 변경(인코딩 diff, .idea/ 등)은 절대 같이 넣지 않는다. `git status`로 확인 후 경로를 명시해 `git add`.
- **브랜치:** main이 아닌 작업 브랜치에서. 커밋 메시지 끝에 Co-Authored-By 라인.
- **기능 추가/수정 = 문서 갱신 필수.** [Wiki.md](Wiki.md)(시스템 사실), [Roadmap.md](Roadmap.md)(진행/커밋). 사용자 명시 요구사항.
- **사운드 보류:** AudioManager 없는 컨텍스트 가드 유지(연결 시 자동 동작). [Roadmap.md] §Phase D.

## MCP 운영 메모

- **스크립트 수정 후:** `refresh_unity`(도메인 리로드로 연결 잠깐 끊김 = 정상) → `editor/state`로 `ready_for_tools`/컴파일 완료 확인 → `read_console`(error)로 컴파일 에러 0 확인. **그 다음에야** 새 타입 사용 가능.
- **execute_code:** 메서드 본문으로 실행 → `using` 불가, 풀네임 사용. 무거운 작업(navmesh 베이크/씬 저장)은 타임아웃 떠도 유니티 측에서 완료됨 → 이후 상태로 검증.
- **에셋/프리팹 수정:** `AssetDatabase.MoveAsset`(GUID 보존, 파일시스템 mv 금지), 프리팹은 `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`. 직렬화 값은 `SerializedObject` + `ApplyModifiedProperties()` + `SetDirty` + 재로드 검증.
- **씬 검증:** Play → `manage_camera screenshot`(카메라 미지정 = ScreenCapture라 Overlay UI 포함; 카메라 지정 = 시점 자유지만 Overlay 제외) → `read_console`. 끝나면 `stop`.
- **결정성:** 시드 사용처는 전역 `UnityEngine.Random` 말고 전용 `System.Random(seed)`.

## 다음에 할 것 (로드맵 차례)

[Roadmap.md] 진행 상태 참고. 큰 흐름: 적 전투 연출 → (사운드) → 긴장도 스폰(맵 완료로 가능) → 미션/차량 재통합 → 광역 맵 스티칭 → 생존/탈출 루프.
