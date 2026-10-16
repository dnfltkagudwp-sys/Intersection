using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Intersection.EditorTools
{
    public static partial class ContentValidator
    {
        static void CheckFinale(GameConfig config, List<FinaleData> finales, List<ProcessingGroupData> groups,
            Action<string, Object> error, Action<string, string, Object> checkKey)
        {
            if (finales.Count != 1)
                error($"P6 FinaleData는 정확히 1개여야 합니다 (현재 {finales.Count}개).", finales.FirstOrDefault());
            var finale = finales.FirstOrDefault();
            if (finale == null)
                return;
            if (finale.availableFrom != ProgressStage.P6)
                error("최종 처리 콘텐츠의 접근 단계는 P6여야 합니다.", finale);
            if (finale.firstRequest == null || finale.secondRequest == null || finale.firstJob == null || finale.secondJob == null)
                error("최종 처리 콘텐츠의 두 요청·두 작업 참조가 모두 필요합니다.", finale);
            if (finale.firstJob != null && finale.firstJob.request != finale.firstRequest)
                error("첫 연락 작업과 첫 요청 참조가 일치하지 않습니다.", finale);
            if (finale.secondJob != null && finale.secondJob.request != finale.secondRequest)
                error("두 번째 연락 작업과 두 번째 요청 참조가 일치하지 않습니다.", finale);
            if (finale.firstJob != null && (!finale.firstJob.autoStartWhenReady || !finale.firstJob.autoCompleteWhenReady))
                error("첫 연락 작업은 준비 시 자동 시작하고 요청 확인 시 자동 완료해야 합니다.", finale.firstJob);
            if (finale.secondJob != null && (!finale.secondJob.autoStartWhenReady || !finale.secondJob.autoCompleteWhenReady))
                error("두 번째 연락 작업은 준비 시 자동 시작하고 요청 확인 시 자동 완료해야 합니다.", finale.secondJob);
            if (finale.secondJob != null && finale.firstJob != null
                && !finale.secondJob.startConditions.Any(c => c != null && c.kind == ProgressConditionKind.JobCompleted && c.job == finale.firstJob))
                error("두 번째 연락 작업은 첫 연락 작업 완료 뒤에만 시작해야 합니다.", finale.secondJob);
            if (finale.groups.Count != 5 || finale.groups.Any(g => g == null) || finale.groups.Distinct().Count() != finale.groups.Count)
                error("최종 처리안은 서로 다른 자료 묶음 5개를 가져야 합니다.", finale);
            if (groups.Any(g => !finale.groups.Contains(g)))
                error("FinaleData에 연결되지 않은 최종 처리 묶음이 있습니다.", groups.First(g => !finale.groups.Contains(g)));
            if (string.IsNullOrWhiteSpace(finale.priorRequestBody))
                error("최종 처리 전 다시 보여줄 기존 요청 원문이 비어 있습니다.", finale);
            var ui = new UIText(config.strings);
            foreach (var problem in FinalProcessingCalculator.Problems(finale, config.database, ui))
                error(problem, finale);

            foreach (var key in new[]
            {
                UIKeys.FinaleContactConfirm, UIKeys.FinaleTopFirst, UIKeys.FinaleTopSecond, UIKeys.FinaleTopPlan,
                UIKeys.FinaleTopComplete, UIKeys.FinalePlanHeading, UIKeys.FinalePlanTitle,
                UIKeys.FinalePlanRequests, UIKeys.FinalePlanKeep, UIKeys.FinalePlanDelete,
                UIKeys.FinaleProcessCurrent, UIKeys.FinaleReview, UIKeys.FinaleWarningHeading,
                UIKeys.FinaleWarningBody, UIKeys.FinaleWarningConfirm, UIKeys.FinaleBack, UIKeys.FinaleProcessing,
                UIKeys.EndingTitle, UIKeys.EndingFamilyLabel, UIKeys.EndingRequestB, UIKeys.EndingRequestA,
                UIKeys.EndingOutcomePreserved, UIKeys.EndingOutcomeCleared, UIKeys.EndingCounts, UIKeys.EndingComplete,
                UIKeys.EndingResultHeading, UIKeys.EndingDeletedResult, UIKeys.EndingDeletedUnavailable,
                UIKeys.EndingDeletedFinal, UIKeys.EndingPreservedResult, UIKeys.EndingLater,
                UIKeys.EndingAuditHeading, UIKeys.EndingAuditBody, UIKeys.EndingPreservedFinal,
            })
                checkKey(key, "P6 최종 처리", finale);
        }
    }
}
