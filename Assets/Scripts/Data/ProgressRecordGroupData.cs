using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>열람 묶음의 대상 원본 하나. 파일명·배열 순번·증거 ID가 아니라 원본 에셋(불변 ID)과 기기로 가리킨다.</summary>
    [Serializable]
    public class ProgressRecordTarget
    {
        [Tooltip("원본을 판정할 기기. 대화방은 반드시 지정한다(같은 대화방이 여러 기기에 있다). 사진·기록은 비우면 원본의 기기이며, 지정하면 그 기기와 같아야 한다.")]
        public CaseData device;

        [Tooltip("대상 원본 (사진·대화방·브라우저·지도·파일·설정 에셋)")]
        public ContentAsset record;
    }

    /// <summary>
    /// 진행 조건용 열람 묶음 (예: 계정 조사 자료 중 서로 다른 2건). 대상 중 서로 다른 원본(RecordRef.Key)을 minCount건 이상
    /// 실제로 열람하면 RecordGroupViewed 조건을 만족한다. 같은 원본을 여러 번 열어도 한 건이다.
    /// 화면에 체크리스트나 수량으로 보여주지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Progress Record Group", fileName = "Group")]
    public class ProgressRecordGroupData : ContentAsset
    {
        [Min(1), Tooltip("서로 다른 원본을 몇 건 이상 열람해야 하는지 (1 이상, 대상 수 이하)")]
        public int minCount = 1;

        public List<ProgressRecordTarget> targets = new List<ProgressRecordTarget>();

        [TextArea(1, 4), Tooltip("작가용 메모. 화면에 표시하지 않는다.")]
        public string authorNote;
    }
}
