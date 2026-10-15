---
name: unity-verify
description: Intersection의 Unity 코드·콘텐츠·UI 변경을 최소 범위로 검증할 때 사용한다. 컴파일, 콘텐츠 검증, 제한된 로그 확인, 필요한 플레이 경로 검수를 안내한다.
---

# Unity 검증

1. 먼저 `git status --short`로 기존 변경과 이번 변경을 구분한다.
2. 이미 열린 Unity 에디터가 있으면 그 인스턴스를 사용한다. 같은 프로젝트를 경쟁 인스턴스로 열지 않는다.
3. 문서·Claude 규칙만 바꾼 작업이면 Unity를 실행하지 않는다.
4. 코드 변경은 컴파일 완료와 Console의 새 오류를 확인한다.
5. 콘텐츠 변경은 `Intersection/Content/Validate`를 실행한다. 현지화 변경은 `Intersection/Content/Localization Coverage`도 실행한다.
6. UI 구조 변경에만 `Intersection/UI/Build Main Scene UI`를 실행하고 1920×1080, 1366×768의 관련 화면만 확인한다.
7. 플레이 검수는 변경된 기능으로 이어지는 최소 경로만 수행한다. 플레이 모드에서 스크립트를 수정하지 않는다.
8. Unity/Editor 로그 전체를 읽거나 출력하지 않는다. 오류 문자열을 검색하고 필요하면 마지막 100~200줄만 본다.
9. 테스트 전후 `progress_state.json`, `work_state.json`, 임시 캡처를 정리한다. 실제 사용자 세이브는 허락 없이 덮어쓰거나 삭제하지 않는다.
10. 결과는 실행한 검증, 오류·경고 수, 확인하지 못한 항목만 보고한다.
