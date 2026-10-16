using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>P6의 순차 유족 연락과 최종 처리 묶음을 한 곳에서 연결하는 데이터.</summary>
    [CreateAssetMenu(menuName = "Intersection/Finale", fileName = "Finale")]
    public class FinaleData : ContentAsset
    {
        public ProgressStage availableFrom = ProgressStage.P6;
        public WorkRequestData firstRequest;
        public ProgressJobData firstJob;
        public WorkRequestData secondRequest;
        public ProgressJobData secondJob;
        public List<ProcessingGroupData> groups = new List<ProcessingGroupData>();

        [TextArea(2, 6), Tooltip("최종 처리 직전에 다시 보여줄 기존 요청 원문(A05).")]
        public string priorRequestBody;

        public string PriorRequestBody => ContentText.Get(Id, ContentText.Field.Body, priorRequestBody);

        public override IEnumerable<ContentField> ContentFields()
        {
            var list = new List<ContentField>();
            ContentText.Add(list, Id, ContentText.Field.Body, priorRequestBody);
            return list;
        }
    }
}
