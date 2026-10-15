---
paths:
  - "Assets/Scripts/Core/Progress*.cs"
  - "Assets/Scripts/Core/RecordAccess.cs"
  - "Assets/Scripts/Data/ProgressJobData.cs"
  - "Assets/Data/Progress/**/*"
  - "Assets/Scripts/UI/AppShell.Progress.cs"
  - "Assets/Scripts/UI/AppShell.Sync.cs"
  - "Assets/Scripts/UI/SyncSummaryView.cs"
  - "Assets/Scripts/Editor/ContentValidator.Progress.cs"
---

# 진행·저장 규칙

- 진행 조건, 자동 완료, 준비 효과, 알림은 `ProgressJobData`와 관련 데이터로 표현한다. 특정 콘텐츠 ID 비교를 UI나 공용 코드에 추가하지 않는다.
- enum 값은 직렬화 호환을 위해 끝에만 추가한다.
- 작업 시작·완료·효과 실행은 `ProgressStore.Transact` 한 트랜잭션에서 처리한다. 실패하면 메모리와 저장 파일이 모두 이전 상태여야 한다.
- 효과 ID는 불변이고 한 번만 실행된다. 저장 복원과 반복 평가로 알림·효과가 중복되면 안 된다.
- `RecordViewed`는 접근 가능한 실제 원본을 연 경우만 인정한다. 검색 노출, 고정, 분류, 메모, 비교 슬롯·비교 이력은 열람으로 세지 않는다.
- 과거 세이브의 버전·필드 기본값과 원자적 임시 파일 교체 방식을 깨지 않는다.
- 접근 가능 단계 판정은 `RecordAccess` 경로를 공유한다.
