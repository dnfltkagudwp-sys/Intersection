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

        [Header("진행")]
        public ProgressStage startStage = ProgressStage.P0;

        [Header("메시지 앱")]
        [Tooltip("이 시간(분) 이상 간격이 벌어지면 대화 안에 날짜 구분선을 넣는다")]
        public int separatorGapMinutes = 60;

        [Header("상태바 시계")]
        public StatusClockMode statusClock = StatusClockMode.System;
        [Range(0, 23)] public int fixedClockHour = 9;
        [Range(0, 59)] public int fixedClockMinute = 41;

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
                    return CultureInfo.GetCultureInfo(cultureName);
                }
                catch (CultureNotFoundException)
                {
                    return CultureInfo.InvariantCulture;
                }
            }
        }
    }
}
