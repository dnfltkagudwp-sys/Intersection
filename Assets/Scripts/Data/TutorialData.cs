using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    public enum TutorialCondition
    {
        [Tooltip("target을 작업메모에 핀하면 완료")]
        Pinned,
        [Tooltip("target과 target2를 나란히 비교하면 완료 (UI-06 비교 화면에서 처리)")]
        Compared,
        [Tooltip("target을 보존 후보로 지정하면 완료")]
        ClassifiedKeep,
        [Tooltip("target을 삭제 후보로 지정하면 완료")]
        ClassifiedDelete,
    }

    [Serializable]
    public class TutorialStep
    {
        [SerializeField, Tooltip("불변 단계 ID. 자동 발급되며 손으로 수정하지 않는다.")]
        string id;

        [Tooltip("업무 패널에 보이는 단계 이름")]
        public string label;

        public TutorialCondition condition;

        [Tooltip("대상 기록 (요청·사진·기록 에셋). 에셋 참조라 이름·순서가 바뀌어도 연결이 유지된다.")]
        public ContentAsset target;

        [Tooltip("비교 단계의 두 번째 대상")]
        public ContentAsset target2;

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
    /// 업무 튜토리얼. 단계는 앞 단계가 끝나야 진행되며, 완료 여부는 저장 파일에 단계 ID로 기록된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Tutorial", fileName = "Tutorial")]
    public class TutorialData : ContentAsset
    {
        [Tooltip("업무 패널에 보이는 업무 이름")]
        public string title;

        [Tooltip("튜토리얼이 시작되는 요청")]
        public WorkRequestData request;

        public List<TutorialStep> steps = new List<TutorialStep>();

        public override bool EnsureIds()
        {
            bool changed = base.EnsureIds();
            var seen = new HashSet<string>();
            foreach (var s in steps)
            {
                if (s == null)
                    continue;
                changed |= s.EnsureId();
                if (!seen.Add(s.Id))
                {
                    // 인스펙터에서 단계를 복제하면 ID가 함께 복사된다. 뒤의 복제본에만 새 ID를 준다.
                    s.RegenerateId();
                    seen.Add(s.Id);
                    changed = true;
                }
            }
            return changed;
        }
    }
}
