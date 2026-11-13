# ShadowProtocol 연계 문서

마지막 정리: 2026-11-13. 다음 작업자(사람 또는 Claude)가 이 문서와 `Assets/ShadowProtocol/PROGRESS.md`만 읽고 이어갈 수 있게 정리했다.

## 1. 게임 개요
- 장르: 3인칭 잠입 액션 데모. 밤 창고에 침투해 서버실 데이터 코어를 확보하고 시작 지점으로 탈출한다.
- 엔진: Unity 6000.6.8f1, Built-in 렌더 파이프라인(Standard 셰이더), uGUI.
- 맵: 하역 야드(시작·탈출) → 메인 홀(선반 통로, 2층 캣워크, 환기구) → 사무 구역(경비실·사무실·창고, 덕트) → 서버실(데이터 코어). 약 1,900m².
- 핵심 시스템
  - 3인칭 카메라(마우스 회전, 휠 거리, 벽 관통 방지, Tab 전술 시점)
  - 경비 AI: 순찰 → 의심 → 경보·추격 → 수색, 시야·소음·밝기 감지, 무전으로 위치 공유
  - **적응형 경비(GuardDirector)**: 플레이어가 자주 쓰는 길·엄폐물·퍼즐을 기록해 재시도 때 감시를 옮기고 무전 자막으로 알림(한 번에 최대 2명, 항상 3개 경로 이상 열림)
  - 도구: 병·캔 던져 유인, 뒤에서 기절, 기절한 경비 끌어 숨기기
  - 패턴 퍼즐 3종: 레이저 격자(박자 패턴), 보안 키패드(코드 조각 4개 수집, 판마다 무작위), 배전반(3×3 Lights Out)
  - 체크포인트, 튜토리얼, 결과 등급 S~C, 제작 공정(Making Of) 갤러리·턴테이블 뷰어, 크레딧
- 조작: WASD 이동, 마우스 시점, Shift 달리기, Ctrl/C 웅크리기, 우클릭 조준·좌클릭 던지기(G 빠른 던지기), E 제압·상호작용, F 끌기, Tab 전술 시점, P/Esc 일시정지, R 체크포인트 재시작.

## 2. 실행·빌드·검증
| 하는 일 | 방법 |
|---|---|
| 씬 다시 만들기 | 메뉴 `ShadowProtocol/Build Demo Scene` (씬은 손으로 고치지 말고 SceneBuilder 코드를 고친 뒤 다시 만든다) |
| Windows 빌드 | 메뉴 `ShadowProtocol/Build Windows` → `Builds/Windows/ShadowProtocol.exe` |
| 빌드 자동 검증 | `ShadowProtocol.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -autotest -logFile <경로>` → 30초간 경비 이동 거리·NavMesh 오류를 `[AutoTest]` 로그로 남기고 종료 |
| 봇 플레이 | 플레이 모드에서 `ShadowProtocolBot.Run(이름, ShadowProtocolBotRoutes.HallQuiet() / OfficeTools() / CatwalkProbe(2))` |

주요 코드: `Assets/ShadowProtocol/Scripts/` (GuardAI, GuardDirector, PlayerController, PlayerTools, ThirdPersonCamera, GameManager, GameUI, NavBootstrap, AutoTest, 퍼즐 스크립트), 씬 생성기 `Scripts/Editor/ShadowProtocolScene*.cs`.

## 3. 지금 상태 (2026-11-13)
- 끝남: 위 시스템 전부, 빌드 경비 정지 버그 수정(NavMeshAgent를 끈 채 저장 → `NavBootstrap`이 NavMesh 확인 후 켬, exe 실행 로그로 경비 5명 30초 24~53m 이동 확인), 자막 조사 처리(`Josa.cs`), 크레딧을 사실대로 정리.
- 보류: Blender 최종본을 기다리는 에셋. `ShadowProtocolSceneBuilder.HoldContainerV2 = true`, `HoldEnvModules = true`. 최종본으로 교체한 뒤 false로 바꾸고 씬을 다시 만든다.
- 남음
  1. 봇 2루트 탈출, 카메라 관통 검사, 재빌드 후 `-autotest` 재확인, 스크린샷
  2. Blender 최종본(환경 7종·컨테이너·인물 3명·애니메이션 9종)을 받아 교체
  3. 새 인물 반영: 플레이어 = 여성 잠입 요원, 중무장 경비병·경비 반장 v2
