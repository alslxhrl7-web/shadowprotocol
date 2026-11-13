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
- 소리는 귀로 확인하지 못함, exe 실행 확인 안 함, 문 상인방 아래에서 카메라 거리 0.62~0.77m(6/624), 드로우콜 많음(에디터 통계 2026)

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
