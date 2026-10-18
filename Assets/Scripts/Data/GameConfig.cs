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

        [Tooltip("경로·파일명의 날짜 토큰 {folderDate:N} 형식 (기기에 저장된 실제 이름이라 언어와 무관, 고정 문화권). 화면 문구용 날짜 형식(문자열 테이블)과 별개다.")]
        public string folderDateFormat = PathTokens.DefaultFormat;

        [Header("표시 언어")]
        [Tooltip("콘텐츠 원문(데이터 에셋)의 언어. 이 언어로 표시할 때는 번역 테이블을 쓰지 않는다.")]
        public string contentSourceLanguage = "ko";

        [Tooltip("기본 표시 언어. 같은 코드의 UI·콘텐츠 테이블과 문화권을 함께 쓰며, 콘텐츠 번역이 없는 값은 원문으로 보인다. 플레이어가 타이틀에서 고른 언어가 있으면 그것이 우선한다(이 값은 바꾸지 않음).")]
        public string contentLanguage = "ko";

        /// <summary>플레이어가 고른 언어(실행 중에만, 에셋에 저장하지 않음). 비어 있으면 contentLanguage.</summary>
        [System.NonSerialized] string languageOverride;

        /// <summary>지금 표시 언어 코드.</summary>
        public string ActiveLanguage => string.IsNullOrEmpty(languageOverride) ? contentLanguage : languageOverride;

        /// <summary>플레이어가 고른 언어를 적용한다. 고를 수 없는 언어면 무시하고 기본 언어로 둔다. null이면 기본 언어.</summary>
        public void UseLanguage(string language) =>
            languageOverride = !string.IsNullOrEmpty(language) && AvailableLanguages.Contains(language) ? language : null;

        /// <summary>고를 수 있는 언어: 원문 언어 + UI 문자열 테이블이 있는 언어.</summary>
        public System.Collections.Generic.List<string> AvailableLanguages
        {
            get
            {
                var list = new System.Collections.Generic.List<string> { contentSourceLanguage };
                if (localizedStrings != null)
                    foreach (var t in localizedStrings)
                        if (t != null && !string.IsNullOrEmpty(t.language) && !list.Contains(t.language))
                            list.Add(t.language);
                return list;
            }
        }

        /// <summary>그 언어의 UI 문자열 테이블(원문 언어면 기본 strings).</summary>
        public StringTable StringsFor(string language)
        {
            if (string.IsNullOrEmpty(language) || language == contentSourceLanguage || localizedStrings == null)
                return strings;
            foreach (var table in localizedStrings)
                if (table != null && table.language == language)
                    return table;
            return strings;
        }

        [Tooltip("언어별 콘텐츠 번역 테이블 (원문 언어는 넣지 않는다). UI 공통 문구는 strings가 담당한다.")]
        public System.Collections.Generic.List<ContentLocaleTable> contentTables = new System.Collections.Generic.List<ContentLocaleTable>();

        [Tooltip("원문 언어 외의 UI 문자열 테이블. contentLanguage와 같은 언어 코드를 골라 UI와 콘텐츠를 함께 전환한다.")]
        public System.Collections.Generic.List<StringTable> localizedStrings = new System.Collections.Generic.List<StringTable>();

        /// <summary>지금 언어의 번역 테이블. 원문 언어이거나 테이블이 없으면 null(원문 그대로).</summary>
        public ContentLocaleTable ActiveContentTable
        {
            get
            {
                string language = ActiveLanguage;
                if (string.IsNullOrEmpty(language) || language == contentSourceLanguage || contentTables == null)
                    return null;
                foreach (var t in contentTables)
                {
                    if (t != null && t.language == language)
                        return t;
                }
                return null;
            }
        }

        /// <summary>현재 표시 언어의 UI 문자열 테이블. 번역 테이블이 없으면 기본(strings) 테이블로 대체한다.</summary>
        public StringTable ActiveStrings => StringsFor(ActiveLanguage);

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

        // 경로 날짜 토큰은 데이터 에셋(파일 기록)이 GameConfig 없이도 같은 결과를 쓰도록 정적 설정에 맞춘다(로드·값 변경 시).
        void OnEnable() => ApplyPathTokens();

        void OnValidate() => ApplyPathTokens();

        public void ApplyPathTokens() => PathTokens.Configure(CaseDate, folderDateFormat);

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
