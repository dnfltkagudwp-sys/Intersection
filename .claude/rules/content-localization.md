---
paths:
  - "Assets/Data/**/*"
  - "Assets/Scripts/Data/**/*"
  - "Assets/Scripts/Core/ContentText.cs"
  - "Assets/Scripts/Core/UIText.cs"
  - "Assets/Scripts/Editor/Content*.cs"
---

# 콘텐츠·현지화 규칙

- 메시지·사진·파일·로그·블록·효과의 불변 ID를 유지한다. 배열 순서, 날짜, 파일 경로로 ID를 만들지 않는다.
- `Assets/Data` 변경 뒤 `ContentDatabase` 자동 동기화 상태와 `Intersection/Content/Validate`를 확인한다.
- 한국어 원문은 에셋에 남기고 번역 테이블은 선택적으로 덮어쓴다. 번역 누락은 원문으로 대체한다.
- 화면과 검색은 `Title`, `Body`, `FileName`, `DisplayLines` 같은 표시용 접근자만 사용한다.
- `visibleText`는 이미지나 미리보기에서 실제 보이는 글자의 정확한 전사본이다. 검색용 키워드, 작가 메모, 증거 ID, 내부 ID를 넣지 않는다.
- 경로, URL, 전화번호, 카메라 파일명은 번역하지 않는다. 번역 토큰·확장자·표 구조를 원문과 맞춘다.
- 수량은 `UIText.FormatCount`를 사용한다. `.one`은 선택 항목이지만 기본 키는 필수이며 두 키의 토큰이 같아야 한다.
- 이미지에 글자가 있으면 번역 전사본과 언어별 이미지가 정확히 일치하도록 함께 렌더링·연결한다.
