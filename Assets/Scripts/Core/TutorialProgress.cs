using System.Collections.Generic;
using Intersection.Data;

namespace Intersection.Core
{
    public enum StepStatus
    {
        Done,
        Active,
        Waiting,
    }

    /// <summary>
    /// 업무 튜토리얼 진행 평가. 앞 단계가 끝나야 다음 단계가 진행되며, 완료한 단계는 저장 파일에 남는다.
    /// 핀·비교 단계는 상태로 완료된다(비교는 비교 화면이 남긴 이력). 분류 단계는
    /// (1) 바로 앞 단계가 방금 완료되는 순간 이미 그 분류였거나 — 예: 비교 전에 가족사진을 보존해 둔 경우 비교 완료와 함께 —
    /// (2) 그 단계가 진행 중일 때 실제로 분류한 행동으로 완료된다.
    /// 앞 단계와 무관한 시점의 상태만으로는 완료하지 않아, 진행 순서 없이 단계가 건너뛰어지지 않는다.
    /// </summary>
    public static class TutorialProgress
    {
        public static List<(TutorialStep step, StepStatus status)> Evaluate(TutorialData tutorial, WorkStore store)
        {
            var result = new List<(TutorialStep, StepStatus)>();
            bool previousDone = true;
            bool previousJustDone = false;
            foreach (var step in tutorial.steps)
            {
                if (step == null)
                    continue;
                bool stored = store.IsStepCompleted(step.Id);
                bool done = stored;
                if (!done && previousDone && IsMet(step, store, previousJustDone))
                {
                    store.CompleteStep(step.Id);
                    done = true;
                }
                previousJustDone = done && !stored;
                if (done)
                {
                    result.Add((step, StepStatus.Done));
                    continue;
                }
                result.Add((step, previousDone ? StepStatus.Active : StepStatus.Waiting));
                previousDone = false;
            }
            return result;
        }

        /// <summary>분류 행동 직후 호출한다. 진행 중인 분류 단계의 대상이 그 분류로 바뀌었으면 완료한다.</summary>
        public static void OnClassified(TutorialData tutorial, WorkStore store, RecordRef target, Classification classification)
        {
            foreach (var (step, status) in Evaluate(tutorial, store))
            {
                if (status != StepStatus.Active)
                    continue;
                var a = RecordRef.ForAsset(step.target);
                bool matches = a != null && target != null && a.Key == target.Key
                    && ((step.condition == TutorialCondition.ClassifiedKeep && classification == Classification.Keep)
                        || (step.condition == TutorialCondition.ClassifiedDelete && classification == Classification.Delete));
                if (matches)
                    store.CompleteStep(step.Id);
                return;
            }
        }

        /// <param name="previousJustDone">바로 앞 단계가 이번 평가에서 막 완료되었는지. 분류 단계는 이때만 상태로 완료할 수 있다.</param>
        static bool IsMet(TutorialStep step, WorkStore store, bool previousJustDone)
        {
            var a = RecordRef.ForAsset(step.target);
            if (a == null)
                return false;
            switch (step.condition)
            {
                case TutorialCondition.Pinned:
                    return store.IsPinned(a.Key);
                case TutorialCondition.Compared:
                    var b = RecordRef.ForAsset(step.target2);
                    return b != null && store.WasCompared(a.Key, b.Key);
                case TutorialCondition.ClassifiedKeep:
                    return previousJustDone && store.ClassOf(a.Key) == Classification.Keep;
                case TutorialCondition.ClassifiedDelete:
                    return previousJustDone && store.ClassOf(a.Key) == Classification.Delete;
                default:
                    return false;
            }
        }
    }
}
