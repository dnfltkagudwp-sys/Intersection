using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    public enum FinalDisposition
    {
        Keep,
        Delete,
    }

    [Serializable]
    public class ProcessingCountOverride
    {
        public ProgressRecordTarget target;
        [Min(1)] public int includedItemCount = 1;
    }

    /// <summary>
    /// 최종 처리 화면의 계약 단위 묶음. 의뢰와 저장 출처로만 범위를 정하며,
    /// 명시적 포함·제외는 가족사진처럼 계약서가 특정한 원본에만 사용한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Final Processing Group", fileName = "ProcessingGroup")]
    public class ProcessingGroupData : ContentAsset
    {
        public string title;
        public CaseData device;
        public int order;
        public FinalDisposition contractDisposition = FinalDisposition.Delete;

        [Tooltip("device의 선택한 출처에 속한 모든 저장 기록을 포함한다.")]
        public bool includeMatchingSources = true;
        public List<RecordSource> sources = new List<RecordSource>();

        [Tooltip("출처 규칙과 별개로 포함할 원본. 대화방은 기기를 반드시 지정한다.")]
        public List<ProgressRecordTarget> includes = new List<ProgressRecordTarget>();
        [Tooltip("출처 규칙에서 제외할 원본. 다른 묶음의 명시적 예외에 사용한다.")]
        public List<ProgressRecordTarget> excludes = new List<ProgressRecordTarget>();
        [Tooltip("압축파일처럼 한 원본이 여러 항목을 포함할 때만 1보다 큰 값을 둔다.")]
        public List<ProcessingCountOverride> countOverrides = new List<ProcessingCountOverride>();

        [TextArea(1, 4)] public string authorNote;

        public string Title => ContentText.Get(Id, ContentText.Field.Title, title);

        public override IEnumerable<ContentField> ContentFields()
        {
            var list = new List<ContentField>();
            ContentText.Add(list, Id, ContentText.Field.Title, title);
            return list;
        }
    }
}
