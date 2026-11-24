# ShadowProtocol 진행 상황 (이어받기용)

## 끝난 단계(씬 저장·Windows 빌드까지 완료, 컴파일 오류 0)
- A1 사운드 / A2 uGUI / A3 튜토리얼 / C1 적응형 AI / C2 도구 / C3 VARCO 애니메이션 / C4 체크포인트 / C5 카메라·성능
- C6 퍼즐 3종 / C7 제작 공정 갤러리·턴테이블·크레딧
- 봇 2개 경로 승리, 적응 AI 3회 재시도 증거, 카메라 검사, 누수 검사, 스크린샷(Captures/p3_*.png)
- Windows 빌드: Builds/Windows/ShadowProtocol.exe (오류 0, 경고 0)

## 자료가 오면 할 일
- 컨테이너 v2: Assets/ShadowProtocol/Models/SP2/SP2_container_v2.fbx 로 넣고 씬 다시 빌드(자동 교체, FBX의 LODGroup 사용). 갤러리 카드의 '교체 예정' 표시는 ShadowProtocolSceneMakingOf.cs 의 pending 줄을 지운다.
- 환경 모듈 7종: Assets/ShadowProtocol/Models/Env/Env_{wall_panel,floor_slab,asphalt_slab,fence_section,catwalk_section,street_lamp,truss_worklight}.fbx (원점 바닥 가운데, 길이 +X, 정면 +Z) → 씬 다시 빌드하면 ShadowProtocolSceneEnvModules.cs 가 겉모습만 타일링 교체
- 제작 공정 이미지 추가: Assets/ShadowProtocol/MakingOf/<모델>/ 에 Sprite로 넣고 ShadowProtocolSceneMakingOf.cs 의 stages 에 추가

## 다시 만들기·검증
- 씬: 메뉴 ShadowProtocol/Build Demo Scene / 빌드: 메뉴 ShadowProtocol/Build Windows
- 봇: 플레이 모드에서 ShadowProtocolBot.Run(이름, ShadowProtocolBotRoutes.HallQuiet() | OfficeTools() | CatwalkProbe(2))

## 남은 문제(요약)
- 소리는 귀로 확인하지 못함, 좁은 복도 쪽 문틀에서 옆을 볼 때 카메라 거리 0.65~0.70m(8/12,464, 2026-11-16), 드로우콜 여전히 많음(6개 시점 합계 4,170, 아래 2026-11-16 참고)

## 2026-11-13 이어받기(진행 중, 막힘)
- 막힘: 외부 패키지 Claude Hand Tools(C:/Users/mbc/Documents/claude-hand/unity/com.claudehand.tools/Editor/ChCompat.cs 20·32·45행)가 Unity 6000.6의 폐기 API(GetInstanceID, InstanceIDToObject → CS0619 오류)로 컴파일 실패. 이 때문에 도메인 재로드가 안 돼 새 코드가 에디터에 반영되지 않고, 플레이 모드·Windows 빌드('Error building Player because scripts have compile errors in the editor')가 모두 막힘. ch_* 도구도 쓸 수 없음.
- 경비 정지 버그(빌드 'Failed to create agent because there is no valid NavMesh' x6) 수정 코드 작성(미검증): NavMeshSurface 데이터 연결·빌드 씬 경로는 정상. 원인 추정 = 빌드 씬 로드 시 네이티브 NavMeshAgent가 NavMeshSurface.OnEnable(AddData)보다 먼저 활성화. 수정: SceneBuilder BuildGuard에서 agent.enabled=false, GuardAI.Start에서 NavBootstrap.EnableAgent(NavMesh 등록 확인·보정 후 SamplePosition→켜기, 보정 시 로그 '[ShadowProtocol] NavMesh 보정: ...').
- 새 파일: Scripts/NavBootstrap.cs, Scripts/AutoTest.cs(실행 인자 -autotest [-autotest-seconds N]: 타이틀 건너뛰고 30초 경비 이동 거리·NavMesh 오류 수를 '[AutoTest]' 로그로 남기고 종료). Assembly-CSharp 컴파일 자체는 오류 없음.
- 남은 순서: 패키지 오류 해결 → Build Demo Scene → Build Windows → exe -autotest 로그 확인 → 컨테이너 v2 → 환경 모듈 6종 → 크레딧·조사 → 봇·카메라·스크린샷.
- [해결] 감독이 Claude Hand Tools 패키지 수정 → 재컴파일 오류 0, ch 브리지 등록.
- [1 완료] Build Demo Scene → Build Windows(오류 0·경고 0) → exe -autotest: NavMesh 오류 0, 경비 6명 모두 NavMesh 위, 순찰 경비 5명 30초 이동(24.0~52.7m, 합계 181.5m), Guard_Door는 고정 초소(0m). 'NavMesh 보정' 로그 없음 = Start 시점엔 데이터 등록됨 → 원인은 에이전트 활성화 순서로 확정.
- [2 완료] 컨테이너 v2: Models/SP2/SP2_container_v2.fbx(globalScale 1 = 실제 2.435×2.746×5.688m, 노멀 Import, LODGroup LOD0/1/2), 텍스처·Standard 재질 Models/SP2/container_v2_tex/(container_v2_mat, container_v2_LOD2_mat, 명시 remap). v2는 길이 축 +Z라 PlaceProp에서 yaw+90 보정. 야드 컨테이너 Y1~Y6 교체, 충돌체 메시 크기 자동, 겹침 0, 순찰 경로·시작→목표 NavMesh 연결 확인. 갤러리 '컨테이너 v2' 카드(프롬프트 글 → VARCO 이미지 → 원본 매트캡 → Blender 정리 4장 → 최종·턴테이블 8장 → 게임 3D), Stage.text 필드 추가. EnsureMeshLods에서 v2 제외.

## 2026-11-13 중단 시점 정리(사용자 지시로 여기서 멈춤)
### 끝난 것
- 1) 빌드 경비 정지 버그 수정·검증 완료(위 [1 완료] 참고). AutoTest 판정은 고정 초소 경비(stationary)를 이동 대상에서 빼도록 고침(재빌드 후 재확인 필요).
- 4) 자막 조사: Scripts/Josa.cs(받침 판별: Eul/I/Eun/Gwa) 추가, GuardDirector 무전·로그의 '을(를)'·'이(가)'·'X가' 7곳을 받침에 맞게 수정. 크레딧(GameUI.CreditLine·크레딧 화면): 환경은 아직 기본 도형이고 VARCO 모듈은 Blender에서 마무리 중, 벽 모듈은 제작 대기, 보안문·키패드·배전반·환풍기·던지는 병/캔·레이저 빔, 시야 원뿔 등 UI 표시물은 기본 도형이라고 사실대로 적음.
- 씬 다시 만들어 저장(컨테이너 기존 VARCO 6개, 환경 기본 도형, 경비 NavMeshAgent는 꺼진 채 저장). 컴파일 오류 0.
### 보류(사용자 지시: Blender에서 상업 게임 수준으로 다듬은 최종본만 게임에 넣는다)
- 2) 컨테이너 v2, 3) 환경 모듈 6종: 연결 코드는 남기고 보류 스위치로 씬에서 빼 둠. ShadowProtocolSceneBuilder.HoldContainerV2 = true, HoldEnvModules = true(최종본으로 교체한 뒤 false로 바꾸고 Build Demo Scene).
- 이미 가져온 현재 버전 에셋(최종본으로 교체될 예정): Models/SP2/SP2_container_v2.fbx + Models/SP2/container_v2_tex/(텍스처·Standard 재질), Models/Env/Env_{floor_slab,asphalt_slab,fence_section,catwalk_section,street_lamp,truss_worklight}.fbx + Models/Env/Textures/(텍스처·env_*_mat). globalScale 1(실제 m), 노멀 Import, 재질 remap 명시.
- 준비된 연결 코드: v2는 길이 축 +Z라 PlaceProp에서 yaw+90, LODGroup 사용(EnsureMeshLods 제외), 갤러리 '컨테이너 v2' 카드(프롬프트 글→VARCO 이미지→원본 매트캡→Blender 정리→턴테이블→게임 3D, Stage.text 필드). EnvModules: 경계·pitch 기반 타일링(바닥 90° 랜덤 회전, 펜스 3.0m 간격으로 이음부 기둥 겹침, 바깥 지면 아스팔트 4m 격자, 가로등 암을 야드 쪽으로·광원 이동, 트러스 6m 간격·작업등 광원 이동). 캣워크: 모듈 Z 1.4배(1.39m), LevelLayout에서 가운데 구간 데크 충돌체를 모듈 폭으로 줄이고 남쪽 난간·양 끝 투명 충돌벽 추가(모듈이 있을 때만). 최종본 치수가 바뀌면 상수(CatwalkZN/ZS, pitch) 재확인.
### 남은 것
- 5) 봇 2루트 탈출, 카메라 관통 검사, Build Windows 재빌드 → exe -autotest 로그 확인, 스크린샷(Captures/).
- 최종본 manifest가 오면 2)·3) 재개: 에셋 교체 → 보류 스위치 false → 배치·충돌체·NavMesh·스크린샷 확인.
- 참고: ch_import_model은 이번 세션에 노출되지 않아 import_model_file로 가져왔음(파일 교체는 되돌리기 지점에 포함되지 않음).

## 2026-11-16 검증·성능 정리(보류 스위치 HoldContainerV2·HoldEnvModules = true 유지)
- 봇(에디터 플레이 모드): HallQuiet 승리 180.0s 발각 0 제압 0 / OfficeTools 승리 146.5s 발각 0 제압 1 / CatwalkProbe(2) 2번째 재시도 뒤 지휘 무전 "캣워크를 자주 쓴다" → 경비 캣워크 순찰 전환(적응 확인). 성능·카메라 수정 후 씬을 다시 만들고 재확인: HallQuiet 승리 180.0s 발각 0, OfficeTools 승리 146.5s 발각 0 제압 1(결과 동일).
- 카메라: ThirdPersonCamera에 '옆으로 비켜 서기' 추가. 뒤·위(문 상인방)가 모두 막혀 카메라–머리 거리가 1m 아래로 떨어질 때 피벗 기준 옆 1.1/-0.6/-1.1m 후보를 SphereCast로 재고 가장 멀리 확보되는 쪽으로 부드럽게 이동(이미 비켜 선 쪽 선호로 좌우 튐 방지). 들어 올릴 때 실제 어깨 위치에서 위 공간을 다시 재서 상인방을 넘지 않게 함.
  - 검사(NavMesh 1m 격자 중 주변 1.6m 안에 벽이 있는 1,558곳 × 8방향 = 12,464회, 서기/웅크리기): 서기 0.8m 미만 25 → 8회(최소 0.51 → 0.65m), 1.0m 미만 45 → 27회, 카메라 벽 관통 0 → 0. 웅크리기는 전후 모두 0.8m 미만 0. 남은 8회는 좁은 복도 쪽 문틀(-16,13)·(5~6,13)·(10,11~12)에서 옆을 볼 때(0.65~0.70m, 캐릭터 반투명 처리).
