using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>검토 묶음의 대상 원본 하나. 위치·배열 순번이 아니라 원본 에셋(불변 ID)으로 가리킨다.</summary>
    [Serializable]
    public class ReviewTarget
    {
        [Tooltip("대상 원본 (사진·대화방·브라우저·지도·파일·설정 에셋). 대화방은 묶음 의뢰 기기의 대화방으로 판정한다.")]
        public ContentAsset record;

        [Tooltip("`검토 계속`·분류 행이 여는 순서 (작을수록 먼저). 묶음 전체에서 비교한다.")]
        public int order;
    }

    /// <summary>검토 묶음의 화면 분류 하나 (예: 최근 사진).</summary>
    [Serializable]
    public class ReviewGroup
    {
        [SerializeField, Tooltip("불변 분류 ID. 자동 발급되며 손으로 수정하지 않는다.")]
        string id;

        [Tooltip("분류 표시 문자열 키")]
        public string labelKey;

        public List<ReviewTarget> targets = new List<ReviewTarget>();

        public string Id => id;

        public bool EnsureId()
        {
            if (!string.IsNullOrEmpty(id))
                return false;
            id = ContentAsset.NewId();
            return true;
        }

        public void RegenerateId() => id = ContentAsset.NewId();
    }

    /// <summary>
    /// 온보딩 검토 묶음 (예: P1 로컬 1차 검토). 상단 진행률·검토 팝오버와 실제 진행 판정(ReviewBatchViewed 조건)이
    /// 같은 대상 목록을 쓴다. 대상은 실제로 원본을 연 기록(WorkStore viewed)만 인정한다.
    /// 화면에는 중립적인 자료 종류와 수량만 보이며 증거 ID·관계·정답은 넣지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Review Batch", fileName = "Review")]
    public class ReviewBatchData : ContentAsset
    {
        [Tooltip("대상 원본이 있는 의뢰(기기)")]
        public CaseData device;

        [Tooltip("검토를 마친 뒤 시작할 진행 작업. 그 작업의 시작 조건에 이 묶음(ReviewBatchViewed)이 있어야 한다.")]
        public ProgressJobData job;

        [Header("문구 (문자열 키)")]
        [Tooltip("검토 이름 (예: 로컬 1차 검토)")]
        public string titleKey;
        [Tooltip("모두 확인했을 때 제목 (예: 로컬 1차 검토 완료)")]
        public string doneTitleKey;
        [Tooltip("모두 확인했을 때 설명. {total} 토큰.")]
        public string doneBodyKey;

        public List<ReviewGroup> groups = new List<ReviewGroup>();

        [TextArea(1, 4), Tooltip("작가용 메모. 화면에 표시하지 않는다.")]
        public string authorNote;

        public override bool EnsureIds()
        {
            bool changed = base.EnsureIds();
            foreach (var g in groups)
                changed |= g != null && g.EnsureId();
            return changed;
        }

        public override void RegenerateIds()
        {
            base.RegenerateIds();
            foreach (var g in groups)
                g?.RegenerateId();
        }
    }
}
