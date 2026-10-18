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

        [Header("도입 장면")]
        [Tooltip("이 의뢰를 처음 검토할 때 요청 본문을 업무 화면을 덮는 도입 장면으로 보여준다. '확인하고 시작'을 누르면 요청 열람으로 기록한다.")]
        public bool introScene;

        [Tooltip("도입 장면에서 작은 글씨의 부가 정보로 보여줄 첫 본문 줄(0부터). 0이면 본문 전체가 주요 요청이다. 표시 구분일 뿐 원문은 그대로다.")]
        [Min(0)] public int introDetailFromLine;

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