- 드로우콜 축소(6개 시점 합계, 에디터 Game 뷰 UnityStats, 같은 세션에서 단계별 측정):
  - 전: 드로우 6,078 / SetPass 4,658 / 그림자 캐스터 1,542 / 삼각형 15.6M
  - 그림자 4단·150m → 2단·45m: 5,701 / 4,625 / 1,022 / 14.7M
  - 작업등 ForcePixel 6 → 3: 5,685 / 4,612
  - 재질 GPU 인스턴싱: 5,505 / 4,635
  - farClip 200 → 100m: 5,503 / 4,633
  - 벽 OccluderStatic + 가림 컬링 굽기: 4,170 / 3,386 / 1,015 / 12.0M (드로우 -31%, SetPass -27%)
  - 시점별 최종(드로우): 야드 1,813→1,218, 홀 1,122→852, 캣워크 586→508, 사무 905→557, 서버실 201→163, 야드→홀 1,451→872. 야드·홀 전후 화면 비교에서 눈에 띄는 차이 없음.
  - 코드: ShadowProtocolSceneSystems.ApplyPerformanceSettings(그림자·작업등·Level/Walls에 OccluderStatic), ShadowProtocolSceneBuilder(Mat/UnlitTransparentMat enableInstancing, farClip 100, Build()에서 StaticOcclusionCulling.Compute → Assets/Scenes/ShadowProtocol_Demo/OcclusionCullingData.asset). 그림자 값은 ProjectSettings/QualitySettings.asset(Ultra)에 저장됨.
  - 아직 많은 이유: SetPass가 드로우와 거의 같음 = Forward 렌더의 픽셀 조명 추가 패스(Ultra pixelLightCount 4, Auto 조명이 강제 픽셀 자리를 대신 채움)와 VARCO 소품마다 다른 재질. 다음 후보: pixelLightCount 4→2, VARCO 소품 텍스처 아틀라스·Mesh LOD 강화(승인 필요).
- Windows 빌드 성공(314.0MB, 오류 0·경고 0) → exe -autotest PASS: Guard_Office 24.0m, Guard_Vault 32.1m, Guard_Center 36.3m, Guard_Yard 52.7m, Guard_West 36.6m(합계 181.7m), Guard_Door 0m(고정 초소), 6명 모두 NavMesh 위, NavMesh 오류 0·예외 0.
- 스크린샷: Captures/p4_01_loading_yard, p4_02_main_hall, p4_03_catwalk, p4_04_office_corridor, p4_05_server_room_laser, p4_06_puzzle_keypad(.png).
- 남은 것: Blender 최종본 교체(보류 스위치), 소리 귀 확인, 픽셀 조명·VARCO 재질 정리로 추가 드로우콜 축소.

