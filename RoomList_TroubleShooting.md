# 방 목록 표시 문제 해결 가이드

## 수정 완료 사항

### 1. 상세한 디버그 로그 추가
- `[RoomList]` 태그를 모든 로그에 추가하여 추적 용이
- 로비 접속, 세션 업데이트 등 모든 단계별 로그 추가
- UI 참조 누락 에러 체크 추가

### 2. UI 참조 검증
- `ContentParent`가 null인지 체크
- `RoomEntryPrefab`이 null인지 체크
- null일 경우 명확한 에러 메시지 표시

### 3. 세션 정보 상세 로그
- 각 세션의 이름, IsVisible 상태, 플레이어 수 출력
- 방 UI 생성 성공/실패 로그

## 방 목록이 표시되지 않는 일반적인 원인

### 1. Inspector 설정 확인
Unity Editor에서 다음을 확인하세요:

1. **RoomList 게임 오브젝트 선택**
2. **Inspector에서 확인:**
   - `GameMenu`: UIGameMenu 오브젝트 연결
   - `Room Entry Prefab`: RoomEntry 프리팹 할당
   - `Content Parent`: ScrollView의 Content Transform 할당

### 2. 방이 실제로 생성되었는지 확인
방 목록은 **이미 생성된 방**만 표시합니다:
- 먼저 다른 클라이언트나 에디터 인스턴스에서 방을 생성하세요
- 방 생성 시 `IsVisible = true`로 설정되어 있는지 확인 (UIGameMenu.cs에서 이미 설정됨)

### 3. Unity 콘솔 로그 확인
다음 로그를 확인하세요:

**정상 동작 시 예상되는 로그:**
```
[RoomList] NetworkRunner 생성 및 콜백 등록 완료
[RoomList] 로비 접속 시도 중...
[RoomList] ✓ Fusion Lobby 접속 성공! 세션 목록 대기 중...
[RoomList] ★ 방 목록 업데이트 콜백 호출됨! 총 X개의 세션
[RoomList] 세션 발견: RoomName, IsVisible: True, PlayerCount: X/Y
[RoomList] 방 UI 생성 완료: RoomName
[RoomList] ✓ X개의 방 UI 생성 완료
```

**문제 발생 시 나타날 수 있는 로그:**
```
[RoomList] ContentParent가 null입니다! → Inspector 설정 필요
[RoomList] RoomEntryPrefab이 null입니다! → Inspector 설정 필요
[RoomList] RoomEntry 컴포넌트를 찾을 수 없음! → Prefab 확인 필요
[RoomList] 표시 가능한 방이 없습니다 → 방 생성 필요
```

### 4. Photon AppId 설정 확인
`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`에서:
- App Id Fusion이 올바르게 설정되어 있는지 확인

### 5. 네트워크 연결 확인
- 인터넷 연결 확인
- 방화벽이 Fusion 연결을 차단하지 않는지 확인

## 테스트 방법

### 방법 1: 두 개의 Unity Editor 실행
1. 첫 번째 Editor: 방 생성
2. 두 번째 Editor: 방 목록 새로고침
3. 생성된 방이 목록에 표시되는지 확인

### 방법 2: Editor + Build
1. Unity Editor에서 방 생성
2. 빌드된 게임에서 방 목록 확인

## 추가 개선사항

코드에 다음 기능이 추가되었습니다:
- ✅ 로비 재접속 시 기존 러너 정리
- ✅ 비동기 Shutdown 처리
- ✅ 상세한 에러 로깅
- ✅ UI 참조 검증

## 다음 단계

만약 여전히 방 목록이 표시되지 않는다면:

1. Unity Console에서 에러 메시지 확인
2. 위의 로그 메시지 패턴과 비교
3. Inspector 설정 다시 확인
4. 최소 하나의 방이 생성되어 있는지 확인

