---
paths:
  - "Assets/Data/Captures/**/*"
  - "Assets/Scripts/Data/Capture*.cs"
  - "Assets/Scripts/Editor/*Capture*.cs"
  - "Assets/Art/Captures/**/*"
---

# 휴대전화 캡처 규칙

- 캡처 PNG는 손으로 그리지 않는다. `CaptureScreenData`와 `CaptureStyleData`를 `Intersection/Content/Render Captures`로 렌더링한다.
- 콘텐츠 문구와 사진 참조를 렌더러 코드에 하드코딩하지 않는다.
- 출력 기준은 1080×2340이며, 서비스 로고 없이 공통 템플릿을 사용한다.
- 게시자 가림은 승인된 크림색 둥근 스마일 스티커를 사용하고 사진 밖으로 넘기지 않는다.
- 캡처의 보이는 글자와 파일 `visibleText`는 글자·순서·문단 기준으로 일치해야 한다. 자동 줄바꿈만 템플릿에 맡긴다.
- 원본 문구·스타일·사진 참조가 바뀌면 다시 렌더링하고 미리보기 연결·임시 표시·검증 결과를 확인한다.
- 언어별 캡처는 해당 언어 전사본과 글자 그대로 맞춘다.
- 검증 스크린샷과 파생 임시 파일을 작업 후 누적하지 않는다.
