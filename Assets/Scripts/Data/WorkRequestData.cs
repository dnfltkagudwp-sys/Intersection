using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 유족의 업무 요청 (예: T01 가족사진 보존 요청). 휴대전화가 아니라 업무 프로그램에 속한 기록이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Work Request", fileName = "Request")]
    public class WorkRequestData : ContentAsset
    {
        [Tooltip("요청이 속한 의뢰")]
        public CaseData device;

        [Tooltip("좌측 목록과 업무 패널에 보이는 요청 제목")]
        public string title;

        [TextArea(2, 6), Tooltip("요청 본문 (업무 패널에만 표시)")]
        public string body;

        [Tooltip("표시 순서")]
        public int order;

        public ProgressStage availableFrom = ProgressStage.P0;

        [Tooltip("증거 마스터표 ID (예: T01). 화면에 표시하지 않는다.")]
        public string evidenceRef;

        /// <summary>화면에 보이는 요청 제목 (현재 언어).</summary>
        public string Title => ContentText.Get(Id, ContentText.Field.Title, title);

        /// <summary>화면에 보이는 요청 본문 (현재 언어).</summary>
        public string Body => ContentText.Get(Id, ContentText.Field.Body, body);

        public override System.Collections.Generic.IEnumerable<ContentField> ContentFields()
        {
            var list = new System.Collections.Generic.List<ContentField>();
            ContentText.Add(list, Id, ContentText.Field.Title, title);
            ContentText.Add(list, Id, ContentText.Field.Body, body);
            return list;
        }
    }
}
