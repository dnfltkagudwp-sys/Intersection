using System;
using System.Globalization;
using UnityEngine;

namespace Intersection.Data
{
    public enum StatusClockMode
    {
        [Tooltip("실행 중인 PC의 현재 현지 시각")]
        System,
        [Tooltip("테스트·스크린샷용 고정 시각")]
        Fixed,
    }

    /// <summary>
    /// 프로젝트 전역 설정. 임시 caseDate는 여기 한 곳에만 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("사망일 (임시 caseDate)")]
        [Tooltip("모든 실제 날짜와 '사망 N일 전' 표기는 이 날짜에서 계산된다. 최종 사망일은 미확정.")]
        public int caseYear = 2025;
        [Range(1, 12)] public int caseMonth = 1;
        [Range(1, 31)] public int caseDay = 1;

        [Tooltip("날짜·시각 표기에 쓰는 로케일")]
        public string cultureName = "ko-KR";

        [Header("표시 언어")]
        [Tooltip("콘텐츠 원문(데이터 에셋)의 언어. 이 언어로 표시할 때는 번역 테이블을 쓰지 않는다.")]
        public string contentSourceLanguage = "ko";

        [Tooltip("지금 표시할 언어. 같은 코드의 UI·콘텐츠 테이블과 문화권을 함께 쓰며, 콘텐츠 번역이 없는 값은 원문으로 보인다.")]
        public string contentLanguage = "ko";

        [Tooltip("언어별 콘텐츠 번역 테이블 (원문 언어는 넣지 않는다). UI 공통 문구는 strings가 담당한다.")]
        public System.Collections.Generic.List<ContentLocaleTable> contentTables = new System.Collections.Generic.List<ContentLocaleTable>();

        [Tooltip("원문 언어 외의 UI 문자열 테이블. contentLanguage와 같은 언어 코드를 골라 UI와 콘텐츠를 함께 전환한다.")]
        public System.Collections.Generic.List<StringTable> localizedStrings = new System.Collections.Generic.List<StringTable>();

        /// <summary>지금 언어의 번역 테이블. 원문 언어이거나 테이블이 없으면 null(원문 그대로).</summary>
        public ContentLocaleTable ActiveContentTable
        {
            get
            {
                if (string.IsNullOrEmpty(contentLanguage) || contentLanguage == contentSourceLanguage || contentTables == null)
                    return null;
                foreach (var t in contentTables)
                {
                    if (t != null && t.language == contentLanguage)
                        return t;
                }
                return null;
            }
        }

        /// <summary>현재 표시 언어의 UI 문자열 테이블. 번역 테이블이 없으면 기본(strings) 테이블로 대체한다.</summary>
        public StringTable ActiveStrings
        {
            get
            {
                if (string.IsNullOrEmpty(contentLanguage) || contentLanguage == contentSourceLanguage || localizedStrings == null)
                    return strings;
                foreach (var table in localizedStrings)
                {
                    if (table != null && table.language == contentLanguage)
                        return table;
                }
                return strings;
            }
        }

        [Header("진행")]
        public ProgressStage startStage = ProgressStage.P0;

        [Tooltip("에디터·개발 빌드에서만 적용. 새 진행 저장 파일을 만들 때의 시작 단계를 검수용으로 바꾼다(기본은 꺼 둔다). 진행 중인 저장 파일이 있으면 그 단계를 쓴다.")]
        public bool useDevStartStage;
        public ProgressStage devStartStage = ProgressStage.P1;

        public ProgressStage EffectiveStartStage
        {
            get
            {
                // 에디터와 개발 빌드에서만 검수용 시작 단계를 쓴다. 정식 빌드는 항상 startStage.
                if (useDevStartStage && (Application.isEditor || Debug.isDebugBuild))
                    return devStartStage;
                return startStage;
            }
        }

        [Header("메시지 앱")]
        [Tooltip("이 시간(분) 이상 간격이 벌어지면 대화 안에 날짜 구분선을 넣는다")]
        public int separatorGapMinutes = 60;

        [Header("비교")]
        [Tooltip("비교 화면에서 개별 메시지·첨부·위치 공유를 보여줄 때 앞뒤로 함께 보여줄 메시지 수")]
        [Min(0)] public int compareMessageContext = 2;

        [Header("상태바 시계")]
        public StatusClockMode statusClock = StatusClockMode.System;
        [Range(0, 23)] public int fixedClockHour = 9;
        [Range(0, 59)] public int fixedClockMinute = 41;

        [Header("저장")]
        [Tooltip("업무 상태(열람·핀·분류·작업메모·튜토리얼) 저장 파일 이름. Application.persistentDataPath 아래에 만들어진다.")]
        public string workSaveFileName = "work_state.json";

        [Tooltip("진행 상태(접근 단계·의뢰별 작업·업무 알림) 저장 파일 이름. 업무 상태와 별도 파일이다.")]
        public string progressSaveFileName = "progress_state.json";

        [Header("참조")]
        public ContentDatabase database;
        public StringTable strings;
        public UITheme theme;
        public AppRegistry apps;

        public bool TryGetCaseDate(out DateTime date)
        {
            date = default;
            if (caseMonth < 1 || caseMonth > 12 || caseDay < 1 || caseDay > DateTime.DaysInMonth(caseYear, caseMonth))
                return false;
            date = new DateTime(caseYear, caseMonth, caseDay);
            return true;
        }

        public DateTime CaseDate => TryGetCaseDate(out var d) ? d : new DateTime(caseYear, 1, 1);

        public CultureInfo Culture
        {
            get
            {
                try
                {
                    string activeCulture = ActiveStrings != null && !string.IsNullOrEmpty(ActiveStrings.cultureName)
                        ? ActiveStrings.cultureName
                        : cultureName;
                    return CultureInfo.GetCultureInfo(activeCulture);
                }
                catch (CultureNotFoundException)
                {
                    return CultureInfo.InvariantCulture;
                }
            }
        }
    }
}
