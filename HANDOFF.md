# ShadowProtocol 연계 문서

마지막 정리: 2026-11-26 17:10(6.5 남은 일 일부 반영 — 경비 기준 자세·웅크림 카메라·시작 표시판, **플레이 확인 전**). 다음 작업자(사람 또는 Claude)가 이 문서와 `Assets/ShadowProtocol/PROGRESS.md`만 읽고 이어갈 수 있게 정리했다. 가장 최근 작업은 **6.6장 (2026-11-26)** → 6장 웅크림 동작 인계 순으로 읽는다.

## 1. 게임 개요
- 장르: 3인칭 잠입 액션 데모. 밤 창고에 침투해 서버실 데이터 코어를 확보하고 시작 지점으로 탈출한다.
- 엔진: Unity 6000.6.8f1, **HDRP 17.6**(2026-11-24 Built-in에서 전환, Linear), uGUI.
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
| 빌드 자동 검증 | `ShadowProtocol.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -autotest -logFile <경로>` → 30초간 경비 이동 거리·NavMesh 오류·GPU/CPU 프레임 시간을 `[AutoTest]` 로그로 남기고 종료. `-autotest-shots <폴더>`를 더하면 p7과 같은 6시점을 실제 화면으로 찍고 시점별 GPU 시간도 남긴다(hdrp_01~06) |
| 봇 플레이 | 플레이 모드에서 `ShadowProtocolBot.FramesPerTick=8; ShadowProtocolBot.Run(이름, ShadowProtocolBotRoutes.HallQuiet() / OfficeTools() / CatwalkProbe(2))` (봇은 에디터를 멈춘 채 Step()으로 진행 — 이때 HDRP에서 인물이 유령처럼 보이는 것은 단계 진행 때만의 현상이라 봇이 인물 모션 벡터를 Camera로 바꿔 둔다) |

주요 코드: `Assets/ShadowProtocol/Scripts/` (GuardAI, GuardDirector, PlayerController, PlayerTools, ThirdPersonCamera, GameManager, GameUI, NavBootstrap, AutoTest, 퍼즐 스크립트), 씬 생성기 `Scripts/Editor/ShadowProtocolScene*.cs`.

## 3. 지금 상태 (2026-11-13)
- 끝남: 위 시스템 전부, 빌드 경비 정지 버그 수정(NavMeshAgent를 끈 채 저장 → `NavBootstrap`이 NavMesh 확인 후 켬, exe 실행 로그로 경비 5명 30초 24~53m 이동 확인), 자막 조사 처리(`Josa.cs`), 크레딧을 사실대로 정리.
- 2026-11-16 오후: Blender 최종본 환경 10종·컨테이너를 씬에 반영(`HoldEnvModules = false`, `HoldContainerV2 = false`). 가져오기 메뉴 `ShadowProtocol/Import Env Final (Blender)`·`Import Container Final (Blender)` → `Build Demo Scene`. 검증 수치는 PROGRESS.md.
- 2026-11-16: 봇 2루트 탈출·캣워크 적응 확인, 카메라 문틀 '옆으로 비켜 서기'(0.8m 미만 25→8/12,464), 드로우콜 축소(그림자 2단·45m, 작업등 강제 픽셀 3, 인스턴싱, farClip 100m, 벽 가림 컬링 → 6개 시점 합계 6,078→4,170), 재빌드 후 `-autotest` PASS, 스크린샷 `Captures/p4_*.png`. 자세한 수치는 PROGRESS.md.
- 남음
  1. (끝남, 위 2026-11-16 참고)
  2. (끝남) Blender 최종본 전부 반영: 환경·컨테이너·인물 3명·애니메이션 18개(2026-11-16, PROGRESS.md)
  3. (끝남) 플레이어 = 여성 잠입 요원, 경비 = 중무장 경비병(Yard·West·Vault·Door) / 경비 반장(Center·Office). 가져오기 메뉴 `ShadowProtocol/Import Characters Final (Blender)` → `Setup Animations`(씬 빌드에 포함)
