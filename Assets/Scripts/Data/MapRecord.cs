using UnityEngine;

namespace Intersection.Data
{
    public enum MapRecordKind
    {
        [Tooltip("장소 검색")]
        PlaceSearch,
        [Tooltip("경로 조회")]
        Route,
        [Tooltip("위치 기록 (시작~끝 시각)")]
        LocationHistory,
    }

    /// <summary>
    /// 지도 앱 기록 한 건. 위치 공유 핀은 메시지의 위치 첨부에서 계산하므로 여기 만들지 않는다.
    /// 장소명은 화면에 보이는 표시용 이름만 넣고, 미정이면 비워 둔다(중립 표기로 보인다).
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Records/Map", fileName = "Map")]
    public class MapRecord : DeviceRecord
    {
        public MapRecordKind kind;

        [Tooltip("장소 검색·위치 기록의 표시용 장소명. 미정이면 비운다.")]
        public string placeLabel;

        [Tooltip("경로 조회의 출발지 표시명")]
        public string routeFrom;

        [Tooltip("경로 조회의 도착지 표시명")]
        public string routeTo;

        [Tooltip("경로 조회의 이동 방식·경유 표시 (예: 도보). 미정이면 비운다.")]
        public string routeNote;

        [Tooltip("위치 기록의 마지막 시각 (time = 시작)")]
        public RelativeTime endTime;
    }
}