- 알려진 문제: 소리는 귀로 확인 못 함(코드 합성음), 드로우콜 많음, 문 상인방 아래 카메라 거리 0.62~0.77m(일부), 빌드 용량 약 314MB.

## 4. 아트 파이프라인 (반드시 지킬 규칙)
1. **VARCO 3D**에서 이미지 → 3D 메시(→ 리깅·애니메이션) 생성. 프롬프트·생성 이미지·원본 메시는 `ArtSource/<분류>/01_varco_source/`에 보관.
2. **Blender에서 상업용 게임 수준까지 다듬는다.** AI 특유의 느낌(물결진 평면, 휜 모서리, 뭉개진 디테일, 흐린 텍스처, 구운 조명)을 지운다. 다듬기 작업자는 Fable 5.2 모델로 띄운다(사용자 지시).
3. 다듬은 결과만 `ArtSource/<분류>/05_final_export/`의 manifest로 Unity에 가져온다. **VARCO 원본을 게임에 바로 넣지 않는다.**
4. 재료는 VARCO 생성물과 Blender 기능만 쓴다(외부 텍스처·에셋 금지). 전/후 렌더를 `03_renders_final/`에 남겨 제작 공정 갤러리에 쓴다.

ArtSource 폴더
| 폴더 | 내용 |
|---|---|
| `ArtSource/Container/` | 20ft 컨테이너. 01 원본, 02 .blend, 03 렌더(1차), 04 1차 정리본, 03/05 `_final` = 최종본 |
| `ArtSource/Environment/` | 외벽·콘크리트 바닥·아스팔트·펜스·캣워크·가로등·트러스 7종 |
| `ArtSource/Characters/` | 여성 요원(새 플레이어), 중무장 경비병 v2, 경비 반장 v2 (리깅 완료 원본, 다듬기 전) |
| `ArtSource/BLENDER_PROGRESS.md` | Blender 다듬기 진행 메모 |

Blender 최종 다듬기 진행(2026-11-13 중단 시점)
- 끝남: 콘크리트 바닥(4×4×0.2m, 무이음매 타일 텍스처, `Environment/05_final_export/env_floor_*`). 근접에서 잔금 무늬가 약하게 남음, 검증 렌더 일부는 노멀 강도 조정 전 버전.
- 남음: 아스팔트(바닥과 같은 방식), 펜스·캣워크·가로등·트러스(원본이 뭉개져 참조 이미지 기준으로 깨끗한 지오메트리 재구성 후 VARCO 팔레트·절차적 재질 베이크), 벽(원본 메시는 못 씀 → v2b 참조 이미지 기준으로 패널 제작), 컨테이너 최종본, 인물 3명, 애니메이션 9종.
- `.blend` 안 텍스트 블록 `helpers`에 무이음매 합성·노멀 생성·검증 렌더 함수가 있다: `exec(bpy.data.texts['helpers'].as_string())`.
- Blender 원본 묶음 zip: `Documents\user\_Archives\VARCO_Blender_Originals_2026-11-13.zip` (git에는 ArtSource를 올리지 않는다).

VARCO 플로우: VARCO 3D 편집기의 플로우 `394d7222-dceb-4c59-96cc-bec0855fcaa7`(이름 Untitled)에 이 프로젝트의 노드가 모두 있다. 지금까지 사용한 포인트는 약 14,000.

## 5. 도구 환경
- claude-hand 0.12.1 (`C:\Users\mbc\Documents\claude-hand`): Unity·Blender 창마다 전담 하위 세션. Unity 6.6 호환을 위해 `unity/com.claudehand.tools/Editor/ChCompat.cs`에 `UNITY_6000_6_OR_NEWER` 조건을 로컬로 추가했다(업스트림 반영 필요).
- MCP for Unity 서버는 Unity 에디터가 켤 때 `localhost:8080`에 뜬다. 에디터를 모두 닫으면 서버도 꺼진다.
- 관련 프로젝트: SugarDrop3D(3D 매치-3), NyangPunch(고양이 머지+턴제 퍼즐). 각 저장소의 HANDOFF.md 참고.