- 2026-11-24: 렌더 = HDRP 17.6 + Linear(`ShadowProtocolHDRP.ApplyToScene`, 빌더 마지막 단계). 볼륨 효과를 끌 때는 컴포넌트 active=false가 아니라 켠 채 세기 0(꺼 두면 HDRP 기본 볼륨 프로필 값이 적용됨 — 모션 블러 0.5가 그렇게 켜져 있었다). 플레이어 애니메이션 = Humanoid(`ShadowProtocolAnimBuilder.PlayerGeneric=false`), 클립 아바타 기준 자세를 모델 T자세로 맞춰(`MatchSkeleton`) Blender 자세와 ±1cm·±1.5° 일치. 웅크림 절차적 덧입힘(CharacterProceduralPose stealth*)은 2026-11-24 사용자 결정으로 **꺼짐(0)** — 6장 참고. 자세 측정: `ShadowProtocolAnimBuilder.MeasureClipPose(클립, 프레임수)`, 플레이 모드 `ShadowProtocolPoseProbe`.
- 알려진 문제: 소리는 귀로 확인 못 함(코드 합성음), 드로우콜 6개 시점 합계 4,679(2026-11-16 저녁, pixelLightCount 2·펜스 합치기·작은 장식 30m 컬링 반영), 좁은 복도 문틀에서 옆을 볼 때 카메라 거리 0.65~0.70m(드묾), 빌드 용량 약 416MB.

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
- claude-hand **1.0.0** (`C:\Users\mbc\Documents\claude-hand`, 2026-11-25 0.12.1에서 올림): Unity·Blender 창마다 전담 하위 세션. Unity 6.6 호환을 위해 `unity/com.claudehand.tools/Editor/ChCompat.cs`에 `UNITY_6000_6_OR_NEWER` 조건을 로컬로 추가했다(1.0.0에도 없음, 업스트림 반영 필요 — `git pull` 때 stash 후 다시 적용).
- 주의(2026-11-25): Unity 에디터가 꺼졌다 켜지면 그 전에 연결돼 있던 Claude 세션의 `unityMCP`가 "실패"로 굳고, 그 세션에서 띄우는 `unity-*` 하위 작업자에게도 MCP for Unity 도구가 붙지 않는다(claude-hand `ch_*` 도구만 붙음). 세션의 MCP 목록(/mcp)에서 다시 연결하거나 새 세션을 연다.
- MCP for Unity 서버는 Unity 에디터가 켤 때 `localhost:8080`에 뜬다. 에디터를 모두 닫으면 서버도 꺼진다.
- 관련 프로젝트: SugarDrop3D(3D 매치-3), NyangPunch(고양이 머지+턴제 퍼즐). 각 저장소의 HANDOFF.md 참고.
- Blender는 화면 없이 돌려도 된다: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b <파일.blend> --python <스크립트> -- <인자>`. 이번 웅크림 작업은 전부 이 방식으로 했다(MCP 연결 불필요).
- unityMCP(기본 연결)는 자주 끊긴다(ECONNREFUSED). 그때는 `unity-shadowprotocol` 전담 작업자를 쓰거나, Unity에서 Window > MCP for Unity > Start Server 후 다시 연결한다.

## 6. 웅크림 동작 인계 (2026-11-24~25)

### 6.1 무엇을 했나
- 사용자 지적: "몸 숙이고 걷는 게 어색함" → Mixamo 원본(`ArtSource/Mixamo/`) 위에 Blender에서 7·8차로 다시 만들었다(1~6차 기록은 `ArtSource/BLENDER_PROGRESS.md`).
- 참고 자료: 사용자가 직접 녹화한 어쌔신 크리드 오리진 영상(`C:\Users\mbc\Videos\Captures\Assassin's Creed Origins 2026-11-24 12-29-28.mp4`, 11초). **게임 데이터는 추출하지 않았다.** 영상에서 자세·박자만 재서(상체 70~75°, 골반 무릎 높이, 팔이 아래로 늘어져 손이 땅 근처, 분당 약 150걸음) 수치로 우리 캐릭터에 다시 입혔다. 게임 화면이 들어간 비교 그림은 저장소에 넣지 않는다(저작권).
- 7차: 골반 0.60 → 0.52m, 상체 37.5 → 66°, 팔을 아래로 늘어뜨림, 머리 정면 보정.
- 8차(사용자 "아직 어색함" → 원인을 재서 고침):
  1. 앞무릎이 어깨보다 앞으로 나와 가슴에 붙음 → 발 디딤을 골반 뒤로 0.25m(`feet_back`), 무릎 바깥 27°(`knee_out`)
  2. 팔이 거의 안 움직임(손 11~14cm) → 반대쪽 발과 엇갈리는 팔 흔들기(±14°), 손 18~21cm
  3. 앞무릎이 안쪽으로 모여 안짱다리 → 무릎 안쪽 돌림 제거
  4. 상체가 몸 왼쪽으로 4~8cm 치우침(왼팔이 늘 허벅지에 닿던 원인) → 어깨 방향 보정 42.8 → 31°(치우침 1cm)
  5. 몸통·머리 굳음 → 골반 흔들림·어깨 비틀림 키움, 머리 흔들림 40% 남김
  6. 발이 떨어질 때 뒤로 14cm, 닿을 때 앞으로 25~38cm 튐(6·7차에도 있던 문제) → `fix_swing`: 공중 구간 궤적을 양 끝 속도가 디딤 속도와 같은 곡선으로 이음
