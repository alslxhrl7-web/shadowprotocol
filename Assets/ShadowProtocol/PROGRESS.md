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
