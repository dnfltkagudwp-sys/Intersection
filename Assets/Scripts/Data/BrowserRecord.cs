using UnityEngine;

namespace Intersection.Data
{
    public enum BrowserRecordKind
    {
        Search,
        Visit,
    }

    /// <summary>모바일 브라우저의 검색·방문 기록 한 건.</summary>
    [CreateAssetMenu(menuName = "Intersection/Records/Browser", fileName = "Browser")]
    public class BrowserRecord : DeviceRecord
    {
        public BrowserRecordKind kind;

        [Tooltip("검색어 또는 방문한 페이지 제목 (화면에 그대로 보인다)")]
        public string title;

        [Tooltip("확정된 주소만 입력한다. 미정이면 비워 둔다.")]
        public string url;
    }
}