- 게임 쪽: `CharacterProceduralPose`의 stealth 덧입힘 5개 값(22/0.10/14/22/14)이 새 클립 위에 겹쳐 상체 83°·골반 0.62m가 됐다 → 스크립트 기본값과 씬 7곳 모두 0. 이 값은 6차 클립에 맞춘 것이었으므로 다시 켜지 않는다.

### 6.2 지금 값 (게임 안 측정, 플레이 모드 + `ShadowProtocolPoseProbe`)
| 항목 | 웅크려 걷기 | 웅크린 대기 |
|---|---|---|
| 골반 높이 | 0.537m (0.523~0.549) | 0.540m |
| 상체 숙임(골반→목, 수직 기준) | 66.7° | 66.6° |
| 머리 시선 | +9.2° | +1.6° |
| 머리 꼭대기 | 0.91~0.94m | 0.89m |
| 팔–다리 파고듦 | 0/35프레임(최소 틈 0.28cm) | 0/63(최소 0.77cm) |
| 무릎 간격 | 52~61cm | 43cm |
| 속도 | 1.20m/s(`PlayerController.crouchSpeed` 1.20과 같음) | — |
| 발 | 세계 기준 뒤로 미끄러지는 순간 없음, 디딤 중 고정 | 고정 |

### 6.3 파일
| 무엇 | 위치 |
|---|---|
| 내보낸 동작(Unity로 가는 것) | `ArtSource/Characters/05_final_export/anims/female_agent@CrouchWalk.fbx`(25프레임) / `@CrouchIdle.fbx`(76) + `.claudehand.json`(revision, `natural_speed_mps` 1.2, `intended_pose`) |
| Unity 쪽 | `Assets/ShadowProtocol/Animations/Final/female_agent@Crouch*.fbx`(위 파일 복사본, .meta 설정 유지) |
| Blender 원본 | `ArtSource/Characters/02_blender_work/characters_final.blend`(동작 `female_agent@CrouchWalk/Idle`). 백업 `.pre_d7.bak`, `.pre_d8.bak` |
| 다시 만드는 스크립트·설정 | `ArtSource/Characters/02_blender_work/d7_crouch/` — `d8_try.py`(최종), `final9_CrouchWalk.json`(걷기 최종), `final8_CrouchIdle.json`(대기 최종), `*.pf.json`(팔 프레임별 값), `d7_render.py`(확인 렌더) |
| 확인 그림 | `ArtSource/Characters/03_renders_final/anims/female_agent_crouch_d8_walk.jpg`, `_d8_idle.jpg`. 게임 캡처 `Captures/d9_*.png`(웅크려 걷기), `d7_on_*/d7_off_*`(덧입힘 켬/끔 비교) |
| 상세 기록 | `ArtSource/BLENDER_PROGRESS.md`의 7차·8차 |

`ArtSource`는 git에 올라가지 않는다(.gitignore). 위 Blender 원본·스크립트는 이 PC에만 있으니 옮길 때는 폴더째 복사한다.

