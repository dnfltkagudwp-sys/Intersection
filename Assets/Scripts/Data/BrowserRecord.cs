using System.Collections.Generic;
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

        /// <summary>화면에 보이는 검색어·페이지 제목 (현재 언어). 주소(url)는 번역하지 않는다.</summary>
        public string Title => ContentText.Get(Id, ContentText.Field.Title, title);

        public override IEnumerable<ContentField> ContentFields()
        {
            var list = new List<ContentField>();
            ContentText.Add(list, Id, ContentText.Field.Title, title);
            return list;
        }
    }
}
