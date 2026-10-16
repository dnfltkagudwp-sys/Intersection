using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    public enum FinalProcessingChoice
    {
        Contract,
        KeepExceptions,
        CurrentDesignations,
    }

    [Serializable]
    public class FinalProcessingUnitSnapshot
    {
        public string recordKey;
        public string groupId;
        public int itemCount;
        public bool kept;
        public bool exception;
    }

    [Serializable]
    public class FinalProcessingSnapshot
    {
        public string choice;
        public int keptCount;
        public int deletedCount;
        public int contractKeptCount;
        public int exceptionKeptCount;
        public List<FinalProcessingUnitSnapshot> units = new List<FinalProcessingUnitSnapshot>();
    }

    public class FinalProcessingGroupSummary
    {
        public ProcessingGroupData group;
        public int totalCount;
        public int keepCandidateCount;
        public int deleteCandidateCount;
    }

    public class FinalProcessingPlan
    {
        public readonly List<FinalProcessingGroupSummary> groups = new List<FinalProcessingGroupSummary>();
        internal readonly List<FinalProcessingCalculator.Unit> units = new List<FinalProcessingCalculator.Unit>();
        public int ContractKeptCount { get; internal set; }
        public int ContractDeletedCount { get; internal set; }
        public int KeepCandidateCount { get; internal set; }
        public int DeleteCandidateCount { get; internal set; }
        public int KeepOverrideCount { get; internal set; }
        public int DeleteOverrideCount { get; internal set; }
        public int ExceptionKeptCount => ContractKeptCount + KeepOverrideCount;
        public int ExceptionDeletedCount => ContractDeletedCount - KeepOverrideCount;
        public bool CanKeepExceptions => KeepOverrideCount > 0;
        public int CurrentKeptCount => ContractKeptCount - DeleteOverrideCount + KeepOverrideCount;
        public int CurrentDeletedCount => ContractDeletedCount - KeepOverrideCount + DeleteOverrideCount;
        public int ContractOverrideCount => KeepOverrideCount + DeleteOverrideCount;
        public bool HasContractOverrides => ContractOverrideCount > 0;
    }

    /// <summary>
    /// P6에서 접근 가능한 실제 저장 원본을 계약 묶음에 정확히 한 번 배치하고 수량을 계산한다.
    /// 요청·알림·요약·폴더 같은 업무 UI 기록은 열거하지 않는다.
    /// </summary>
    public static class FinalProcessingCalculator
    {
        internal class Unit
        {
            public RecordRef target;
            public ContentAsset asset;
            public CaseData device;
            public RecordSource source;
            public ProcessingGroupData group;
            public int count;
            public Classification classification;
        }

        public static FinalProcessingPlan Build(FinaleData finale, ContentDatabase db, WorkStore work, ProgressStage stage, UIText text)
        {
            var plan = new FinalProcessingPlan();
            if (finale == null || db == null)
                return plan;

            var groups = finale.groups.Where(g => g != null).OrderBy(g => g.order).ToList();
            var all = Units(db, stage, text).ToList();
            foreach (var unit in all)
            {
                var matches = groups.Where(g => Matches(g, unit)).ToList();
                if (matches.Count != 1)
                    continue;
                unit.group = matches[0];
                unit.count = CountOf(unit.group, unit.target);
                unit.classification = ClassificationOf(unit.target, work);
                plan.units.Add(unit);
            }

            foreach (var group in groups)
            {
                var units = plan.units.Where(u => u.group == group).ToList();
                var summary = new FinalProcessingGroupSummary
                {
                    group = group,
                    totalCount = units.Sum(u => u.count),
                    keepCandidateCount = units.Where(u => u.classification == Classification.Keep).Sum(u => u.count),
                    deleteCandidateCount = units.Where(u => u.classification == Classification.Delete).Sum(u => u.count),
                };
                plan.groups.Add(summary);
                if (group.contractDisposition == FinalDisposition.Keep)
                    plan.ContractKeptCount += summary.totalCount;
                else
                    plan.ContractDeletedCount += summary.totalCount;
                plan.KeepCandidateCount += summary.keepCandidateCount;
                plan.DeleteCandidateCount += summary.deleteCandidateCount;
                if (group.contractDisposition == FinalDisposition.Delete)
                    plan.KeepOverrideCount += summary.keepCandidateCount;
                else
                    plan.DeleteOverrideCount += summary.deleteCandidateCount;
            }
            return plan;
        }

        /// <summary>모든 P6 저장 원본이 정확히 한 묶음에 속하는지와 명시 참조·수량 규칙을 검증한다.</summary>
        public static List<string> Problems(FinaleData finale, ContentDatabase db, UIText text)
        {
            var result = new List<string>();
            if (finale == null || db == null)
                return result;
            var groups = finale.groups.Where(g => g != null).ToList();
            var allUnits = Units(db, ProgressStage.P6, text).ToList();
            foreach (var group in groups)
            {
                if (group.device == null)
                    result.Add($"자료 묶음의 의뢰가 비어 있습니다: {group.name}");
                if (string.IsNullOrWhiteSpace(group.title))
                    result.Add($"자료 묶음 표시명이 비어 있습니다: {group.name}");
                if (group.includeMatchingSources && group.sources.Count == 0)
                    result.Add($"출처 규칙을 쓰는 자료 묶음의 출처가 비어 있습니다: {group.name}");
                if (group.sources.Distinct().Count() != group.sources.Count)
                    result.Add($"자료 묶음에 같은 출처가 중복됩니다: {group.name}");
                var includeKeys = Keys(group.includes);
                var excludeKeys = Keys(group.excludes);
                if (includeKeys.Overlaps(excludeKeys))
                    result.Add($"자료 묶음의 명시 포함·제외에 같은 원본이 있습니다: {group.name}");
                var overrideKeys = new HashSet<string>();
                foreach (var entry in group.countOverrides.Where(x => x != null))
                {
                    var target = entry.target != null ? Progression.TargetRef(entry.target.device, entry.target.record) : null;
                    if (target == null)
                        result.Add($"포함 항목 수 대상이 비어 있거나 지원되지 않습니다: {group.name}");
                    else if (!overrideKeys.Add(target.Key))
                        result.Add($"포함 항목 수 대상이 중복됩니다: {group.name} / {target.Key}");
                    else if (entry.includedItemCount < 1)
                        result.Add($"포함 항목 수는 1 이상이어야 합니다: {group.name} / {target.Key}");
                    else
                    {
                        var unit = allUnits.FirstOrDefault(u => u.target.Key == target.Key);
                        if (unit == null || !Matches(group, unit))
                            result.Add($"포함 항목 수 대상이 이 자료 묶음에 속하지 않습니다: {group.name} / {target.Key}");
                    }
                }
            }
            foreach (var unit in allUnits)
            {
                var matches = groups.Where(g => Matches(g, unit)).ToList();
                if (matches.Count == 0)
                    result.Add($"P6 저장 원본이 어느 처리 묶음에도 속하지 않습니다: {unit.target.Key}");
                else if (matches.Count > 1)
                    result.Add($"P6 저장 원본이 여러 처리 묶음에 중복됩니다: {unit.target.Key} / {string.Join(", ", matches.Select(g => g.name))}");
            }
            foreach (var group in groups)
            {
                foreach (var targetData in group.includes.Concat(group.excludes).Where(t => t != null))
                {
                    var target = Progression.TargetRef(targetData.device, targetData.record);
                    if (target == null || RecordAccess.Check(target, db, ProgressStage.P6, text) != AccessState.Accessible)
                        result.Add($"자료 묶음의 명시 원본을 P6에서 열 수 없습니다: {group.name}");
                }
            }
            return result;
        }

        public static FinalProcessingSnapshot Snapshot(FinalProcessingPlan plan, FinalProcessingChoice choice)
        {
            var snapshot = new FinalProcessingSnapshot
            {
                choice = choice.ToString(),
                contractKeptCount = plan.ContractKeptCount,
                exceptionKeptCount = choice == FinalProcessingChoice.Contract ? 0 : plan.KeepOverrideCount,
            };
            foreach (var unit in plan.units)
            {
                bool keepOverride = unit.group.contractDisposition == FinalDisposition.Delete
                                    && unit.classification == Classification.Keep;
                bool kept;
                switch (choice)
                {
                    case FinalProcessingChoice.KeepExceptions:
                        kept = unit.group.contractDisposition == FinalDisposition.Keep || keepOverride;
                        break;
                    case FinalProcessingChoice.CurrentDesignations:
                        kept = unit.classification == Classification.Keep
                               || (unit.classification == Classification.Unclassified
                                   && unit.group.contractDisposition == FinalDisposition.Keep);
                        break;
                    default:
                        kept = unit.group.contractDisposition == FinalDisposition.Keep;
                        break;
                }
                bool exception = kept && keepOverride && choice != FinalProcessingChoice.Contract;
                snapshot.units.Add(new FinalProcessingUnitSnapshot
                {
                    recordKey = unit.target.Key,
                    groupId = unit.group.Id,
                    itemCount = unit.count,
                    kept = kept,
                    exception = exception,
                });
                if (kept)
                    snapshot.keptCount += unit.count;
                else
                    snapshot.deletedCount += unit.count;
            }
            return snapshot;
        }

        static Classification ClassificationOf(RecordRef target, WorkStore work)
        {
            var direct = work.ClassOf(target.Key);
            if (direct != Classification.Unclassified || target.kind != RecordKind.Thread)
                return direct;
            // 말풍선에서 지정한 후보도 그 대화 원본 처리 단위에 반영한다. 서로 다른 하위 후보가 섞이면 보존 후보를 우선해
            // 실제 삭제가 조용히 넓어지지 않게 하고, 화면은 여전히 후보 사실만 표시한다.
            var children = work.All.Where(e => e.target != null && e.target.deviceId == target.deviceId
                                                && e.target.threadId == target.recordId
                                                && e.target.kind != RecordKind.Thread)
                .Select(e => e.classification).ToList();
            if (children.Contains(Classification.Keep))
                return Classification.Keep;
            return children.Contains(Classification.Delete) ? Classification.Delete : Classification.Unclassified;
        }

        static int CountOf(ProcessingGroupData group, RecordRef target)
        {
            foreach (var entry in group.countOverrides.Where(x => x?.target != null))
            {
                var r = Progression.TargetRef(entry.target.device, entry.target.record);
                if (r != null && r.Key == target.Key)
                    return Math.Max(1, entry.includedItemCount);
            }
            return 1;
        }

        static bool Matches(ProcessingGroupData group, Unit unit)
        {
            if (group.device != unit.device)
                return false;
            var excludes = Keys(group.excludes);
            if (excludes.Contains(unit.target.Key))
                return false;
            var includes = Keys(group.includes);
            return includes.Contains(unit.target.Key)
                   || (group.includeMatchingSources && group.sources.Contains(unit.source));
        }

        static HashSet<string> Keys(IEnumerable<ProgressRecordTarget> targets) =>
            new HashSet<string>(targets.Where(t => t != null)
                .Select(t => Progression.TargetRef(t.device, t.record))
                .Where(r => r != null).Select(r => r.Key));

        static IEnumerable<Unit> Units(ContentDatabase db, ProgressStage stage, UIText text)
        {
            foreach (var device in db.cases.Where(c => c != null && c.IsAvailable(stage)))
            {
                foreach (var e in DeviceQuery.Threads(db, device, stage))
                    yield return Make(RecordRef.ForThread(device, e.thread), e.thread, e.state.source, device);
                foreach (var p in PhotoQuery.DevicePhotos(db, device, stage))
                    yield return Make(RecordRef.ForPhoto(p), p, p.source, device);
                foreach (var r in Records(db, device, stage))
                    yield return Make(RecordRef.ForRecord(r), r, r.source, device);
            }
        }

        static Unit Make(RecordRef target, ContentAsset asset, RecordSource source, CaseData device) =>
            new Unit { target = target, asset = asset, source = source, device = device };

        static IEnumerable<DeviceRecord> Records(ContentDatabase db, CaseData device, ProgressStage stage)
        {
            foreach (var r in RecordQuery.For(db.browser, device, stage)) yield return r;
            foreach (var r in RecordQuery.For(db.maps, device, stage)) yield return r;
            foreach (var r in RecordQuery.For(db.files, device, stage)) yield return r;
            foreach (var r in RecordQuery.For(db.settings, device, stage)) yield return r;
            foreach (var r in RecordQuery.For(db.mails, device, stage)) yield return r;
        }
    }
}