### 6.4 다시 만들기 / 고치기
```
cd ArtSource\Characters\02_blender_work
blender -b characters_final.blend --python d7_crouch\d8_try.py -- CrouchWalk d7_crouch\final9_CrouchWalk.json
blender -b characters_final.blend --python d7_crouch\d8_try.py -- CrouchIdle d7_crouch\final8_CrouchIdle.json
```
- json에 `"save": true, "export": true`가 있으면 동작을 .blend에 저장하고 FBX·json을 내보낸다(빼면 측정만). 결과는 `RESULT {...}` 한 줄(골반·상체·팔 겹침·무릎·발 속도 진단 포함).
- `d8_try.py`는 .blend 안 텍스트 블록 `helpers`를 실행 전에 고쳐 `feet_back`·`spine_roll` 설정을 추가한다(헬퍼 원본은 그대로).
- 주요 설정: `hips_z`(골반), `lean`(상체 — 원본 숙임 위에 더함, 약 0.56배로 반영), `knee_out`(무릎 바깥 각), `yaw.shoulders`(어깨 방향 = 상체 좌우 치우침 보정), `feet_back`(발 디딤 뒤로), `narrow`(발 간격), `life`(흔들림), `arm.swing_deg`(팔 엇갈림), `arm.abds`(팔 벌림 후보, 음수 = 안쪽), `head_keep`.
- 팔은 프레임마다 "다리 메시와 0.2cm 이상 떨어지고 손이 바닥 위 3cm 이상"인 가장 늘어진 각을 찾는다. `NOFIT` 줄이 나오면 그 프레임은 맞는 각이 없다는 뜻 → 무릎 위치(`knee_out`, `feet_back`)나 `abds` 후보를 바꾼다.
- 내보낸 뒤 Unity: FBX를 `Animations/Final`에 복사 → `AssetDatabase.ImportAsset(ForceUpdate)`. **`ShadowProtocol/Setup Animations` 메뉴는 가능하면 쓰지 않는다** — 컨트롤러를 지우고 다시 만들어 열린 씬의 Animator 참조가 끊긴다(쓴 뒤에는 씬을 디스크에서 다시 불러올 것. 씬 파일 자체는 멀쩡).

### 6.5 남은 일
1. 사용자가 게임에서 직접 움직여 보고 판단하는 단계(정지 캡처·수치로만 확인함). 야간 장면이라 캡처가 어둡다.
2. 작은 점: 발이 닿는 순간 발끝이 땅속 0.4~0.7cm, 팔–다리 최소 틈 0.28cm(파고듦 0), 게임 안 손 엇갈림 16~17cm(의도 18~21cm).
3. 웅크림 카메라: 머리 꼭대기가 0.9m로 낮아져 캐릭터가 화면 아래쪽에 작게 잡힌다. `ThirdPersonCamera`의 `crouchPivotHeight`(지금 1.05)를 낮출지 검토.
4. 경비 클립에도 플레이어처럼 기준 자세 문제(`MatchSkeleton`)가 있는지 확인 안 함.
5. 시작 표시판이 야드에서 밝은 파랑으로 튐(HDRP 전환 뒤).
6. 파쿠르(뛰어넘기·슬라이딩, `ParkourController`, `ShadowProtocolMixamoBake`)는 다른 세션이 만든 것으로 같은 커밋에 들어갔다. 이번 웅크림 작업에서는 검증하지 않았다.
7. (끝남 2026-11-26) `ad671f4`와 아래 6.6 작업을 `origin`(alslxhrl7-web/shadowprotocol)에 푸시.

## 6.6 2026-11-26 반영분 (플레이 확인 전)

감독 세션("Claude-hand 설치", fiveriver 폴더)이 `unity-shadowprotocol` 작업자로 6.5의 3·4·5를 진행했다. 도중에 Unity가 꺼지고 작업자들의 MCP 연결이 끊겨 **씬 다시 만들기 1회 뒤의 확인은 하지 못했다.**