## 2026-11-16(오후) Blender 최종본 반영: 환경 10종 + 컨테이너 (HoldEnvModules = false, HoldContainerV2 = false)
- 가져오기: 새 메뉴 `ShadowProtocol/Import Env Final (Blender)`, `ShadowProtocol/Import Container Final (Blender)` (Scripts/Editor/ShadowProtocolEnvImport.cs). claude-hand manifest(05_final_export/*.claudehand.json, _superseded 제외)를 읽어 FBX를 Models/EnvFinal/env_*.fbx, Models/ContainerFinal/container_final.fbx로, 텍스처를 각 Textures/로 복사. 스케일 1, 노멀 Import, Mikk 탄젠트, 읽기 가능(씬 생성 때 메시 합치기용). 재질은 Built-in Standard(BaseColor/Normal/MetallicSmoothness(알파=매끈함)/Occlusion, 인스턴싱)로 Materials/에 만들고 FBX 재질 이름(M_env_galv → env_mat_galv, M_container_wall → container_mat_wall)으로 remap. 데칼은 Cutout(0.5), 렌즈(env_mat_lens)는 Emission 켬. 컨테이너는 _LOD0/1/2 → LODGroup 자동(25%/12.5%/1%).
- 실측 축(FBX 루트 -90°X 포함): 펜스 다음 기둥 로컬 -X, 가시철선 암 로컬 -Z / 가로등 암 로컬 -X / 트러스 위 +0.38·투광등 바닥 -0.69 / 캣워크 폭 1.39·데크 +0.25 / 벽 4×3×0.25 / 컨테이너 길이 X(6.06×2.59×2.44), 회전 보정 없음.
- 배치(ShadowProtocolSceneEnvModules.cs 다시 씀): 벽·상인방·칸막이 박스마다 env_wall 한 줄(4m 간격, 높이·두께는 박스에 맞춰 스케일, 칸막이의 VARCO Prop_Wall 치장은 최종본이 있으면 생략) 57개 / 바닥 4m 격자 64 / 아스팔트 야드 60 + 바깥 지면 551 / 펜스 7변 3m 간격 71 + 변 끝 env_fence_post_end 7(암이 부지 바깥으로 가도록 방향 자동) / 캣워크 4m 6(폭 그대로, 데크 윗면 맞춤) / 가로등 5(암을 야드 안쪽으로, 광원을 등기구로) / 트러스 6m 10(벽 안쪽 면에서 끝판이 닿게, 광원을 투광등 아래로, 카메라가 뚫지 않게 상자 충돌체) / 안전선 데칼 10(노란 띠 4, 흰 주차선 6). 충돌체·NavMesh는 기존 박스 유지.
- 드로우콜 대책: 같은 칸(지면 16m, 바닥·벽·캣워크 8m, 트러스 12m)의 모듈을 재질별 하위 메시 하나로 합침(Assets/Scenes/ShadowProtocol_Demo/EnvMerged/*.asset, 씬 생성 때마다 다시 만듦) → 렌더러 지면 45, 바닥 38, 벽 18, 캣워크 4, 트러스 6. 펜스는 정적 배칭에서 빼 GPU 인스턴싱. 합치기 전 9,847 → 합친 뒤 5,964(환경만).
- 기타: PrefabBounds가 FBX 루트 회전을 지우던 문제 수정, 작업등 강제 픽셀 3개 선택을 트러스 광원 10개 배치에 맞춤(남 -6 / 남 6 / 북 0), 제작 공정 갤러리 컨테이너 카드를 최종본 렌더(03_renders_final)로 교체, EnsureMeshLods에서 EnvFinal·ContainerFinal 제외, AutoTest가 Application.runInBackground = true(플레이어 설정이 false라 창이 배경이면 자동 검증이 멈췄음).
- 검증(환경 + 컨테이너 모두 반영한 씬):
  - 봇: HallQuiet 승리 182.2s 발각 0 / OfficeTools 승리 146.5s 발각 0 제압 1. 컨테이너 6개 겹침 0, 시작→데이터 코어 NavMesh 경로 Complete.
  - 카메라(같은 방법, 12,464회): 서기 0.8m 미만 4회(최소 0.65m, 이전 8회), 1.0m 미만 23회, 관통 0 / 웅크리기 0.8m 미만 0.
  - 드로우콜(6개 시점 합계): 4,170 → 6,323(+52%), SetPass 3,386 → 5,274, 그림자 캐스터 1,015 → 1,390, 삼각형 12.0M → 15.0M. 시점별 야드 1,791 / 홀 1,235 / 캣워크 884 / 사무 852 / 서버실 261 / 야드→홀 1,300.
  - Windows 빌드 375.7MB(+61.7MB), 오류 0·경고 0 → exe -autotest PASS: Guard_West 36.6m, Guard_Center 36.3m, Guard_Vault 31.9m, Guard_Yard 52.7m, Guard_Office 24.0m(합계 181.5m), Guard_Door 0m(고정 초소), NavMesh 오류 0·예외 0.
  - 스크린샷: Captures/p5_01_loading_yard, p5_02_main_hall, p5_03_catwalk, p5_04_office_corridor, p5_05_server_room_laser, p5_06_puzzle_keypad(.png, p4와 같은 시점).
- 알려진 점: 6m 홀 벽은 3m 모듈을 세로 2배로 늘려 콘크리트 띠가 1.7m로 높아짐 / 벽 두께 1m 박스는 골 깊이도 4배(약 9cm) / 상인방은 콘크리트 띠가 문 위 헤더처럼 보임 / 칸막이 끝면 콘크리트 텍스처가 늘어남 / 드로우콜이 늘었음(다음 후보: pixelLightCount 4→2, 펜스 변별 합치기) / 이전 버전 Models/Env/, Models/SP2/SP2_container_v2.fbx는 씬에서 안 쓰지만 지우지 않음.

## 2026-11-16(저녁) 드로우콜 추가 축소(사용자 승인)
- 6개 시점 합계, 같은 플레이 세션에서 단계별 측정(이번 세션 기준값 6,099 — 앞 측정 6,323과는 경비 위치 차이):
  | 단계 | 드로우 | SetPass | 그림자 캐스터 | 삼각형 |
  |---|---|---|---|---|
  | 전(환경·컨테이너 최종본) | 6,099 | 5,140 | 1,309 | 14.9M |
  | 펜스 12m 칸마다 합치기(16개) | 5,927 | 4,924 | 1,166 | 16.5M* |
  | pixelLightCount 4→2 | 4,711 | 3,714 | 1,166 | 13.4M |
  | 납작한 장식(높이 5cm 미만) 그림자 끔 | 4,721 | 3,722 | 1,051 | 13.4M |
  | 작은 장식 SmallDetail 층 30m 컬링 | **4,679** | **3,676** | **1,047** | 13.3M |
  (*펜스 합치기 단계는 납작 장식 그림자를 되돌려 켠 상태라 삼각형이 늘어 보임)
  - 전체 -23%(드로우)·-28%(SetPass)·-20%(그림자 캐스터). 시점별 최종: 야드 1,380 / 홀 899 / 캣워크 644 / 사무 604 / 서버실 192 / 야드→홀 960.
- 화면 비교(Captures/p6_cmp_*): pixelLightCount 2는 야드(컨테이너 앞 그늘이 아주 약간 어두워짐)·홀·사무 복도 모두 눈에 띄는 차이 없음 → 강제 픽셀 조명 조정은 하지 않음. 전후: p6_cmp_yard_before/after, p6_cmp_hall_before/after, 픽셀 조명만 비교: p6_cmp_*_pix2, p6_cmp_office_before/pix2.
- 코드: ShadowProtocolSceneSystems.ApplyPerformanceSettings(QualitySettings.pixelLightCount = 2 → Ultra 저장, 납작 장식 그림자 끔, Environment/ParkingLines·SecurityCameras·DecorProps를 SmallDetail 층(TagManager 8번, 없으면 만듦)), ThirdPersonCamera.ApplyLayerCulling(Start에서 SmallDetail 30m, 구형 거리), ShadowProtocolSceneEnvModules(펜스도 MergeUnder 12m).
- 펜스 합치기는 효과(-172 드로우, 그림자 캐스터 -143)에 비해 빌드 용량이 늘어남(펜스 약 53만 정점): 빌드 375.7 → 402.4MB. 용량이 문제면 MergeUnder 펜스 줄을 빼면 인스턴싱으로 돌아간다.
- Windows 빌드 402.4MB 오류 0·경고 0 → exe -autotest PASS(Guard_Office 24.0 / Vault 31.9 / Yard 52.7 / Center 36.2 / West 36.6m, 합계 181.4m, Guard_Door 고정 0m, NavMesh 오류 0·예외 0). 이후 같은 exe를 창 모드 1280×720으로 실행해 둠(사용자 확인용).

## 2026-11-16(밤) 인물 3명 + 애니메이션 18개 Blender 최종본 반영
- 가져오기: 새 메뉴 `ShadowProtocol/Import Characters Final (Blender)`(ShadowProtocolEnvImport.ImportCharacters) → Models/CharactersFinal/{female_agent,heavyguard_v2,supervisor_v2}_final.fbx(Humanoid, 아바타 CreateFromThisModel, Standard 4맵 재질 remap) + Animations/Final/<인물>@<클립>.fbx 18개.
- 아바타: 세 모델 모두 정상 Humanoid(22개 사람 뼈 대응, LeftUpperArm=LeftArm, Head=Head). T포즈에서 왼팔·왼손이 왼다리와 같은 쪽, 좌우 대칭(중무장 손 ±0.78m).
  - 애니메이션 아바타: 모델 아바타 복사(CopyFromOther)는 계층 불일치로 Unity가 거부 → 클립 파일 아마추어에서 만들되 사람 뼈 대응표는 모델과 같게 덮어씀. 자동 매핑이 중무장 경비병 클립 6개에서 Hips를 Root에 붙였던 것(경고 'Hips is inbetween…')을 이걸로 해결. 18개 모두 Humanoid 정상, 남은 경고는 반장 Run·HitFall의 'RightUpLeg 크기 애니메이션 버림'(무해).
  - **중무장 경비병 팔 확인**: Walk 재생 표본 12장에서 왼손·왼발 앞뒤 상관 -0.73(정상 반대 위상, 여성 -0.55·반장 -0.70과 같은 경향), 평균 좌우 위치 왼손 -0.28 / 오른손 +0.26, Talk의 손 들기 높이가 반장과 같은 쪽·같은 크기. 근접 캡처에서도 두 팔이 제자리.
- ShadowProtocolAnimBuilder 다시 씀: ClipDef가 <인물>@<클립>.fbx를 가리키고 (first, last, rotOffset) = (-1, -1, 0), loopPose 끔(이음매는 Blender에서 맞춤). 인물별 컨트롤러 3개: SP_PlayerAnimator(여성), SP_GuardAnimator(중무장), SP_GuardCaptainAnimator(반장, 새 파일). 블렌드 임계 = 실측 속도: 여성 걷기 0.89 / 달리기 3.80, 중무장 0.87 / 4.07, 반장 0.76 / 3.56 m/s(웅크려 걷기는 측정 신뢰가 낮아 1.0 유지). 던지기 놓는 시점은 새 클립 길이(3.30s)로 계산.
- 배치: 플레이어 = 여성 요원(키 1.705), 경비 반장 = Guard_Center(홀 중앙 순찰)·Guard_Office(사무동 감독관) 기존 'super' 초소, 중무장 경비병 = 나머지 Yard·West·Vault·Door. SetupAnimator가 모델 출처로 인물별 컨트롤러·달리기 속도를 고름. 제작 공정 갤러리: 인물 카드 3장(전후·근접·T포즈·피부 테스트·애니메이션 프레임 시트 6장씩, 중무장은 리그 수리 설명), 옛 SP2 경비 카드 2장은 뺌.
- 검증: 봇 HallQuiet 승리 180.0s 발각 0 / OfficeTools 승리 146.5s 발각 0 제압 1. 카메라(12,464회) 서기 0.8m 미만 4회(최소 0.65m)·관통 0, 웅크리기 0 — 이전과 같음(카메라 피벗 높이 1.6/1.05m 그대로, 여성 머리 뼈 1.52m라 화면 구도 이상 없음). 드로우콜 6개 시점 합계 4,954(이전 4,679, +275: 인물 메시 30k tris·스킨), SetPass 3,841, 그림자 캐스터 1,111. Windows 빌드 415.8MB 오류 0·경고 0 → exe -autotest PASS(Office 24.0 / Yard 52.7 / West 36.6 / Vault 32.1 / Center 36.3m, 합계 181.7m, Door 고정 0m, NavMesh 오류 0·예외 0). 같은 exe를 창 모드 1280×720으로 실행해 둠.
- 스크린샷: Captures/p7_01_loading_yard, p7_02_main_hall, p7_03_catwalk, p7_04_office_corridor, p7_05_server_room_laser, p7_06_puzzle_keypad, 인물 근접 p7_char_player, p7_char_heavyguard, p7_char_captain(.png).
- 남은 것: 소리 귀 확인, 얼굴 텍스처 해상도(VARCO 한계, 근접 컷신용이면 재투영 필요), 옛 모델(Models/SP_Player.fbx·SP_Guard.fbx·SP2_heavyguard/supervisor, Animations/Source/anim_*.fbx)은 씬에서 안 쓰지만 지우지 않음.

## 2026-11-16(밤) 플레이어 웅크려 걷기 → Adobe Mixamo "Crouched Walking" 교체(사용자 플레이 피드백)
- 파일: ArtSource/Mixamo/mixamo_crouched_walking.fbx → Assets/ShadowProtocol/Animations/Mixamo/(In Place, 30fps, 31프레임 = 1.033s). Humanoid, 아바타는 이 파일(mixamorig)에서 생성, Loop Time·Loop Pose 켬, 루트 회전·높이·XZ 모두 포즈에 굽기(회전 기준 Original). ShadowProtocolAnimBuilder: ClipDef(Player, "CrouchWalk")가 이 파일을 가리킴(MixamoCrouchWalkPath), Mixamo 클립은 뼈 대응표 덮어쓰기에서 제외.
- 속도: 여성 요원 모델에 표본 186장 — 접지(1cm) 발 속도 1.54 m/s, 보폭 기준 1.43 → 게임 웅크려 이동 1.5 m/s와 같아 PlayerCrouchWalkNatural = 1.5(최고 속도에서 걷기 클립만, 재생 배율 1.0).
- 회전 기준 결정: Mixamo 원본 자체가 골반·어깨가 진행 방향에서 약 42~46° 돌아간 채 걷는 클립(Generic으로 원본 커브 확인: 골반 yaw 41.5°, 디딤발은 정면 축으로 이동).
  - 'Body Orientation'으로 구우면 몸은 정면이지만 진행 축이 45° 틀어져 플레이에서 디딤발이 1.15 m/s 미끄러짐.
  - 'Original'로 구우면 디딤발 미끄러짐 0.24 m/s(바닥 6cm 이내 구간 평균)로 줄지만 몸이 진행 방향에서 약 45° 돌아간 '어깨 먼저' 걸음으로 보임 → 발 미끄러짐을 없애는 쪽(Original)을 택함.
- 플레이 확인(야드, Ctrl 웅크림 + 앞으로, 2.5s 3.6m = 1.5 m/s): 메시 최저 -0.3cm(바닥 뚫림 없음), 발끝 최저 3.3~3.6cm, 무릎 최저 0.48m, 팔은 몸 옆·앞(관통 없음). 웅크려 달리기는 없음(웅크린 상태에서 Shift 무시). 웅크려 서기(Blender, 골반 0.68~0.82m) ↔ 걷기(Mixamo, 0.89~1.00m) 전환은 속도 블렌드로 이어지나 걷기 시작 때 몸이 약 15cm 올라가고 45° 돌아감. 메시 꼭대기 1.58m라 환기구(높이 1.2m) 통과 때 머리가 상인방을 뚫어 보임(이전 VARCO 웅크려 걷기 1.49m도 같은 문제).
- 접촉 시트: Captures/p8_crouchwalk_sheet.png(1행 웅크려 서기→걷기 시작 옆모습, 2행 걷기 한 주기 옆모습, 3행 앞 45° 걷기 4장 + 멈춤 2장).
- 표기: 크레딧(GameUI.CreditLine·크레딧 화면)과 제작 공정 갤러리 플레이어 카드에 "플레이어 웅크려 걷기 애니메이션: Adobe Mixamo" 추가. 크레딧의 옛 문구(환경은 기본 도형·VARCO 모듈 마무리 중, 애니메이션 9종 VARCO Animate)도 현재 사실(환경·인물 Blender 최종본, 애니메이션 VARCO Animate → Blender)로 고침. 갤러리 플레이어 카드에서 Blender 웅크려 걷기 프레임 시트는 뺌.
- Build Demo Scene → Windows 빌드 415.0MB 오류 0·경고 0 → exe -autotest PASS(Center 36.3 / West 36.6 / Office 24.0 / Vault 31.9 / Yard 52.7m, 합계 181.5m, Door 고정 0m, NavMesh 오류 0·예외 0). 같은 exe를 창 모드 1280×720으로 실행해 맨 앞으로 띄움.
- 남은 것: 몸이 45° 돌아간 걸음이 마음에 들지 않으면 정면을 보고 걷는 다른 Mixamo 클립(예: Sneak Walk 계열)으로 바꾸거나 Blender에서 골반·상체 방향을 정면으로 고친 뒤 발을 IK로 다시 붙여야 함. 웅크린 키(1.58m)와 환기구 1.2m 불일치.

## 2026-11-16(밤) 웅크린 대기·걷기 → Mixamo "Crouching Idle" + "Crouch Walk Forward"로 다시 교체
- 파일: Animations/Mixamo/mixamo_crouch_walk_forward.fbx(34프레임 1.133s, In Place), mixamo_crouching_idle.fbx(76프레임 2.5s). 옛 mixamo_crouched_walking.fbx는 프로젝트에서 지움(ArtSource 원본은 남김).
- 가져오기: Humanoid(아바타는 각 파일 mixamorig에서 생성, 정상), Loop Time·Loop Pose 켬, 루트 회전 Body Orientation, 높이 Feet 기준 + 오프셋(대기 -0.175m, 걷기 -0.10m: 발 기준만으로는 대기 17.5cm·걷기 10cm 바닥에 묻혀서 Unity 'Offset'으로 올림, +는 몸을 내리는 방향), XZ 고정. ShadowProtocolAnimBuilder: Player CrouchIdle·CrouchWalk가 Mixamo 두 파일을 가리킴(ClipDef.heightOffset 추가).
- 플레이 측정(야드, 웅크림 + 앞으로 3s, 4.5m = 1.5 m/s):
  - 몸 방향: Body Orientation으로 어깨선이 진행 방향과 -0.3° → 몸은 정면을 봄.
  - **발 미끄러짐: 디딤발이 옆(X)으로 1.375 m/s 미끄러짐(앞뒤 성분 -0.017로 속도 상수 1.5는 맞음).** 원인: 두 Mixamo 파일 모두 원본(Generic으로 커브 확인)에서 골반선 38°·어깨선 53°·발끝 35°가 돌아간 채 디딤발은 정면 축으로 이동 — 내려받을 때 쓴 Mixamo 캐릭터의 휴식 자세가 약 34~40° 돌아가 있었던 것으로 보임(두 파일 휴식 자세 골반선이 모두 34° 돌아감). 몸을 정면으로 돌리면 걸음이 옆으로 비껴 나가고, Original로 두면 발은 붙지만 몸이 약 50° 돌아감(지난번 문제). Unity 안에서 아바타 기준 자세를 T포즈로 바로잡아 봐도 같아서(원본 데이터 문제) 되돌림.
  - 바닥: 메시 최저 걷기 +0.1cm·대기 +0.5cm(뚫림·뜸 없음), 발끝 최저 약 5cm.
  - 높이: 웅크린 대기 골반 0.69m·머리 꼭대기 1.15m, 걷기 골반 0.96m·꼭대기 1.54m → 걷기 시작 때 0.3s 동안 골반이 약 27cm 올라감(두 클립의 웅크림 깊이 차이). 환기구(1.2m): 대기 자세(1.15m)는 들어가지만 걷기(1.54m)는 머리가 상인방을 뚫음.
  - 팔: 걷기 클립 자체가 한 팔을 얼굴 앞에 들고 다른 팔을 앞으로 뻗은 자세(원본 그대로).
- 접촉 시트: Captures/p9_crouch_sheet.png(1행 대기→걷기 시작 옆모습, 2행 걷기 한 주기 옆모습, 3행 정면 걷기 4장 + 멈춤 2장).
- 표기: 크레딧·갤러리 문구를 "플레이어 웅크린 대기·걷기 애니메이션: Adobe Mixamo"로, 갤러리 플레이어 카드에서 Blender 웅크린 대기 프레임 시트도 뺌.
- Build Demo Scene → Windows 빌드 414.1MB 오류 0·경고 0 → exe -autotest PASS(Yard 52.7 / West 36.6 / Center 36.3 / Vault 31.9 / Office 24.0m, 합계 181.5m, Door 고정 0m, NavMesh 오류 0·예외 0). 창 모드 1280×720으로 실행해 맨 앞에 띄움.
- 남은 것(권장): Mixamo 기본 캐릭터(Y Bot/X Bot)로 같은 두 동작을 다시 내려받으면 몸·걸음 방향이 맞을 가능성이 큼. 또는 Blender에서 골반 방향을 정면으로 돌리고 발을 IK로 다시 붙이기. 대기↔걷기 높이 차(27cm)는 웅크림 깊이가 비슷한 클립 짝이 필요.

## 2026-11-17 웅크린 대기·걷기: Mixamo 원본 → Blender 보정본으로 교체, 웅크림 속도 1.05 m/s
- 클립: ArtSource/Characters/05_final_export/anims/female_agent@CrouchWalk.fbx(35프레임 1.133s)·@CrouchIdle.fbx(76프레임 2.5s) — Blender에서 Mixamo 원본의 몸 비틀림 제거·웅크림 깊이 맞춤·팔 내림·발 고정. `Import Characters Final (Blender)`로 다시 가져옴. ClipDef (-1,-1,0), 루트 회전 Original. Animations/Mixamo/ 원본 클립은 프로젝트에서 지움(ArtSource 원본 유지).
- 속도: PlayerController.crouchSpeed 1.5 → 1.05 m/s(사용자 결정), 블렌드 트리 웅크려 걷기 임계값 1.04(Blender 실측).
- Unity에서 추가로 맞춘 것:
  - Foot IK(플레이어 Locomotion 상태 iKOnFeet): 깊은 웅크림이 Humanoid 근육 범위에 걸려 발이 약 9cm 묻히고 디딤발이 0.63 m/s 미끄러졌음 → 켠 뒤 디딤발 0.017 m/s, 발 바닥 높이 정상.
  - 높이 Offset(+ = 몸을 내림): 대기 0.04m, 걷기 0.06m — Unity Humanoid에서 골반이 Blender 실측(0.70/0.71m)보다 4~8cm 높게 나와 맞춤.
  - 환기구·덕트 높이 1.2 → 1.35m(LevelLayout DuctH, 덕트 환풍기 충돌 상자·날개·그릴도 맞춤): 1.2m에서는 웅크려 걷는 모델의 헬멧 꼭대기(1.28~1.30m)가 상인방을 뚫었음(Blender 1.14~1.18m는 머리 뼈 기준, Unity 머리 뼈 1.11~1.13m). 서면(1.8m)·경비(1.9m)는 여전히 통과 못 함.
- 플레이 확인(야드, 웅크림 + 앞으로 3.1s 3.25m = 1.05 m/s): 디딤발 미끄러짐 0.017 m/s, 어깨선이 진행 방향과 4.9°(정면), 대기↔걷기 골반 0.68~0.75m(들썩임 7cm 이내, 이전 27cm), 대기 꼭대기 1.19m·걷기 1.30m, 메시 최저 대기 -4.8cm·걷기 -8.5cm(발끝이 구를 때 잠깐 묻힘, 발끝 뼈 최저 -5.3cm). 환기구 통과 시험: 상인방 아래 87프레임 동안 메시 최고 1.30m < 1.35m, 뚫림 0프레임, 통과 성공.
- 봇(속도 변경 후): HallQuiet 승리 249.2s(이전 180.0s) 발각 0 / OfficeTools 승리 203.9s(이전 146.5s) 발각 0 제압 1. 첫 HallQuiet 시도는 125.8s에 홀 감독관(경비 반장)에게 들켜 실패 — 느려진 봇이 감독관 순찰 끝점(9.5,-9.5) 옆(10.2,-10.2)에서 기다리다 들킴(봇이 '지금 자리'를 안전 지점으로 기록해 물러날 곳이 없었음). 봇 수정: 물러날 곳을 한 칸 전 안전 지점(prevSafe)까지 두게 함(ShadowProtocolBot). 재시도에서 승리.
- 접촉 시트: Captures/p10_crouch_sheet.png(1행 대기→걷기 시작 옆모습, 2행 걷기 한 주기 옆모습, 3행 정면 걷기 4장 + 멈춤 2장).
- 표기: 크레딧·갤러리 "플레이어 웅크린 대기·걷기 애니메이션: Adobe Mixamo 원본 → Blender 보정", 갤러리 플레이어 카드에 보정 전후 시트(female_agent_crouch_mixamo_fix.png) 추가.
- Build Demo Scene → Windows 빌드 415.6MB 오류 0·경고 0 → exe -autotest PASS(Office 24.0 / Vault 31.9 / Center 36.3 / Yard 52.7 / West 36.6m, 합계 181.5m, Door 고정 0m, NavMesh 오류 0·예외 0). 창 모드 1280×720으로 실행해 맨 앞에 띄움.
- **(같은 날 지시 변경)** 사용자: 1.05 m/s는 너무 느림 → PlayerController.crouchSpeed를 1.5로 되돌림(씬 다시 만듦, 씬 값 1.5 확인). 지금 웅크려 걷기 클립(실측 1.04)은 1.5에서 재생 배율 1.44(MoveScale)로 보폭을 맞춰 돌아간다. 1.5 m/s 자연 속도로 다시 만든 CrouchWalk가 Blender에서 오면 PlayerCrouchWalkNatural을 그 실측값으로 바꾸고 봇·빌드·exe 실행을 다시 한다. 위 봇 결과(249.2s/203.9s)와 빌드 415.6MB·exe 실행(PID 47804)은 1.05 m/s 기준이라 무효 — Windows 빌드·화면 실행은 새 클립이 올 때까지 보류. CrouchIdle 교체, Foot IK·높이 보정, 환기구·덕트 1.35m, 크레딧·갤러리 문구, 봇 물러나기 개선은 그대로 유지.

## 2026-11-23 웅크려 걷기 1.5 m/s용 새 클립 반영(Blender 보정본 2차)
- 클립: female_agent@CrouchWalk.fbx(1.5 m/s용으로 다시 만든 25프레임 루프, 0.833s, Blender 실측 1.47 m/s) + @CrouchIdle.fbx(골반 0.718m로 맞춤). `Import Characters Final (Blender)`로 다시 가져옴, 아바타 정상, ClipDef (-1,-1,0)·루트 회전 Original.
- 속도: PlayerController.crouchSpeed 1.5(유지), PlayerCrouchWalkNatural 1.47 → 최고 속도에서 재생 배율 1.02(MoveScale 실측 평균 1.02, 상한 1.8).
- Foot IK·높이 보정 재검토: 그대로 유지(Foot IK 켬, Offset 대기 0.04 / 걷기 0.06). 대기 골반 0.72m로 Blender 0.718m와 일치.
- 플레이 확인(야드, 웅크림 + 앞으로 3.1s 4.65m = 1.5 m/s):
  - 디딤발: 발목 기준 접지 구간 평균 속도 왼발 -0.12 / 오른발 +0.09 m/s(앞뒤로 상쇄, 미끄러짐 거의 없음). 발끝 기준 값(0.52)은 발끝이 구르는 동작이 섞여 과대 — 발목 기준으로 판정.
  - 몸 방향: 어깨선이 진행 방향과 3.7°(정면).
  - 대기↔걷기: 골반 0.72 → 걷기 평균 0.75m, 전환 중 0.70~0.79m(눈에 띄는 들썩임 없음).
  - 높이: 대기 메시 꼭대기 1.21m, 걷기 1.34m, 머리 뼈 1.16m.
  - 환기구(1.35m) 통과 시험: 상인방 아래 61프레임 동안 메시 최고 1.337m, 뚫림 0프레임, 통과 성공(여유 1.3cm로 빠듯함).
  - 접촉 시트: Captures/p11_crouch_sheet.png(1행 대기→걷기 시작 옆모습, 2행 걷기 한 주기 옆모습, 3행 정면 걷기 4장 + 멈춤 2장).
- 봇: HallQuiet 승리 185.2s 발각 0 / OfficeTools 승리 146.5s 발각 0 제압 1(에디터 재시작 뒤 이어서 실행).
- Windows 빌드 415.6MB 오류 0·경고 0 → exe -autotest PASS(Vault 31.9 / Yard 52.7 / Center 36.3 / Office 24.0 / West 36.6m, 합계 181.5m, Door 고정 0m, NavMesh 오류 0·예외 0). 창 모드 1280×720으로 실행해 맨 앞에 띄움(PID 7672).
- 남은 것: 환기구 여유가 1.3cm라 클립이나 모델 키가 조금만 바뀌어도 다시 확인 필요(DuctH 1.40으로 올리면 넉넉함).

## 2026-11-23 웅크림 v3 + 플레이어 애니메이션 Generic 전환(Blender 자세 그대로 재현)
- 클립: female_agent@CrouchWalk(20프레임 루프 0.667s, 자연 속도 1.25 m/s, 보폭 0.417m, 180걸음/분) + @CrouchIdle 다시 가져옴. 웅크림 이동 속도 1.25(사용자 승인), 웅크려 걷기 기준 속도 1.25.
- 자세 측정 도구: Scripts/Editor/ShadowProtocolPoseProbe.cs(Blender intended_pose와 같은 항목: 골반 높이, 골반→목 기울기, 위팔-몸통 각, 팔꿈치 굽힘, 손 앞·높이, 발 간격, 메시 꼭대기). 원본 커브를 Generic으로 샘플하면 Blender 값과 일치(골반 0.624/0.620, 기울기 41.9/45.4°, 팔꿈치 40.1°)해 측정 정의가 맞음을 확인.
- 원인: Humanoid 리타깃. Humanoid(높이 보정 0.04/0.06 + Foot IK)로 재생하면 —

  | 항목 | Blender 걷기 | Unity Humanoid(전) | Unity Generic(후) | Blender 대기 | Humanoid(전) | Generic(후) |
  |---|---|---|---|---|---|---|
  | 골반 높이 | 0.624m | 0.607 | **0.626** | 0.620 | 0.592 | **0.622** |
  | 몸통 기울기 | 42.1° | 31.4 | **41.9** | 45.4° | 40.2 | **45.2** |
  | 머리 꼭대기 | 1.17~1.20 | 1.22 | **1.19** | 1.14 | 1.15 | **1.14** |
  | 어깨(L/R) | 14.6/8.3° | 22.4/14.5 | **16.8/9.9** | 11.5/13.5 | 6.2/7.1 | **13.8/14.7** |
  | 팔꿈치 | 40.1° | 46.9/47.7 | **40.1** | 40.1 | 46.7/48.7 | **40.1** |
  | 손 앞/높이 | 15cm / 0.39~0.45 | 9.2 / 0.43 | **15.2 / 0.39** | 14~15cm | 11.9 / 0.38 | **14.6 / 0.36** |
  | 발 간격 | 16.4cm | 16.9 | **16.6** | 24.2 | 23.5 | **24.4** |

  Humanoid는 몸통이 10°(걷기)·5°(대기) 덜 숙고, 팔꿈치가 7° 더 펴지고 어깨가 7° 덜 벌어져 사용자 캡처처럼 '몸이 서고 손이 무릎에 박히는' 모습. 높이 보정·Foot IK·근육 범위를 고쳐도 몸통·팔 각도는 못 맞춰 플레이어만 Generic으로 바꿈.
- 바꾼 것:
  - ShadowProtocolAnimBuilder: PlayerGeneric=true. 플레이어 클립 6개를 Generic(아바타 없음)으로 가져오고, 아마추어 기준 경로를 플레이어 모델 루트 기준(female_agent_final/…)으로 옮긴 .anim을 Animations/PlayerGeneric/에 생성(커브 값 그대로 복사). 던지기 상체 레이어는 척추 아래 뼈만 켠 Transform 마스크(SP_PlayerUpperBody_Generic.mask). 플레이어 Foot IK 끔, 예전 Humanoid용 높이 보정(0.04/0.06) 뺌. 경비 2명은 Humanoid 그대로.
  - SetupAnimator: 플레이어 Animator 아바타 제거(null). CharacterProceduralPose: Generic일 때 목·머리·가슴 뼈를 이름으로 찾음. PlayerTools: 던지는 손을 이름(RightHand)으로 찾음. MakingOfGallery: Generic 컨트롤러면 턴테이블 모델의 아바타를 뗌.
- 플레이 확인(Generic, 야드):
  - 골반 높이 Unity 대기 0.622m(Blender 0.620, +0.2cm) / 걷기 0.626m(Blender 0.624, +0.2cm) — ±2cm 안.
  - 몸 방향: 어깨선이 진행 방향과 2.1°. 대기→걷기 전환 골반 0.616~0.634m(들썩임 없음).
  - 환기구(1.35m): 상인방 아래 73프레임 메시 최고 1.240m, 뚫림 0, 통과(여유 11cm).
  - 디딤발(발 중간점, 세상 기준): 왼발 0.01 m/s(붙음), **오른발 0.41 m/s 앞으로 미끄러짐** — Generic은 Blender 커브 그대로라 클립 자체의 좌우 차이(오른발 접지 구간이 왼발보다 짧게 뒤로 감)로 판단, Blender 수정 필요.
  - **팔이 허벅지를 스침**: 웅크린 대기에서 손·아래팔이 허벅지 뼈대에서 6.5~6.9cm(허벅지 두께 안쪽) — 클립 자세 문제라 Blender에서 손을 3~5cm 바깥·앞으로 빼야 함.
  - 비교 캡처(사용자 캡처와 같은 각도): Captures/p12_crouch_idle_front_before_humanoid.png(전) / p12_crouch_idle_front_after_generic.png(후), 접촉 시트 Captures/p12_crouch_sheet.png(옆·정면·전환).
- 봇(1.25 m/s): HallQuiet 승리 211.1s 발각 0 / OfficeTools 처음 두 번은 129.1s에 홀 동쪽 경비에게 들킴(기절시킨 감독관에게 무전 확인하러 온 경비와 서버실 문 앞에서 마주침) → 봇 경로 수정: 서버실 안(레이저 바깥쪽)에서 그 경비가 순찰로 돌아갈 때까지 기다리게 함(ShadowProtocolBotRoutes.VaultAndBack waitBeforeExit) → 승리 198.1s 발각 0 제압 1(10.5s 대기).
- Windows 빌드·화면 실행은 다음 Blender 수정본이 올 때까지 보류(지시).

## 2026-11-23 웅크림 6차(Blender 6차 클립, Generic, 1.20 m/s)
- Import Characters Final (Blender) → PlayerGeneric 클립 재생성 → Build Demo Scene. PlayerController.crouchSpeed = 1.20, AnimBuilder PlayerCrouchWalkNatural = 1.20(Blender 실측 보폭 0.48m × 150걸음/분).
- ShadowProtocolPoseProbe 확장: 손-무릎(앞/위/바깥) 측정, 팔-다리 메시 틈(팔 정점 ↔ 가장 가까운 허벅지·정강이 정점, 다리 법선 기준 음수 = 파고듦, -0.3cm 넘으면 파고든 프레임).
- 플레이 측정(Unity Generic vs Blender intended_pose):

  | 항목 | Unity 걷기 | Blender 걷기 | Unity 대기 | Blender 대기 |
  |---|---|---|---|---|
  | 골반 | 0.601m | 0.599 | 0.583 | 0.580 |
  | 몸통 기울기 | 37.2° | 37.5 | 37.5 | 37.5 |
  | 머리 꼭대기 | 1.19 | 1.17~1.19 | 1.13 | 1.13 |
  | 어깨 굽힘 L/R | 63.6/44.0° | 65.2/43.6 | 34.6/41.5 | 36/41 |
  | 팔꿈치 L/R | 50.0/55.0° | 50/55 | 46.1/55.0 | 46.1/55 |
  | 손-무릎 L 앞/위/바깥 | 21.6/11.6/-1.9cm | 22.5/11.9/-2.8 | 12.6/16.5/-2.0 | 같음 |
  | 손-무릎 R 앞/위/바깥 | 16.1/17.0/0.4cm | 15.1/16.6/1.2 | 14.2/11.5/-7.3 | 같음 |
  | 발 간격 | 17.0cm | 16.4 | 24.2 | 24.2 |
  | 팔-다리 틈 | 평균 1.82cm(최소 -0.65) | — | 1.41(최소 1.34) | — |
  | 파고든 프레임 | 1/32 | 0 | 0/20 | 0 |

  모두 ±2cm/±3° 안(어깨 굽힘 L 걷기 -1.6°, 대기 -1.4°). 걷기 32프레임 중 1프레임만 손이 무릎에 0.65cm 스침(Blender 0과의 차이는 평가 시점 차), 대기는 관통 없음. 대기→걷기 전환 골반 0.583~0.611m(튐 없음), 어깨선-진행 방향 0.1°.
- 디딤발 미끄러짐(접지 구간별): 오른발 0.01~0.02 m/s(붙음), **왼발 0.13 m/s**(몸 기준 발 속도 1.32 vs 플레이어 1.19 → 왼발 접지 구간이 약 10% 길게 뒤로 감). 눈에 띄는 수준은 아니나 Blender 왼발 접지 커브 확인 여지.
- 캡처: Captures/p13_crouch_front.png(사용자 캡처와 같은 정면 각도, 대기), p13_crouch_behind.png(게임 카메라 뒤, 대기·걷기), p13_crouch_sheet.png(옆 전환·옆 걷기 한 주기·정면 걷기·정지).
- 환기구(1.35m): 상인방 아래 76프레임 메시 최고 1.233m, 뚫림 0, 통과(여유 11.7cm).
- 봇(1.20 m/s): HallQuiet 승리 246.0s 발각 0 / OfficeTools 승리 199.2s 발각 0 제압 1.
- Windows 빌드 415.8MB 오류 0 경고 0 → exe 자동 시험 PASS(경비 6/6 NavMesh 위, 순찰 5/5 이동, 합계 181.4m, 오류·예외 0) → 창 모드 실행·맨 앞(PID 21120). 콘솔 오류 0.

## 2026-11-23 HDRP 전환 + 모션 사실화(사용자 지시: "모션하고 모든 것을 사실주의적으로")
- 렌더: 빌트인·Gamma → **HDRP 17.6 + Linear**(사용자 선택). 자산 Rendering/SP_HDRenderPipeline.asset(디퍼드, SSAO·SSR·SSGI·볼류메트릭·접촉 그림자·라이트 레이어, 그림자 아틀라스 8192), 볼륨 Rendering/SP_NightVolumeProfile.asset.
  - 구조: 빌더는 지금처럼 Standard 재질·빌트인 조명으로 씬을 만들고, Build() 끝의 `ShadowProtocolHDRP.ApplyToScene()`(Scripts/Editor/ShadowProtocolHDRP.cs)이 한꺼번에 HDRP로 바꾼다. 메뉴 `ShadowProtocol/HDRP/Setup Pipeline`, `.../Apply To Open Scene`.
  - 재질: 씬이 참조하는 Standard → HDRP/Lit(금속·AO·매끈함을 `<재질>_HDRPMask.png` 마스크 맵으로 구움, 컷아웃·투명 유지, 발광은 nits). 표시등 발광 = 예전 세기 × 12 nits(SPRender.NitsPerEmissionUnit), 가로등·투광등 렌즈 4000 nits, 시작 표시 판 0.15 nits(도색처럼). 빌더 LitShader()는 항상 Standard(변환은 마지막 단계에서).
  - 조명(물리 단위): 달빛 0.6 lux(그림자), 가로등 = 아래로 비추는 스포트 135° 820 cd(그림자 1024), 홀 투광등 120° 1550 cd(그림자 1024), 사무 천장등 150° 500 cd(그림자 512), 데이터 코어 30 cd, 탈출 비컨 40 cd, 그 밖의 작은 표시등은 빌트인 강도에서 환산.
  - 볼륨: 그라디언트 밤하늘(지평선 빛공해), 볼류메트릭 안개(평균 자유 경로 90m), 자동 노출(히스토그램, EV -1~7, 보정 +0.4, 밝음→어둠 적응 느리게), ACES, 블룸 0.22, SSAO, SSR, SSGI, 접촉 그림자, 모션 블러 0.35(카메라 이동 블러는 거의 0으로 제한 — 타이틀→게임 순간 이동 번짐 방지), 비네트·필름 그레인·색수차 약하게. 카메라 TAA.
  - **인물만 보이게(사용자 지시)**: 배경 노출은 어둡게 두고, 플레이어·경비 모델만 렌더링 레이어 1번(인물)에 넣어 인물만 비추는 그림자 없는 보조광(플레이어 PlayerFillLight 16 cd, 경비 CharacterFill 12 cd)을 붙임. 바닥·벽·안개에는 닿지 않음.
  - 런타임 색 변경: SPRender(Scripts/SPRender.cs)로 _BaseColor/_EmissiveColor와 _Color/_EmissionColor를 함께 처리 — BreakerPanel, SecurityDoor, GuardStatusIndicator, CameraFadeable(투명 페이드는 _BaseColor 알파). FadeCopy는 이미 HDRP인 재질도 투명 복사본을 만든다. 갤러리 턴테이블: HDRP 카메라(단색 배경)·칸델라 조명, 와이어프레임은 RenderPipelineManager 이벤트로.
  - 주의: Unity API 업데이터가 폐기 API(hd.shapeRadius)를 `legacyLight.shapeRadius`로 잘못 바꿔 컴파일 오류를 낸 적 있음 → `l.shapeRadius`(Light)로 고침.
- **경비 시야 원뿔 숨김(사용자 지시)**: 빌더가 VisionCone 오브젝트를 꺼진 채 만든다(표시 전용이라 감지 판정 GuardAI는 그대로). 작전 설명·튜토리얼·크레딧 문구에서 "바닥 부채꼴" 언급 제거. 다시 보이려면 BuildGuard의 `coneGO.SetActive(false)` 줄을 지움.
- 모션:
  - 플레이어 관성(PlayerController): 가속 걷기 8 / 달리기 10 / 웅크림 6 m/s², 제동 11 m/s², 몸은 실제 진행 방향으로 회전(걷기 560 → 달리기 340°/s, 달릴수록 회전 반경 큼), 벽에 막힌 속도는 버림(벽 따라 미끄러짐), 순간 이동 시 관성 초기화. PlanarVelocity 공개.
    - 측정(ShadowProtocolMotionProbe): 0→2.4 m/s 약 0.25s, 반대로 꺾기 = 0.25s 제동 후 0.36s에 180° 돌며 재가속, 달리기 중 90° 꺾기 5.0→3.55 m/s로 줄며 약 0.7s 호, 달리기 정지 약 0.3s.
  - 경비(GuardLocomotion, 새 파일): NavMeshAgent는 경로만(updatePosition·updateRotation 끔), 몸이 향한 방향과 경로 차이가 80° 이상이면 제자리에서 먼저 돌고 20° 이하에서 제 속도. 가속 순찰 2.8 / 추격 6.5, 감속 4.5 m/s², 회전 250~400°/s. Warp는 감지해 몸을 옮김. 측정: 순찰 0→1.9 m/s 약 0.7s, 걷는 중 몸–진행 방향 차 0°.
  - 몸 기울기(CharacterProceduralPose 보조 모드, 플레이어·경비 공통): 회전 시 안쪽으로(원심 기울기의 절반, 최대 9°), 가속 시 앞으로(1.1°/m/s², 최대 6°) — 척추 60%·가슴 40%.
  - 카메라(ThirdPersonCamera.ApplyHandheld): 걸음 수에 맞춘 상하(걷기 1.2 / 달리기 3 / 웅크림 0.8cm)·좌우 흔들림, 손에 든 카메라 같은 느린 떨림(0.15~0.3°, 달리기 1.6배, 조준 시 절반). `handheld`로 끔.
- 측정 도구: Scripts/Editor/ShadowProtocolMotionProbe.cs — 에디터 창이 포커스를 잃으면 플레이 모드 프레임이 멈추므로 봇처럼 EditorApplication.Step()으로 진행(PauseAtFrame으로 멈춰 스크린샷).
- 캡처: Captures/p14_before_hdrp_start(전), p14_hdrp_play_title, p14_hdrp_play_start2, p14_hdrp_walk_yard, p14_char_light_run(인물 보조광).

## 2026-11-23(오후) 어쌔신 크리드 참고 — 웅크린 이동 비교 + 액션 요소(사용자 지시)
- 참고 영상: YouTube "Assassin's Creed Black Flag Resynced - Crouch Detection 4K!"(내장 브라우저로 프레임 확인). 관찰: 상체 40~50° 숙임, 팔은 힘 빼고 무릎 근처로 늘어뜨림, 몸은 진행 방향 정면, 카메라 어깨 뒤 약 2.5m·낮게, 암살 때 낮은 클로즈업.
- 비교(측정): 몸 방향–진행 방향 0°, 어깨선 약 3° → **이동 방향은 맞음**. 상체 37°, 카메라 머리에서 4.4m·높이 2.1m(멀고 높음).
- 바꾼 것(사용자 결정: 네 가지 모두 + 웅크린 자세 AC처럼):
  - 웅크린 자세 보정(CharacterProceduralPose.ApplyStealthCrouch, Blender 클립 위에 덧입힘): 상체 +8°(측정 43°), 위팔 뒤로 14°, 팔꿈치 22° 펴기, 고개 8° 들기 → 손 높이 약 42cm(무릎 근처).
  - AC식 카메라(ThirdPersonCamera): 웅크리면 거리 ×0.62(4m→약 2.5m, 측정 머리–카메라 2.9m·높이 1.7m), 어깨 오프셋 0.6m, 달리면 ×1.12. Focus(대상 바라보기)·Shake(충격)·CinematicShot(지정 샷) API.
  - ActionDirector(Scripts/ActionDirector.cs, 게임 시작 때 스스로 생김): 슬로모션(일시정지·결과 화면에선 시간 안 건드림, fixedDeltaTime도 맞춤). 들킨 순간(경비가 보고 경보 전환) = 0.45s 시간 배율 0.3 + 카메라가 그 경비 쪽으로 + 흔들림 + 붉은 비네트·색수차 펄스(5초 쿨다운). 암살 = 0.7s 배율 0.35 + 어깨 뒤 낮은 클로즈업(충돌체·렌더러 가림 검사로 자리 고름) + 채도 -50 색감.
  - 암살(PlayerTools.TakedownRoutine): 판정(기절)은 바로, 경비 등 뒤 0.55m로 0.2s 돌진 → 팔 감아 내려치기 + 타격음·흔들림 → 경비 쓰러짐(GuardAI.FallDelay/ShowDown으로 0.26s 늦춤, 머리 위 Zz도 그때) → 반 걸음 물러남. 연출 중 조작 잠금.
  - 들켜도 추격·도주: 규칙은 원래 있었음(경보 경비가 1.2m 안으로 붙어야 잡힘, 2.5s 시야 놓치면 수색). 들킨 순간 연출을 붙여 체감되게 함.
  - 뛰어넘기·슬라이딩(Scripts/ParkourController.cs, Space 하나): 앞 1.1m 안 높이 0.45~1.25m·깊이 1.8m 이하 장애물 + 넘은 자리 비어 있으면 뛰어넘기(0.5s 달리기/0.65s 걷기, 베지어 궤적, 착지 소음 5m), 아니면 달리는 중 슬라이딩(0.8s, 4.5→1.3 m/s, 웅크린 키로 낮은 틈 통과, 소음 6m). 맵의 대상: 상자 1.2m·팔레트 1.1m·드럼 0.9m 13곳. **전용 클립 없음** → 웅크림 다리 + 절차적 상체(vaultWeight: 왼손 앞으로·몸 옆 기울기 / slideWeight: 상체 젖힘·오른팔 앞으로·몸 낮춤). 클립이 오면 ParkourController의 Vault/Slide에서 Animator 트리거로 교체. 안내 문구 "[Space] 뛰어넘기", 조작 설명·튜토리얼에 추가.
  - PlayerController: ForceCrouch, MotionLocked(파쿠르가 이동할 때), 걷기·웅크림 제동 16 m/s²(정지 거리 약 18cm — 레이저 사이 정확히 서기), 달리기 제동 11.
- 봇 수정: 후퇴 중 검사 사이에도 계속 이동(관성 때문에 한 프레임 입력으로는 출발 못 함), 목표 40cm 안에서 입력 줄여 감속, 경비 예측에 GuardLocomotion의 출발·정지(0.55s)·모퉁이 회전(250°/s) 시간 반영.
- 캡처: Captures/p15_crouch_game_cam·side·front(전), p15_crouch_ac_game_cam·side(후), p15_takedown_b~e(암살 연출), p15_detected(들킨 순간), p15_vault_mid·side(뛰어넘기).
- 검증(에디터 플레이, 씬 다시 빌드 후): **OfficeTools 승리 206.3s 발각 0 제압 1**(새 암살 연출 포함). HallQuiet는 아직 실패 — 3번 시도에서 단계 9→20→35/46까지 진전(마지막: 데이터 코어 확보 후 귀환 중 155s 홀 동쪽 경비에게 들킴). 원인은 봇의 경비 예측이 새 경비 이동(GuardLocomotion) 타이밍과 아직 어긋나는 것으로 봄(게임 규칙 문제 아님, 확인 필요).
- 런타임 예외 0, 컴파일 오류 0. **Windows 빌드·exe 자동 시험은 이번에 하지 않음**(HDRP 셰이더 컴파일로 오래 걸림 — 다음에).
- 남은 것: HallQuiet 봇 통과, Windows 빌드 + exe -autotest, 뛰어넘기·슬라이딩 전용 클립(Mixamo 등에서 받아 Blender 보정 → Generic), 시작 표시 판(StartPad)이 HDRP에서 아직 밝은 파랑으로 보임(색 낮추기).

## 2026-11-23(오후) HallQuiet 봇 통과 + Windows 빌드 검증
- HallQuiet 실패 원인 2가지(봇 문제, 게임 규칙은 그대로):
  1) 경비가 모퉁이에서 돌아서는 동안 시선이 옛 방향→새 방향을 훑는데 예측은 이미 새 방향을 본다고 가정 → Walk가 회전 구간에서 멈추면 가운데 방향 + 회전량 절반만큼 시야를 넓힘(WalkSweep).
  2) 정문 키패드: 도착 뒤 5초 머무는 검사를 웅크림(시야 ×0.6)으로 했는데 실제로는 서서 조작 → PathSafe(standAtEnd)·Move(standAfter) 추가, "front keypad"에 적용.
- 결과(에디터): **HallQuiet 승리 235.1s 발각 0 제압 0**, **OfficeTools 승리 206.3s 발각 0 제압 1**(두 번 모두 같은 결과).
- 시작 표시 판(StartPad) 색 (0.2,0.6,1) → (0.06,0.16,0.30)(HDRP에서 밝은 판이 튀었음).
- **Windows 빌드 성공 436.8MB 오류 0 경고 20**(경비 반장 클립 가져오기 경고·폐기 API 알림, 466초) → exe -autotest **PASS**: 경비 6/6 NavMesh 위, 순찰 5/5 이동(Center 35.1 / Vault 29.0 / Office 21.6 / West 34.2 / Yard 50.5m, 합계 170.4m — 이전 181.4m보다 약 6% 적음 = GuardLocomotion 출발 가속·모퉁이 회전), NavMesh 오류 0·예외 0, 셰이더 오류 없음(D3D12, RTX 3060 Ti). 로그 Logs/autotest_p15.log.
- 참고: 이 프로젝트의 에디터 로그는 Logs/Editor.log(사용자 폴더 Editor.log 아님).

## 2026-11-23(저녁) Mixamo 파쿠르 클립 + AC2·AC3 참고 움직임(서기·웅크림 모두)
- Mixamo 클립(사용자 제공 ArtSource/Mixamo/Running Slide.fbx, Vault Over Box.fbx) → 메뉴 `ShadowProtocol/Bake Mixamo Parkour (Player)`(Scripts/Editor/ShadowProtocolMixamoBake.cs):
  Humanoid로 가져와 여성 요원 Humanoid 아바타에 AnimationMode로 샘플(PlayableGraph 수동 평가는 포즈가 적용 안 됐음) → 30fps 뼈 회전 + 골반 위치를 Animations/PlayerGeneric/{Vault,Slide}.anim(Generic)으로. 골반 전진(XZ)은 빼서 ParkourMotion.asset(ParkourMotionData: 전진 거리·골반 높이 곡선)에 저장 → ParkourController가 그 곡선대로 몸을 옮김(발 미끄러짐 없음).
  - 실측: Slide 1.53s(0.3s 골반 0.24m, 전진 6.2m, 처음 약 5.2 m/s) → 0.05~1.45s 사용. Vault 3.53s(0.77s 골반 최고 1.45m, 1.5s 착지, 이후 웅크려 숨음) → 0.30~1.65s 사용(전진 2.16m). 상수 Scripts/ParkourTiming.cs.
  - 뛰어넘기: 클립 최고점이 장애물 가운데 위에 오게 최고점 전·후 전진 비율을 따로, 장애물이 1.0m보다 높으면 그만큼 들어 올림. 착지점은 반대편 끝 + 0.8m.
  - 애니메이터: 플레이어 기본 레이어에 Vault·Slide 상태(Any State 트리거, ParkourSpeed 배율, offset/exitTime은 위 구간). 클립·데이터가 없으면 예전 절차적 자세로.
  - 확인: 뛰어넘기(팔레트 1.1m 위 손 짚고 넘기), 슬라이딩(한 다리 뻗고 손 짚어 낮게) 플레이 캡처 Captures/p16_vault_clip_*, p16_slide_clip_*.
- 사용자: "앉아서 이동이 자연스럽지 못함" → 측정: 골반 0.60m·무릎이 골반보다 37~41cm 앞·머리는 30cm 앞(무릎보다 뒤) = 쪼그려 걷기.
- AC2·AC3 참고(영상 프레임: AC3 Remastered 잠입 플레이 — 스토킹 걸음, 뒤에서 붙잡아 쓰러뜨리는 암살; 원작 데이터는 쓰지 않고 움직임 특징만):
  - 두 관절 IK(CharacterProceduralPose.TwoBoneIK, Generic이라 직접 구현).
  - 웅크린 걸음(AC3 스토킹): 클립이 딛은 발 위치를 기억 → 골반 +0.10m → 다리 IK로 발을 원래 자리에, 상체 +22°, 고개 +14°. 측정: 골반 0.70m, 골반→목 52°, 머리가 골반보다 36cm 앞, 발 바닥 유지, 머리 높이 1.01m(환기구 1.35m 여유).
  - 서서 걷기·달리기(AC2·AC3): 속도에 따라 상체 숙임(걷기 +4°, 달리기 +7° → 달리기 24°), 고개 들기(걷기 10°, 달리기 12° — 목이 28° 숙어 바닥을 보던 것 → 19°). 웅크림 비율만큼 섞여 서기↔웅크리기 전환이 이어짐.
  - 암살(AC3): 돌진 0.2s 동안 왼손이 경비 입 앞을 덮어 잡고, 닿는 순간 오른손이 목덜미를 찌름(손 IK), 붙잡아 당기며 상체 젖힘 → 0.45s부터 놓음, 경비는 뒤로 무너짐. 서는 거리 0.42m.
  - 프리런(AC2·AC3): 달리는 중이면 Space 없이 낮은 장애물 자동 뛰어넘기(가상 입력·봇은 끔, 시험용 ParkourController.AutoVaultForVirtualInput).
- 플레이어가 Generic인 이유(사용자 질문): Humanoid 리타깃이 Blender 웅크림 자세를 틀어 놓아(상체 10° 덜 숙임 등) 이전 세션이 Generic으로 바꿈. 대가는 클립 재사용·Humanoid IK 불가 → 위 굽기 도구·직접 IK로 해결.
- 캡처: p17_cw_side_ik_a/b(웅크림 전후), p17_walk_side_before/after, p17_run_to_crouch, p17_assassin_game/side*, p17_autovault_game.
- 검증(위 변경 모두 반영, 씬 다시 빌드): **HallQuiet 승리 234.9s 발각 0**, **OfficeTools 승리 206.4s 발각 0 제압 1**(AC3식 암살). Windows 빌드 437.1MB 오류 0·경고 0(셰이더 캐시로 13s) → exe -autotest **PASS**(경비 6/6 NavMesh 위, 순찰 5/5 이동, 합계 170.6m, 오류·예외·셰이더 오류 0, Logs/autotest_p17.log).
- 남은 것: 서서 달리기 옆 캡처는 장식물에 가려 측정값으로만 확인(상체 24°). 암살 왼손은 중무장 경비가 커서 입까지는 못 닿고 어깨·목 뒤를 잡는 정도. 뛰어넘기·슬라이딩은 클립 원본 속도 기준이라 장애물 깊이에 따라 착지 직후 약간 미끄러질 수 있음. 서서 걷기 속도 2.4 m/s는 사실상 가벼운 조깅(AC 걸음보다 빠름) — 게임 속도라 바꾸지 않음.

## 2026-11-24 HDRP 마무리(조정자 지시 대조) + "캐릭터가 뿌옇다" 원인 수정
- 상황: HDRP 전환 본체(11-23 기록)는 이미 되어 있어 그대로 두고, 조정자 지시와 다른 점·빠진 점만 채움.
- 바꾼 것:
  - 모션 블러 끔(SP_NightVolumeProfile MotionBlur active=false, intensity 0 — 지시: 게임 판독성).
  - 구역별 반사 프로브 4개(ShadowProtocolHDRP.SetupReflectionProbes, ApplyToScene에서 호출): 야드·홀·사무 구역·서버실, 실시간 OnEnable(시작 때 한 번), 상자 높이는 천장까지 레이로(홀 9m, 사무·서버 3.2m, 야드 10m).
  - **인물 보조광 낮춤**: 플레이어 16 → 6 cd, 경비 12 → 4.5 cd. 어두운 배경 대비 인물만 4~10 lux를 더 받아 평평하게 바래(연회색·뿌옇게) 보였음.
  - 봇·측정용 렌더 보정(ShadowProtocolBot.SteppedRenderFix): 에디터를 멈춘 채 Step()으로 진행하면 HDRP 디퍼드에서 스킨 메시의 Object 모션 벡터가 어긋나 인물이 반투명 유령처럼 보임(실시간 플레이에서는 정상 — 에디터를 앞으로 올려 실시간으로 찍어 확인). 봇 실행 중에만 플레이 인스턴스의 인물 모션 벡터를 Camera로.
  - AutoTest: 프레임 시간(FrameTimingManager GPU·CPU 평균·95%) 로그, `-autotest-shots <폴더>` = 실제 실행 화면으로 p7과 같은 6시점 캡처 + 시점별 GPU 시간. 빌드 때 PlayerSettings.enableFrameTimingStats=true.
- 확인:
  - 분홍(깨진) 재질 0: 씬 렌더러의 재질은 모두 HDRP/Lit·HDRP/Unlit 또는 Sprites/Default(시야 원뿔·소음 원·투척 궤적, SRPDefaultUnlit로 그려짐). 인물 3명 재질에 BaseColor·Normal·Mask 맵 모두 있음.
  - 밝기 감지: 경비 시야 보정은 조명 세기가 아니라 LightZone 데이터 상자 값이라 HDRP 단위와 무관. 구역 가운데 바닥 1m 높이 조도를 조명별로 합산해 대조: 어두움 구역(야드 0.45·창고 0.45·캣워크 0.4) 0~0.5 lux, 중간(서버실 0.6·복도 0.85) 약 17 lux, 밝음(홀 0.8·사무 0.9·가로등 아래·경비실·코어 1.0) 32~78 lux → 같은 구역이 예전처럼 어둡게/밝게 판정, 구역 값 그대로.
  - 캡처: Captures/hdrp_fix_character_close.png(경비·플레이어 근접, 실시간, 보조광 낮춘 뒤).

## 2026-11-24 플레이어 애니메이션 Generic → Humanoid 복귀(사용자 지시) + Humanoid 차이 원인 해결
- ShadowProtocolAnimBuilder.PlayerGeneric = false. 플레이어 클립 6개(대기·걷기·달리기·웅크린 대기·웅크려 걷기·던지기) Humanoid, 아바타 = 여성 요원 모델, 던지기 상체 레이어는 Humanoid 마스크(SP_UpperBody), 웅크림 Foot IK 켬. 파쿠르(뛰어넘기·슬라이딩)는 Humanoid에서 Mixamo 원본 FBX 클립(Animations/MixamoParkour/*_mixamo.fbx)을 직접 씀(ParkourClip). 코드의 뼈 찾기(CharacterProceduralPose·PlayerTools·GuardStatusIndicator)는 원래 Humanoid/이름 둘 다 지원.
- **원인**: 클립 FBX의 휴식 자세(아바타 기준 T자세)가 T자세가 아니라 첫 동작 자세였음 — 모델 기준 자세와 왼팔 46°·오른팔 62°·머리 56°·무릎 28°·발 25~35° 차이. 클립 아바타가 그 자세를 T자세로 삼아 근육 값을 계산하니, 모델 아바타로 되살릴 때 그만큼 일정하게 어긋났음(뼈별 회전 비교로 확인).
  - 근육 범위는 원인 아님: 모델·클립 범위를 똑같이 넓혀도(무릎 -160°, 척추 ±60° 등) 결과가 소수점까지 같음(Humanoid 근육 값은 ±1 밖도 잘리지 않음) → 그 코드는 넣었다가 뺌.
- **수정**: ConfigureImporters에서 플레이어 클립 가져오기 설정의 humanDescription.skeleton을 모델 기준 자세로 바꿔 넣음(MatchSkeleton — 뼈 이름으로 대응, 클립 루트 = 모델 아마추어 항목). 경비 클립은 지금 모습 유지(같은 문제가 있을 수 있음, 필요하면 같은 방법).
- 측정(편집 모드 클립 샘플 MeasureClipPose + 플레이 모드 Foot IK 포함, 덧입힘 끔):

  | 항목 | Blender | Humanoid 전(기본) | Humanoid 후(플레이) |
  |---|---|---|---|
  | 대기 골반 | 0.580m | 0.621 | **0.581** |
  | 대기 몸통 | 37.5° | 35.4 | **37.7** |
  | 대기 어깨 L/R | 36/41° | 28.3/35.2 | **34.6/41.5** |
  | 대기 팔꿈치 L/R | 46.1/55° | 52.3/62.1 | **46.1/55.0** |
  | 대기 손-무릎 L 앞/위/바깥 | 12.6/16.5/-2.0 | 17.0/25.8/1.5 | **12.6/16.6/-1.8** |
  | 대기 관통 | 0 | 0/20 | **0/40** |
  | 걷기 골반 | 0.599 | 0.656 | **0.599** |
  | 걷기 몸통 | 37.5° | 33.9 | **37.3** |
  | 걷기 어깨 L/R | 65.2/43.6° | 57.6/35.9 | **64.7/43.1** |
  | 걷기 팔꿈치 L/R | 50/55° | 56.6/61.7 | **50.0/55.0** |
  | 걷기 관통 | 0 | 0/32 | **4/40(최소 -0.44cm, 살짝 스침)** |

- **남은 문제(사용자 결정 필요)**: 게임에서 보이는 웅크림은 클립 위에 CharacterProceduralPose의 AC3식 덧입힘(몸통 +22°, 골반 +0.10m, 위팔 뒤로 14°, 팔꿈치 펴기 22°, 고개 +14° — 11-23 다른 세션)이 얹혀 Blender 의도와 크게 다름: 대기 골반 0.681m·몸통 53.5°·손이 무릎 뒤 16cm·팔-다리 틈 -0.1cm(닿음), 걷기 골반 0.699·몸통 52.9°. 정면에서 손이 무릎 뒤로 숨어 "팔이 무릎을 뚫는" 모습. 팔 보정 2개만 0으로 하면 걷기 관통 12/39로 오히려 늘어남(몸통 숙임·골반 올림 때문) → 덧입힘을 끄거나(Blender 자세 그대로) 줄이는 것을 사용자에게 물어야 함.
- 캡처: Captures/humanoid_fix_crouch_front.png(사용자 캡처와 같은 정면 각도 — 왼쪽 게임 화면 = 덧입힘 켬, 오른쪽 클립 그대로), humanoid_fix_crouch_sheet.png(1줄 옆 대기→걷기 전환·게임, 2줄 옆 걷기 한 주기·게임, 3줄 옆 걷기 한 주기·클립 그대로, 4줄 정면 걷기 4장 + 정지 2장·게임).
- 함께 고친 것: **모션 블러가 실제로는 켜져 있었음** — 볼륨에서 MotionBlur를 active=false로 끄면 덮어쓰기가 빠져 HDRP 기본 볼륨 프로필(HDRPDefaultResources/DefaultSettingsVolumeProfile)의 모션 블러 0.5가 적용됨 → 켠 채 세기 0으로(스택 값 0 확인). "캐릭터가 뿌옇다"의 원인 중 하나(빠르게 움직이는 인물·카메라 이동 때 번짐).
- 검증(Humanoid + HDRP 마무리 반영, 씬 다시 빌드): **OfficeTools 승리 206.3s 발각 0 제압 1**, **HallQuiet 승리 235.1s 발각 0**. 콘솔 오류 0, 컴파일 오류 0.
- **Windows 빌드 436.8MB 오류 0 경고 0(16s)** → exe `-autotest -autotest-shots Captures` **PASS**(경비 6/6 NavMesh 위, 순찰 5/5 이동, 합계 171.6m, 오류·예외 0).
  - GPU 시간(1280×720, RTX 3060 Ti, D3D12, vSync 끔): 30초 측정(시작 카메라, 플레이어 숨김) GPU 평균 2.95ms·95% 4.20ms, CPU 주 스레드 2.45ms, 322fps. **실제 시점별**: 01 야드 15.37ms(약 65fps, 볼류메트릭 안개 + 가로등 그림자 5개) / 02 홀 9.40 / 03 캣워크 11.35 / 04 사무 복도 9.38 / 05 서버실 6.61 / 06 키패드 7.65ms.
  - 캡처(exe 실제 화면, p7과 같은 시점): Captures/hdrp_01_loading_yard ~ hdrp_06_puzzle_keypad.png. 인물 유령 현상 없음(03 캣워크 반투명은 카메라가 가까울 때의 기존 플레이어 페이드).
  - 창 모드 실행·맨 앞(PID 21644).
- 남은 것: ① 웅크림 AC3식 덧입힘을 끌지·줄일지 사용자 결정(위 표). ② 시작 표시 판(StartPad)이 야드에서 아직 밝은 파랑으로 튐. ③ 경비 클립도 같은 기준 자세 문제가 있을 수 있음(MatchSkeleton을 경비에 적용하면 Blender 자세에 더 가까워짐 — 모습이 바뀌므로 확인 후).