| 항목 | 바꾼 것 | 측정 · 상태 |
|---|---|---|
| 6.5-4 경비 클립 기준 자세 | `Scripts/Editor/ShadowProtocolAnimBuilder.cs`: `MatchSkeleton`을 `Player`만이 아니라 `Heavy`·`Captain` Blender 클립에도 적용. `ConfigureImporters()`만 실행해 경비 FBX 12개 다시 가져옴(.meta 12개 바뀜) | 기준 자세 차 전 위팔 21~70°·손 13~81°·Run 발 134~139° → 후 12개 모두 0.00°. Blender 원본 대비 게임 자세 전 최대 18.9~26 cm → 후 평균 0.2 cm·최대 0.7~1.3 cm. **발 미끄러짐 수정 후 값은 못 잼**(수정 전: Yard 걷기 L0.08/R0.23, 달리기 L0.39/R0.95 · Center 걷기 L0.13/R0.24, 달리기 L0.41/R1.10 m/s, 발목 기준 접지 구간 최저 속도 평균) |
| 6.5-3 웅크림 카메라 | `Scripts/ThirdPersonCamera.cs` `crouchPivotHeight` 1.05 → **0.95** (씬 값도 0.95) | 웅크리면 카메라 거리가 0.62배라 화면 속 크기는 서 있을 때와 같음(0.28 대 0.285). 피벗이 머리(0.89 m)보다 높아 화면 2~3% 아래에 잡히던 것을 맞춤. 기본 줌 4 m 예상 가운데 0.354(서기 0.359). **실측·전환 튐·좁은 복도/문틀/환기구 벽 뚫림 검사 못 함**. 웅크림 9차가 오면 다시 볼 것 |
| 6.5-5 시작 표시판 파랑 | `Scripts/Editor/ShadowProtocolSceneBuilder.cs`: `Mat("StartPad", (0.12,0.28,0.52))` 발광 인자 제거 + `Mat()` 발광 없음 분기에서 `_EmissionColor`=검정·`EmissiveIsBlack`. `ShadowProtocolHDRP.cs`는 주석만 | 원인: 0.15 nits 발광이 밤 아스팔트(약 0.02 nits)보다 밝아 자동 노출에 판만 빛남. **현재 커밋된 `StartPad.mat`·씬은 마지막 수정 전에 만들어져 아직 흰색 0.15 nits** → `ShadowProtocol/Build Demo Scene`을 한 번 더 돌리고 `_EmissiveIntensity` 0 확인 필요. 비교 캡처 `Captures/cam65_p105_1_stand_idle.png`(시작 지점 (-35.5,0.04,-15), yaw 45°, pitch 15°, 줌 4.8 m) |
| 가림 컬링 로그 "0.0 KB" | 바꾸지 않음 | `Assets/Scenes/ShadowProtocol_Demo/OcclusionCullingData.asset`에 데이터 있음(렌더러 211). 빌더가 `Compute()` 직후 `StaticOcclusionCulling.umbraDataSize`를 찍어 0이 나오는 로그 문제로 추정. 드로우콜(6개 시점 합계 기준 4,679) 미측정 |
| 6.5-6 파쿠르 | — | 확인 못 함 |

- 씬 다시 만들기 때 애니메이터 컨트롤러 3개(`SP_GuardAnimator`·`SP_GuardCaptainAnimator`·`SP_PlayerAnimator`)도 빌더가 새로 만들었다. 씬도 그 뒤에 만들어져 참조는 이어져 있음(플레이 확인 전).
- 같은 날 다른 세션("ShadowProtocol 인계 작업 이어가기")이 웅크림 8차의 균형을 쟀다: 대기에서 무게중심이 발끝보다 16~22 cm 앞, 걷기 한 발 디딤 때 35~45 cm 앞, 엉덩관절 최대 153° → 사용자 결정 "목표대로 다시 만들기(9차)", 속도 1.20 유지. 9차는 아직 만들어지지 않았다(패치 스크립트 `make_d9.py`는 그 세션 임시 폴더에만 있고 미실행).
- 다음에 할 일: ① Build Demo Scene → StartPad 발광 0·캡처 비교 ② 경비 발 미끄러짐 수정 후 값 ③ 웅크림 카메라 실측·벽 뚫림 ④ 가림 컬링 로그·드로우콜 ⑤ 파쿠르 확인 ⑥ 웅크림 9차.
