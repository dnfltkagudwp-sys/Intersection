using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Intersection.EditorTools
{
    /// <summary>진행 연동(UI-07) 검증: 작업 데이터·효과·도달 가능성·출처와 접근 단계·검색 인덱스·진행 저장 파일·삭제 대기 조회.</summary>
    public static partial class ContentValidator
    {
        static readonly ProgressStage[] Stages = (ProgressStage[])Enum.GetValues(typeof(ProgressStage));

        /// <summary>
        /// RecordViewed 조건 대상의 구조 검사 (시작·완료 조건 공용). 판정 기기는 런타임과 같은 <see cref="Progression.ViewDevice"/> 규칙:
        /// 대화방은 조건의 device(없으면 작업 의뢰 기기), 사진·기록은 원본 자신의 기기(device를 지정했으면 원본 기기와 같아야 함).
        /// before 단계까지 열 수 있는지도 본다. 판정에 쓰는 열람 키(없으면 null)를 돌려준다.
        /// </summary>
        static RecordRef CheckViewCondition(ProgressJobData job, ProgressCondition c, string what, ProgressStage before,
            List<ThreadData> threads, List<PhotoData> photos, List<DeviceRecord> records, string where, Action<string, Object> error)
        {
            if (c.record == null)
            {
                error($"{what}이 비어 있습니다(삭제된 대상): {where}", job);
                return null;
            }
            return CheckTargetShape(Progression.ViewDevice(job, c), c.record, what, job, threads, photos, records, where, error, before);
        }

        /// <summary>효과 목록이 연쇄 완료까지 포함해 올리는 가장 높은 접근 단계 (self의 완료는 따라가지 않는다).</summary>
        static ProgressStage EffectsStage(IEnumerable<ProgressEffect> effects, ProgressJobData self, HashSet<ProgressJobData> visited)
        {
            var stage = ProgressStage.P0;
            foreach (var e in effects.Where(e => e != null))
            {
                if (e.kind == ProgressEffectKind.RaiseStage && e.stage > stage)
                    stage = e.stage;
                else if (e.kind == ProgressEffectKind.CompleteJob && e.job != null && e.job != self && visited.Add(e.job))
                {
                    var s = EffectsStage(e.job.onComplete, self, visited);
                    if (s > stage)
                        stage = s;
                }
            }
            return stage;
        }

        /// <summary>이 작업이 진행 중일 때(시작했고 아직 완료하지 않음) 진행 구조상 도달할 수 있는 최대 접근 단계.</summary>
        static ProgressStage StageWhileRunning(ProgressJobData job, List<ProgressJobData> jobs, ProgressStage startStage)
        {
            var before = StageReachableBefore(job, jobs, startStage);
            var own = EffectsStage(job.onStart, job, new HashSet<ProgressJobData>());
            return own > before ? own : before;
        }

        /// <summary>원본을 처음 열 수 있는 접근 단계 (어느 단계에서도 열 수 없으면 null).</summary>
        static ProgressStage? FirstAccessible(RecordRef target, ContentDatabase db, UIText text) =>
            Stages.Where(s => RecordAccess.Check(target, db, s, text) == AccessState.Accessible).Select(s => (ProgressStage?)s).FirstOrDefault();

        /// <summary>
        /// 완료 조건·열람 묶음 대상의 구조: 사진·기록·대화방 에셋, 대화방은 기기 지정 필수, 사진·기록은 지정한 기기와 원본 기기가 같아야 한다.
        /// 접근 단계는 작업마다 <see cref="CheckCompletionTiming"/>이 따로 본다. 판정에 쓰는 열람 키(없으면 null)를 돌려준다.
        /// </summary>
        static RecordRef CheckTargetShape(CaseData device, ContentAsset record, string what, Object ctx,
            List<ThreadData> threads, List<PhotoData> photos, List<DeviceRecord> records, string where, Action<string, Object> error,
            ProgressStage before = ProgressStage.P6)
        {
            if (record is ThreadData && device == null)
            {
                error($"{what}이 대화방인데 판정할 기기가 지정되지 않았습니다(같은 대화방이 여러 기기에 있을 수 있음): {where}", ctx);
                return null;
            }
            var own = record is PhotoData p ? p.device : record is DeviceRecord r ? r.device : null;
            CheckOpenTarget(device != null ? device : own, record, what, ctx, before, threads, photos, records, where, error);
            return record is PhotoData || record is DeviceRecord || record is ThreadData ? Progression.TargetRef(device, record) : null;
        }

        /// <summary>
        /// 완료 조건 대상의 접근 시점: 작업이 진행 중일 때 도달하는 단계(running)에서 열 수 있어야 한다.
        /// 자기 작업의 완료 효과(completes)로만 열리는 자료는 순환, 그 밖의 늦은 자료는 완료 전 접근 불가로 오류.
        /// </summary>
        static void CheckCompletionTiming(RecordRef target, string what, Object ctx, ProgressStage running, ProgressStage completes,
            ContentDatabase db, UIText text, string where, Action<string, Object> error)
        {
            if (target == null)
                return;
            var first = FirstAccessible(target, db, text);
            if (first == null)
                error($"{what}을 어느 접근 단계에서도 열 수 없습니다({RecordAccess.Check(target, db, ProgressStage.P6, text)}): {target.Key} ({where})", ctx);
            else if (first.Value > running && completes > running && first.Value <= completes)
                error($"{what}은 이 작업의 완료 효과로 열리는 단계({first.Value})부터 열 수 있어 완료 조건으로 쓸 수 없습니다(순환): {target.Key} ({where})", ctx);
            else if (first.Value > running)
                error($"{what}은 {first.Value}부터 열 수 있는데, 이 작업이 진행 중일 때 도달하는 단계는 {running}까지입니다(작업 완료 전 접근 불가): {target.Key} ({where})", ctx);
            else if (target.kind == RecordKind.Photo)
            {
                var res = RecordResolver.Resolve(target, db, text);
                var all = res.found ? PhotoQuery.DevicePhotos(db, res.device, running) : new List<PhotoData>();
                var photo = all.FirstOrDefault(x => x.Id == target.recordId);
                if (photo != null && !PhotoQuery.Albums(db, res.device).Any(a => PhotoQuery.AlbumPhotos(a, all).Contains(photo)))
                    error($"{what} 사진이 어떤 앨범에도 없어 한 장 보기로 열 수 없습니다: {target.Key} ({where})", ctx);
            }
        }

        /// <summary>
        /// 진행 작업의 완료 조건과 자동 완료. 조건은 시작 조건과 같은 종류·같은 열람 기준이며, 대상은 작업이 진행 중일 때 열 수 있어야 한다.
        /// 자동 완료는 완료 조건이 있어야 하고, 작업이 여는 단계에 새로 접근할 수 있는 기록이 있어야 켤 수 있다(빈 단계를 열지 않음).
        /// </summary>
        static void CheckCompletion(GameConfig config, ProgressJobData job, List<ProgressJobData> jobs, List<ProgressRecordGroupData> recordGroups,
            List<CaseData> cases, List<ThreadData> threads, List<PhotoData> photos, List<DeviceRecord> records, UIText text, Action<string, Object> error)
        {
            var db = config.database;
            string where = Path(job);
            var running = StageWhileRunning(job, jobs, config.startStage);
            var completes = EffectsStage(job.onComplete, job, new HashSet<ProgressJobData>());
            var keys = new HashSet<string>();
            foreach (var c in job.completionConditions)
            {
                if (c == null)
                {
                    error($"비어 있는 완료 조건이 있습니다: {where}", job);
                    continue;
                }
                switch (c.kind)
                {
                    case ProgressConditionKind.Retired:
                        error($"더 이상 쓰지 않는 조건(예전 단계형 튜토리얼 완료)이 완료 조건에 있습니다: {where}", job);
                        break;
                    case ProgressConditionKind.RequestViewed:
                        if (c.request == null)
                            error($"완료 조건의 요청이 비어 있습니다(삭제된 대상): {where}", job);
                        break;
                    case ProgressConditionKind.RecordKept:
                        CheckRecordTarget(c.record, "완료 조건(RecordKept)의 record", job.device, where, job, error);
                        break;
                    case ProgressConditionKind.RecordViewed:
                    {
                        string what = "완료 조건(RecordViewed)의 record";
                        // 접근 시점은 아래 CheckCompletionTiming이 진행 중 도달 단계·자기 완료 순환으로 따로 본다.
                        var target = CheckViewCondition(job, c, what, ProgressStage.P6, threads, photos, records, where, error);
                        if (target != null && !keys.Add(target.Key))
                            error($"같은 원본이 완료 조건에 두 번 이상 등록돼 있습니다: {target.Key} ({where})", job);
                        CheckCompletionTiming(target, what, job, running, completes, db, text, where, error);
                        break;
                    }
                    case ProgressConditionKind.RecordGroupViewed:
                        if (c.group == null)
                            error($"완료 조건의 열람 묶음이 비어 있습니다(삭제된 대상): {where}", job);
                        else if (!recordGroups.Contains(c.group))
                            error($"완료 조건이 존재하지 않는 열람 묶음을 가리킵니다: {where}", job);
                        else
                        {
                            foreach (var t in c.group.targets.Where(t => t != null && t.record != null))
                                CheckCompletionTiming(Progression.TargetRef(t.device, t.record), $"완료 조건 열람 묶음({c.group.name})의 대상",
                                    job, running, completes, db, text, where, error);
                        }
                        break;
                    case ProgressConditionKind.ReviewBatchViewed:
                        if (c.batch == null)
                            error($"완료 조건의 검토 묶음이 비어 있습니다(삭제된 대상): {where}", job);
                        break;
                    case ProgressConditionKind.ClassifiedRecords:
                        if (c.minCount < 1)
                            error($"완료 조건의 최소 분류 개수는 1 이상이어야 합니다: {where}", job);
                        break;
                    case ProgressConditionKind.JobCompleted:
                        if (c.job == null)
                            error($"완료 조건의 작업이 비어 있습니다(삭제된 대상): {where}", job);
                        else if (c.job == job)
                            error($"작업이 자기 자신의 완료를 완료 조건으로 씁니다: {where}", job);
                        break;
                }
            }
            foreach (var g in job.completionConditions.Where(c => c != null && c.kind == ProgressConditionKind.RecordGroupViewed && c.group != null)
                         .GroupBy(c => c.group).Where(g => g.Count() > 1))
                error($"같은 열람 묶음이 완료 조건에 두 번 이상 있습니다: {g.Key.name} ({where})", job);

            if (!job.autoCompleteWhenReady)
                return;
            if (job.completionConditions.Count == 0)
            {
                error($"완료 조건 없이 자동 완료(autoCompleteWhenReady)가 켜져 있습니다: {where}", job);
                return;
            }
            if (completes <= running)
                return;
            // 이 작업이 여는 자료가 하나도 없으면 빈 단계가 열린다. 그 작업 의뢰 기기의 그 작업이 여는 출처(unlocksSources) 원본 중
            // 작업 완료 전(진행 중 도달 단계)에는 열 수 없고 완료 단계부터 열 수 있는 것만 센다. 기기·출처는 RecordAccess의 실제 해석 결과로 본다.
            var device = job.device;
            int opened = 0;
            if (device != null && job.unlocksSources.Count > 0)
            {
                var sources = new HashSet<RecordSource>(job.unlocksSources);
                var candidates = DeviceQuery.Threads(db, device, ProgressStage.P6).Select(e => RecordRef.ForThread(device, e.thread))
                    .Concat(photos.Where(p => p.device == device).Select(RecordRef.ForPhoto))
                    .Concat(records.Where(r => r.device == device).Select(RecordRef.ForRecord));
                opened = candidates.Count(r => RecordAccess.Check(r, db, completes, text, out var res) == AccessState.Accessible
                                               && res.device == device && sources.Contains(res.source)
                                               && RecordAccess.Check(r, db, running, text) != AccessState.Accessible);
            }
            if (opened == 0)
                error($"자동 완료가 켜져 있지만 이 작업이 여는 단계({completes})에 이 의뢰({(device != null ? device.name : "?")})의 여는 출처({string.Join("·", job.unlocksSources)}) 정식 기록이 하나도 없습니다 — 빈 단계가 열리지 않도록 그 단계의 콘텐츠를 연결한 뒤 켜세요: {where}", job);
        }

        /// <summary>열람 묶음: 대상 목록·최소 열람 수·같은 대상 중복·기기와 원본 조합.</summary>
        static void CheckRecordGroups(List<ProgressRecordGroupData> recordGroups, List<ProgressJobData> jobs, List<ThreadData> threads,
            List<PhotoData> photos, List<DeviceRecord> records, Action<string, Object> error, Action<string, Object> warn)
        {
            foreach (var group in recordGroups)
            {
                string where = Path(group);
                if (group.targets.Count == 0)
                    error($"열람 묶음에 대상이 없습니다: {where}", group);
                if (group.minCount < 1 || group.minCount > group.targets.Count)
                    error($"열람 묶음의 최소 열람 수({group.minCount})는 1 이상, 대상 수({group.targets.Count}) 이하여야 합니다: {where}", group);
                var pairs = new HashSet<(CaseData, ContentAsset)>();
                var keys = new HashSet<string>();
                foreach (var t in group.targets)
                {
                    if (t == null || t.record == null)
                    {
                        error($"열람 묶음의 대상이 비어 있습니다(삭제된 대상): {where}", group);
                        continue;
                    }
                    if (!pairs.Add((t.device, t.record)))
                    {
                        error($"같은 대상이 열람 묶음에 두 번 이상 등록돼 있습니다: {t.record.name} ({where})", group);
                        continue;
                    }
                    var target = CheckTargetShape(t.device, t.record, $"열람 묶음 대상({t.record.name})", group, threads, photos, records, where, error);
                    if (target != null && !keys.Add(target.Key))
                        error($"열람 묶음의 서로 다른 대상이 같은 원본(RecordRef.Key)을 가리킵니다: {target.Key} ({where})", group);
                }
                if (!jobs.Any(j => j.startConditions.Concat(j.completionConditions).Any(c => c != null && c.kind == ProgressConditionKind.RecordGroupViewed && c.group == group)))
                    warn($"어떤 진행 작업도 쓰지 않는 열람 묶음입니다: {where}", group);
            }
        }

        /// <summary>
        /// 실제로 열어야 하는 대상(RecordViewed·검토 묶음): 사진·기록·대화방 에셋이고, 지정한 의뢰 기기에 있으며, 불변 ID가 하나로 정해지고,
        /// before 단계까지 진행 구조상 도달할 수 있는 단계에서 열 수 있어야 한다.
        /// </summary>
        static void CheckOpenTarget(CaseData device, ContentAsset target, string what, Object ctx, ProgressStage before,
            List<ThreadData> threads, List<PhotoData> photos, List<DeviceRecord> records, string where, Action<string, Object> error)
        {
            var job = ctx;
            if (target == null)
            {
                error($"{what}이 비어 있습니다(삭제된 대상): {where}", job);
                return;
            }
            if (!(target is PhotoData || target is DeviceRecord || target is ThreadData))
            {
                error($"{what}은 사진·기록·대화방 에셋이어야 합니다{(target is WorkRequestData ? "(의뢰 요청은 RequestViewed를 씀)" : "")}: {where}", job);
                return;
            }
            if (string.IsNullOrEmpty(target.Id))
                error($"{what}의 불변 ID가 비어 있습니다: {where}", job);
            else
            {
                int same = threads.Count(x => x.Id == target.Id) + photos.Count(x => x.Id == target.Id) + records.Count(x => x.Id == target.Id);
                if (same > 1)
                    error($"{what}의 불변 ID {target.Id}가 여러 원본에 있어 하나로 정할 수 없습니다: {where}", job);
            }
            if (device == null)
                return;

            ProgressStage availableFrom;
            switch (target)
            {
                case ThreadData t:
                {
                    var state = t.StateFor(device);
                    if (state == null)
                    {
                        error($"{what} 대화방이 이 작업 의뢰의 기기에서 보이지 않습니다: {where}", job);
                        return;
                    }
                    availableFrom = state.availableFrom;
                    break;
                }
                case PhotoData p:
                    if (p.device != device)
                    {
                        error($"{what}이 다른 의뢰의 기록입니다: {where}", job);
                        return;
                    }
                    availableFrom = p.availableFrom;
                    break;
                default:
                {
                    var r = (DeviceRecord)target;
                    if (r.device != device)
                    {
                        error($"{what}이 다른 의뢰의 기록입니다: {where}", job);
                        return;
                    }
                    availableFrom = r.availableFrom;
                    break;
                }
            }
            if (device.availableFrom > availableFrom)
                availableFrom = device.availableFrom;
            if (availableFrom > before)
                error($"{what}은 {availableFrom}부터 열 수 있는데, 이 작업을 시작하기 전에 도달할 수 있는 단계는 {before}까지입니다: {where}", job);
        }

        /// <summary>완료 조건을 모두 만족하면 스스로 완료되는 작업.</summary>
        static bool SelfCompletes(ProgressJobData j) => j.autoCompleteWhenReady && j.completionConditions.Count > 0;

        /// <summary>
        /// 이 작업을 시작하지 않고 도달할 수 있는 최대 접근 단계. 다른 작업 중 시작할 수 있는 것의 시작 효과와,
        /// 완료될 수 있는 것(completedExternally, 완료 조건으로 자동 완료, 또는 시작 가능한 다른 작업이 완료시킴)의 완료 효과만 반영한다.
        /// 자동 완료 작업은 완료 조건 대상의 접근 시점을 CheckCompletion이 따로 검사하므로 여기서는 시작하면 완료될 수 있다고 본다.
        /// </summary>
        static ProgressStage StageReachableBefore(ProgressJobData self, List<ProgressJobData> jobs, ProgressStage startStage)
        {
            var started = new HashSet<ProgressJobData>();
            var completed = new HashSet<ProgressJobData>();
            ProgressStage stage = startStage;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var j in jobs.Where(j => j != self && j.device != null))
                {
                    if (!started.Contains(j) && j.device.availableFrom <= stage
                        && j.startConditions.Where(c => c != null && c.kind == ProgressConditionKind.JobCompleted).All(c => completed.Contains(c.job)))
                    {
                        started.Add(j);
                        changed = true;
                        foreach (var e in j.onStart.Where(e => e != null))
                        {
                            if (e.kind == ProgressEffectKind.RaiseStage && e.stage > stage) stage = e.stage;
                            if (e.kind == ProgressEffectKind.CompleteJob && e.job != null && e.job != self) completed.Add(e.job);
                        }
                    }
                    if (started.Contains(j) && !completed.Contains(j) && (j.completedExternally || SelfCompletes(j)))
                    {
                        completed.Add(j);
                        changed = true;
                    }
                }
                foreach (var j in completed.ToList())
                {
                    foreach (var e in j.onComplete.Where(e => e != null))
                    {
                        if (e.kind == ProgressEffectKind.RaiseStage && e.stage > stage) { stage = e.stage; changed = true; }
                        if (e.kind == ProgressEffectKind.CompleteJob && e.job != null && e.job != self && completed.Add(e.job)) changed = true;
                    }
                }
            }
            return stage;
        }

        static void CheckProgress(GameConfig config, List<ProgressJobData> jobs, List<ReviewBatchData> reviews, List<ProgressRecordGroupData> recordGroups, List<CaseData> cases, List<ThreadData> threads,
            List<PhotoData> photos, List<DeviceRecord> records, List<FolderData> folders, List<WorkRequestData> requests,
            Action<string, Object> error, Action<string, Object> warn, Action<string, string, Object> checkKey)
        {
            var db = config.database;
            var text = new UIText(config.strings);
            var time = new PhoneTime(config, text);

            // ── 작업 데이터: 참조·문구 키·효과 ID
            var effectIds = new Dictionary<string, string>();
            foreach (var job in jobs)
            {
                string where = Path(job);
                if (job.device == null)
                    error($"진행 작업의 의뢰가 비어 있습니다: {where}", job);
                else if (!cases.Contains(job.device))
                    error($"진행 작업이 존재하지 않는 의뢰를 가리킵니다: {where}", job);
                if (job.request == null)
                    error($"진행 작업의 시작 요청이 비어 있습니다: {where}", job);
                else if (!requests.Contains(job.request))
                    error($"진행 작업이 존재하지 않는 요청을 가리킵니다: {where}", job);
                else if (job.request.device != job.device)
                    error($"진행 작업의 요청이 다른 의뢰의 요청입니다: {where}", job);
                foreach (var (key, field) in new[]
                         {
                             (job.actionKey, "actionKey"), (job.notReadyKey, "notReadyKey"), (job.runningKey, "runningKey"),
                             (job.doneKey, "doneKey"), (job.shortRunningKey, "shortRunningKey"), (job.shortDoneKey, "shortDoneKey"),
                         })
                {
                    if (string.IsNullOrEmpty(key))
                        error($"진행 작업의 {field}가 비어 있습니다: {where}", job);
                    else
                        checkKey(key, $"진행 작업 {field} ({where})", job);
                }
                // 시작 연출(선택): 문구 키가 있으면 테이블에 있어야 하고, 입력을 막는 시간이라 짧게 둔다.
                if (!string.IsNullOrEmpty(job.startPresentationKey))
                {
                    checkKey(job.startPresentationKey, $"진행 작업 startPresentationKey ({where})", job);
                    if (job.startPresentationSeconds <= 0f || job.startPresentationSeconds > 5f)
                        error($"시작 연출 시간({job.startPresentationSeconds}초)은 0초보다 길고 5초 이하여야 합니다: {where}", job);
                }

                if (job.startConditions.Count(c => c != null && c.kind == ProgressConditionKind.ReviewBatchViewed) > 1)
                    error($"한 작업에 검토 묶음 조건이 둘 이상 있습니다: {where}", job);
                foreach (var c in job.startConditions)
                {
                    if (c == null)
                    {
                        error($"비어 있는 시작 조건이 있습니다: {where}", job);
                        continue;
                    }
                    switch (c.kind)
                    {
                        case ProgressConditionKind.Retired:
                            error($"더 이상 쓰지 않는 시작 조건(예전 단계형 튜토리얼 완료)이 남아 있습니다. RequestViewed·RecordKept 등으로 바꾸세요: {where}", job);
                            break;
                        case ProgressConditionKind.RecordKept:
                            CheckRecordTarget(c.record, "시작 조건(RecordKept)의 record", job.device, where, job, error);
                            break;
                        case ProgressConditionKind.RequestViewed:
                            if (c.request == null)
                                error($"시작 조건의 요청이 비어 있습니다(삭제된 대상): {where}", job);
                            break;
                        case ProgressConditionKind.RecordViewed:
                            // 시작 전에 도달할 수 있는 단계에서 열 수 있어야 한다(완료 조건과 같은 대상 해석 규칙).
                            CheckViewCondition(job, c, "시작 조건(RecordViewed)의 record", StageReachableBefore(job, jobs, config.startStage),
                                threads, photos, records, where, error);
                            break;
                        case ProgressConditionKind.RecordGroupViewed:
                            if (c.group == null)
                                error($"시작 조건의 열람 묶음이 비어 있습니다(삭제된 대상): {where}", job);
                            else if (!recordGroups.Contains(c.group))
                                error($"시작 조건이 존재하지 않는 열람 묶음을 가리킵니다: {where}", job);
                            else
                            {
                                // 개별 RecordViewed와 같은 기준: 각 대상을 이 작업 시작 전에 도달할 수 있는 단계에서 열 수 있어야 한다.
                                var before = StageReachableBefore(job, jobs, config.startStage);
                                foreach (var t in c.group.targets)
                                {
                                    if (t == null || t.record == null)
                                        error($"시작 조건 열람 묶음({c.group.name})의 대상이 비어 있습니다(삭제된 대상): {where}", job);
                                    else
                                        CheckTargetShape(t.device, t.record, $"시작 조건 열람 묶음({c.group.name})의 대상({t.record.name})", job,
                                            threads, photos, records, where, error, before);
                                }
                            }
                            break;
                        case ProgressConditionKind.ReviewBatchViewed:
                            if (c.batch == null)
                                error($"시작 조건의 검토 묶음이 비어 있습니다(삭제된 대상): {where}", job);
                            else if (!reviews.Contains(c.batch))
                                error($"시작 조건이 존재하지 않는 검토 묶음을 가리킵니다: {where}", job);
                            else if (c.batch.job != job)
                                error($"시작 조건의 검토 묶음이 다른 작업을 가리킵니다(끊긴 참조) — 묶음의 job과 이 작업이 같아야 합니다: {where} / {Path(c.batch)}", job);
                            break;
                        case ProgressConditionKind.ClassifiedRecords:
                            if (c.minCount < 1)
                                error($"시작 조건의 최소 분류 개수는 1 이상이어야 합니다: {where}", job);
                            break;
                        case ProgressConditionKind.JobCompleted:
                            if (c.job == null)
                                error($"시작 조건의 작업이 비어 있습니다(삭제된 대상): {where}", job);
                            else if (c.job == job)
                                error($"작업이 자기 자신의 완료를 시작 조건으로 씁니다: {where}", job);
                            break;
                    }
                }

                CheckCompletion(config, job, jobs, recordGroups, cases, threads, photos, records, text, error);

                // 준비 효과: 시작 조건을 처음 모두 만족했을 때 한 번. 알림만 허용(작업 시작·단계 변경은 플레이어가 직접 한다).
                if (job.onReady.Count > 0 && !job.startConditions.Any(c => c != null))
                    error($"시작 조건 없이 준비 효과(onReady)가 있습니다 — 준비 효과는 시작 조건을 처음 모두 만족했을 때 실행됩니다: {where}", job);
                foreach (var e in job.onReady.Where(e => e != null && e.kind != ProgressEffectKind.Notify))
                    error($"준비 효과(onReady)에는 업무 알림(Notify)만 쓸 수 있습니다({e.kind}) — 작업 시작·단계 변경은 플레이어가 상단에서 직접 합니다: {where}", job);

                foreach (var e in job.AllEffects)
                {
                    if (e == null)
                    {
                        error($"비어 있는 진행 효과가 있습니다: {where}", job);
                        continue;
                    }
                    CheckInnerId(e.Id, "진행 효과", where, job, effectIds, error);
                    switch (e.kind)
                    {
                        case ProgressEffectKind.RaiseStage:
                            if (!Enum.IsDefined(typeof(ProgressStage), e.stage))
                                error($"진행 효과의 접근 단계가 올바르지 않습니다: {where}", job);
                            break;
                        case ProgressEffectKind.CompleteJob:
                            if (e.job == null)
                                error($"진행 효과의 완료할 작업이 비어 있습니다(삭제된 대상): {where}", job);
                            else if (!jobs.Contains(e.job))
                                error($"진행 효과가 존재하지 않는 작업을 가리킵니다: {where}", job);
                            break;
                        case ProgressEffectKind.Notify:
                            CheckTemplate(config, e, where, job, error);
                            break;
                        case ProgressEffectKind.SyncSummary:
                            if (!job.onComplete.Contains(e))
                                error($"연동 결과 요약 효과는 완료 효과(onComplete)에만 둘 수 있습니다: {where}", job);
                            if (job.unlocksSources.Count == 0)
                                error($"연동 결과 요약 효과가 있는데 이 작업이 여는 출처(unlocksSources)가 없습니다: {where}", job);
                            if (job.onComplete.Count(x => x != null && x.kind == ProgressEffectKind.SyncSummary) > 1)
                                error($"한 작업에 연동 결과 요약 효과가 둘 이상 있습니다: {where}", job);
                            CheckTemplate(config, e, where, job, error);
                            foreach (var (key, field) in new[] { (e.summaryHeadingKey, "summaryHeadingKey"), (e.summaryCountKey, "summaryCountKey"), (e.summaryOpenKey, "summaryOpenKey") })
                                if (!string.IsNullOrEmpty(key))
                                    checkKey(key, $"연동 결과 요약 {field} ({where})", job);
                            break;
                    }
                    if (e.kind != ProgressEffectKind.SyncSummary
                        && (!string.IsNullOrEmpty(e.summaryHeadingKey) || !string.IsNullOrEmpty(e.summaryCountKey) || !string.IsNullOrEmpty(e.summaryOpenKey)))
                        error($"요약 문구 키는 연동 결과 요약(SyncSummary) 효과에만 쓸 수 있습니다({e.kind}): {where}", job);
                }
            }

            CheckRecordGroups(recordGroups, jobs, threads, photos, records, error, warn);

            // ── 검토 묶음: 상단 진행률·팝오버·진행 판정이 함께 쓰는 대상 목록
            var groupIds = new Dictionary<string, string>();
            foreach (var batch in reviews)
            {
                string where = Path(batch);
                if (batch.device == null)
                    error($"검토 묶음의 의뢰가 비어 있습니다: {where}", batch);
                else if (!cases.Contains(batch.device))
                    error($"검토 묶음이 존재하지 않는 의뢰를 가리킵니다: {where}", batch);
                var before = config.startStage;
                if (batch.job == null)
                    error($"검토 묶음의 진행 작업(job)이 비어 있습니다(끊긴 참조): {where}", batch);
                else if (!jobs.Contains(batch.job))
                    error($"검토 묶음이 존재하지 않는 진행 작업을 가리킵니다: {where}", batch);
                else
                {
                    if (batch.job.device != batch.device)
                        error($"검토 묶음의 의뢰와 진행 작업의 의뢰가 다릅니다: {where}", batch);
                    if (!batch.job.startConditions.Any(c => c != null && c.kind == ProgressConditionKind.ReviewBatchViewed && c.batch == batch))
                        error($"검토 묶음이 가리키는 진행 작업의 시작 조건에 이 묶음(ReviewBatchViewed)이 없습니다(끊긴 참조): {where} / {Path(batch.job)}", batch);
                    if (batch.job.onStart.Concat(batch.job.onComplete).Any(e => e != null && e.kind == ProgressEffectKind.CompleteJob && e.job == batch.job))
                        error($"검토 묶음의 진행 작업이 자기 자신을 완료시킵니다(순환): {where}", batch);
                    before = StageReachableBefore(batch.job, jobs, config.startStage);
                }
                foreach (var (key, field) in new[] { (batch.titleKey, "titleKey"), (batch.doneTitleKey, "doneTitleKey"), (batch.doneBodyKey, "doneBodyKey") })
                {
                    if (string.IsNullOrEmpty(key))
                        error($"검토 묶음의 {field}가 비어 있습니다: {where}", batch);
                    else
                        checkKey(key, $"검토 묶음 {field} ({where})", batch);
                }
                if (!string.IsNullOrEmpty(batch.doneBodyKey) && config.strings != null && config.strings.TryGet(batch.doneBodyKey, out var doneBody))
                {
                    foreach (Match m in Token.Matches(doneBody))
                    {
                        if (!UIKeys.ReviewTokens.Contains(m.Groups[1].Value))
                            error($"검토 묶음 문구 '{batch.doneBodyKey}'에 지원하지 않는 토큰 {m.Value}이 있습니다: {where}", batch);
                    }
                }
                if (batch.groups.Count == 0)
                    error($"검토 묶음에 분류가 없습니다: {where}", batch);
                var keys = new HashSet<string>();
                int total = 0;
                foreach (var group in batch.groups)
                {
                    if (group == null)
                    {
                        error($"비어 있는 검토 분류가 있습니다: {where}", batch);
                        continue;
                    }
                    CheckInnerId(group.Id, "검토 분류", where, batch, groupIds, error);
                    if (string.IsNullOrEmpty(group.labelKey))
                        error($"검토 분류의 표시 문자열 키(labelKey)가 비어 있습니다: {where}", batch);
                    else
                        checkKey(group.labelKey, $"검토 분류 labelKey ({where})", batch);
                    if (group.targets.Count == 0)
                        error($"비어 있는 검토 분류가 있습니다({group.labelKey}): {where}", batch);
                    foreach (var t in group.targets)
                    {
                        total++;
                        string what = $"검토 분류({group.labelKey})의 대상";
                        if (t == null || t.record == null)
                        {
                            error($"{what}이 비어 있습니다(삭제된 대상): {where}", batch);
                            continue;
                        }
                        CheckOpenTarget(batch.device, t.record, what, batch, before, threads, photos, records, where, error);
                        var target = Progression.ReviewTargetRef(batch, t.record);
                        if (target == null)
                            continue;
                        if (!keys.Add(target.Key))
                            error($"같은 원본이 검토 묶음에 두 번 이상 등록돼 있습니다: {target.Key} ({where})", batch);
                        if (batch.device == null)
                            continue;
                        // 실제 판정·`검토 계속`과 같은 접근 판정·원본 열기 경로로 열 수 있는지(대상 단계까지 도달 가능한 범위에서).
                        var access = RecordAccess.Check(target, db, before, text);
                        if (access != AccessState.Accessible)
                            error($"{what}을 이 작업 전 도달 단계({before})에서 열 수 없습니다({access}): {target.Key} ({where})", batch);
                        else if (t.record is PhotoData photo)
                        {
                            var all = PhotoQuery.DevicePhotos(db, batch.device, before);
                            if (!PhotoQuery.Albums(db, batch.device).Any(a => PhotoQuery.AlbumPhotos(a, all).Contains(photo)))
                                error($"{what} 사진이 어떤 앨범에도 없어 한 장 보기로 열 수 없습니다: {Path(photo)}", batch);
                        }
                    }
                }
                if (batch.device != null)
                {
                    foreach (var group in batch.groups.Where(g => g != null && g.targets.Count > 0))
                        CheckRecentPrefix(db, batch, group, before, warn);
                }
                // 화면 총합은 대상 수에서 계산하므로, 중복 등록이 있으면 상단 N/M과 실제로 열어야 하는 원본 수가 달라진다.
                if (total != keys.Count)
                    error($"검토 묶음의 화면 총합({total})과 실제 서로 다른 대상 수({keys.Count})가 다릅니다: {where}", batch);
            }

            // ── 연동 결과 요약: 완료 단계에서 그 출처의 자료가 모두 접근 가능해야 요약 수량이 실제 새 자료와 같다
            foreach (var job in jobs.Where(j => j.device != null && Progression.SummaryEffect(j) != null))
            {
                var after = job.onComplete.Where(e => e != null && e.kind == ProgressEffectKind.RaiseStage).Select(e => e.stage)
                    .DefaultIfEmpty(ProgressStage.P0).Max();
                var sources = new HashSet<RecordSource>(job.unlocksSources);
                var candidates = DeviceQuery.Threads(db, job.device, ProgressStage.P6).Where(e => sources.Contains(e.state.source))
                    .Select(e => RecordRef.ForThread(job.device, e.thread))
                    .Concat(PhotoQuery.DevicePhotos(db, job.device, ProgressStage.P6).Where(p => sources.Contains(p.source)).Select(RecordRef.ForPhoto))
                    .Concat(records.Where(r => r.device == job.device && sources.Contains(r.source)).Select(RecordRef.ForRecord));
                foreach (var r in candidates)
                {
                    var access = RecordAccess.Check(r, db, after, text);
                    if (access != AccessState.Accessible)
                        error($"연동 결과 요약의 출처 자료가 작업 완료 단계({after})에서 접근할 수 없습니다({access}) — 요약은 접근 가능한 자료만 셉니다: {r.Key} ({Path(job)})", job);
                }
            }

            // ── 순환: 작업 완료가 서로를 완료시키는 고리
            foreach (var job in jobs)
            {
                if (CompletionReaches(job, job, new HashSet<ProgressJobData>()))
                    error($"작업 완료 효과가 순환합니다: {Path(job)}", job);
            }

            // ── 도달 가능성: 작업 완료 경로, 교착(서로의 시작·완료를 기다림), 데이터가 쓰는 접근 단계
            foreach (var job in jobs)
            {
                var completers = jobs.Where(o => o != job && o.AllEffects.Any(e => e != null && e.kind == ProgressEffectKind.CompleteJob && e.job == job)).ToList();
                bool needed = jobs.Any(o => o.startConditions.Any(c => c != null && c.kind == ProgressConditionKind.JobCompleted && c.job == job))
                              || job.onComplete.Any(e => e != null);
                bool selfCompletes = SelfCompletes(job);
                if (needed && !job.completedExternally && !selfCompletes && completers.Count == 0)
                    error($"작업을 완료시키는 경로가 없습니다 (다른 작업의 완료 효과도, completedExternally도, 자동 완료도 없음): {Path(job)}", job);
                foreach (var c in job.startConditions.Where(c => c != null && c.kind == ProgressConditionKind.JobCompleted && c.job != null))
                {
                    var dep = c.job;
                    var depCompleters = jobs.Where(o => o != job && o.AllEffects.Any(e => e != null && e.kind == ProgressEffectKind.CompleteJob && e.job == dep)).ToList();
                    if (!dep.completedExternally && !SelfCompletes(dep) && depCompleters.Count > 0 && depCompleters.All(o => o == job))
                        error($"교착: {Path(job)}은 {Path(dep)} 완료를 기다리지만, 그 작업은 이 작업이 시작돼야만 완료됩니다.", job);
                }
            }
            var reachable = new HashSet<ProgressStage> { config.startStage };
            foreach (var e in jobs.SelectMany(j => j.AllEffects).Where(e => e != null && e.kind == ProgressEffectKind.RaiseStage))
                reachable.Add(e.stage);
            ProgressStage maxReachable = reachable.Max();
            var usedStages = cases.Select(c => (c.availableFrom, (Object)c))
                .Concat(records.Select(r => (r.availableFrom, (Object)r)))
                .Concat(photos.Select(p => (p.availableFrom, (Object)p)))
                .Concat(folders.Select(f => (f.availableFrom, (Object)f)))
                .Concat(requests.Select(q => (q.availableFrom, (Object)q)))
                .Concat(threads.SelectMany(t => t.devices.Where(d => d != null).Select(d => (d.availableFrom, (Object)t))));
            foreach (var g in usedStages.Where(u => u.availableFrom > maxReachable).GroupBy(u => u.availableFrom))
                error($"{g.Key} 단계에 도달하는 진행 효과가 없어 {g.Count()}개 자료에 접근할 수 없습니다: {Path(g.First().Item2)}", g.First().Item2);

            // ── 출처와 접근 단계: 로컬은 기기 등록 때부터, 클라우드·연동·복구는 그 출처를 여는 작업이 끝난 뒤부터
            void CheckSource(CaseData device, RecordSource source, ProgressStage availableFrom, string what, Object ctx)
            {
                if (device == null)
                    return;
                if (source == RecordSource.Local)
                {
                    if (availableFrom != device.availableFrom)
                        error($"로컬 기록의 접근 단계({availableFrom})가 기기 등록 단계({device.availableFrom})와 다릅니다 — 로컬 자료는 기기 등록 때부터 존재해야 합니다: {what}", ctx);
                    return;
                }
                var openers = jobs.Where(j => j.device == device && j.unlocksSources.Contains(source)).ToList();
                if (openers.Count == 0)
                {
                    error($"{source} 출처 기록인데 이 의뢰에 그 출처를 여는 진행 작업이 없습니다: {what}", ctx);
                    return;
                }
                var opened = openers.SelectMany(j => j.onComplete).Where(e => e != null && e.kind == ProgressEffectKind.RaiseStage)
                    .Select(e => e.stage).DefaultIfEmpty(ProgressStage.P6).Min();
                if (availableFrom < opened)
                    error($"{source} 출처 기록의 접근 단계({availableFrom})가 그 출처를 여는 작업이 끝나는 단계({opened})보다 이릅니다: {what}", ctx);
            }
            foreach (var r in records)
                CheckSource(r.device, r.source, r.availableFrom, Path(r), r);
            foreach (var p in photos)
                CheckSource(p.device, p.source, p.availableFrom, Path(p), p);
            foreach (var f in folders)
                CheckSource(f.device, f.source, f.availableFrom, Path(f), f);
            foreach (var t in threads)
            {
                foreach (var d in t.devices.Where(d => d != null && d.device != null))
                    CheckSource(d.device, d.source, d.availableFrom, $"{Path(t)} ({d.device.name})", t);
            }

            // ── 검색 인덱스·앱 목록: 각 단계에서 접근 전 기록이 섞이지 않는지, 결과가 열 수 있는 앱·원본을 가리키는지
            foreach (var stage in Stages)
            {
                foreach (var hit in GlobalSearch.Searchable(db, stage, text, time))
                {
                    var access = RecordAccess.Check(hit.target, db, stage, text, out var res);
                    if (access != AccessState.Accessible)
                        error($"{stage} 검색 인덱스에 열 수 없는 기록이 있습니다({access}): {hit.target.Key}", null);
                    if (hit.app.HasValue && (config.apps == null || !config.apps.apps.Any(a => a != null && a.kind == hit.app.Value && a.implemented)))
                        error($"검색 결과가 등록되지 않았거나 구현되지 않은 앱({hit.app.Value})을 가리킵니다: {hit.target.Key}", null);
                    if (hit.target.kind == RecordKind.Photo)
                    {
                        var all = PhotoQuery.DevicePhotos(db, hit.device, stage);
                        var photo = all.FirstOrDefault(p => p.Id == hit.target.recordId);
                        if (photo != null && !PhotoQuery.Albums(db, hit.device).Any(a => PhotoQuery.AlbumPhotos(a, all).Contains(photo)))
                            error($"검색 결과 사진이 어떤 앨범에도 없어 원본을 열 수 없습니다: {Path(photo)}", photo);
                    }
                }
                // 파일 표시 문구는 검색 가능한 원본과 연결돼야 한다: 접근·인덱싱된 파일의 각 줄로 검색하면 그 파일이 나와야 하고,
                // 접근 전 파일은 어떤 줄로도 나오면 안 된다.
                foreach (var f in db.files.Where(x => x != null && x.device != null && x.HasVisibleText))
                {
                    var target = RecordRef.ForRecord(f);
                    bool expected = RecordAccess.Searchable(f.device, stage) && RecordQuery.For(db.files, f.device, stage).Contains(f);
                    foreach (var line in f.VisibleLines(time.NoteDate))
                    {
                        bool found = GlobalSearch.Run(db, stage, line, text, time).Any(h => h.target.Key == target.Key);
                        if (expected && !found)
                            error($"{stage}: 파일 표시 문구로 검색해도 그 파일이 나오지 않습니다('{line}'): {Path(f)}", f);
                        else if (!expected && found)
                            error($"{stage}: 접근 전 파일의 표시 문구가 검색됩니다('{line}'): {Path(f)}", f);
                    }
                }
                // 사진 안 글자 전사본(visibleText)도 같은 기준: 접근 가능한 단계에서만 그 사진이 검색된다.
                foreach (var p in db.photos.Where(x => x != null && x.device != null && !string.IsNullOrWhiteSpace(x.visibleText)))
                {
                    var target = RecordRef.ForPhoto(p);
                    bool expected = RecordAccess.Searchable(p.device, stage) && PhotoQuery.DevicePhotos(db, p.device, stage).Contains(p);
                    foreach (var line in p.VisibleLines())
                    {
                        bool found = GlobalSearch.Run(db, stage, line, text, time).Any(h => h.target.Key == target.Key);
                        if (expected && !found)
                            error($"{stage}: 사진 표시 문구로 검색해도 그 사진이 나오지 않습니다('{line}'): {Path(p)}", p);
                        else if (!expected && found)
                            error($"{stage}: 접근 전 사진의 표시 문구가 검색됩니다('{line}'): {Path(p)}", p);
                    }
                }
                foreach (var device in cases)
                {
                    bool leak = PhotoQuery.DevicePhotos(db, device, stage).Any(p => p.availableFrom > stage)
                                || RecordQuery.For(db.browser, device, stage).Any(r => r.availableFrom > stage)
                                || RecordQuery.For(db.maps, device, stage).Any(r => r.availableFrom > stage)
                                || RecordQuery.For(db.files, device, stage).Any(r => r.availableFrom > stage)
                                || RecordQuery.For(db.settings, device, stage).Any(r => r.availableFrom > stage)
                                || RecordQuery.For(db.mails, device, stage).Any(r => r.availableFrom > stage)
                                || DeviceQuery.Threads(db, device, stage).Any(e => e.state.availableFrom > stage);
                    if (leak)
                        error($"{stage}에서 {device.name}의 앱 목록·개수·위치에 접근 전 기록이 포함됩니다.", device);
                }
            }

            // ── 진행 저장 파일: 사라진 작업·의뢰·알림 문구 참조
            string progressPath = System.IO.Path.Combine(Application.persistentDataPath, config.progressSaveFileName);
            if (System.IO.File.Exists(progressPath))
            {
                var progress = new ProgressStore(progressPath, config.startStage);
                foreach (var id in progress.JobIds.Where(id => !jobs.Any(j => j.Id == id)))
                    warn($"진행 저장 파일에 원본에 없는 작업 ID가 있습니다: {id} ({progressPath})", null);
                foreach (var s in progress.Summaries.Where(s => !jobs.Any(j => j.Id == s.jobId)))
                    warn($"진행 저장 파일의 연동 결과 요약이 원본에 없는 작업을 가리킵니다: {s.jobId} ({progressPath})", null);
                foreach (var n in progress.Notifications)
                {
                    if (!string.IsNullOrEmpty(n.caseId) && !cases.Any(c => c.Id == n.caseId))
                        warn($"진행 저장 파일의 알림이 원본에 없는 의뢰를 가리킵니다: {n.caseId} ({progressPath})", null);
                    if (config.strings != null && !config.strings.Contains(n.templateKey))
                        warn($"진행 저장 파일의 알림 문구 키가 문자열 테이블에 없습니다: {n.templateKey} ({progressPath})", null);
                }
            }

            // ── 삭제 대기 조회: 삭제 후보의 누락·중복 원본 (원본은 바꾸지 않는다)
            string workPath = System.IO.Path.Combine(Application.persistentDataPath, config.workSaveFileName);
            if (System.IO.File.Exists(workPath))
            {
                var work = new WorkStore(workPath);
                foreach (var e in DeletionQueue.Compute(work, db, ProgressStage.P6, text))
                {
                    if (e.state == AccessState.Missing || e.state == AccessState.Duplicate)
                        warn($"삭제 대기 조회에 원본을 {(e.state == AccessState.Duplicate ? "하나로 정할 수 없는(ID 중복)" : "찾을 수 없는")} 항목이 있습니다: {e.target.Key} ({workPath})", null);
                }
            }
        }

        /// <summary>
        /// 검토 분류는 `최근 사진 N`처럼 기기 목록의 최근순 상위 N건 전체여야 한다(꼭 봐야 하는 자료가 N번째면 그 위 최근 자료까지 포함).
        /// 일상 자료 날짜가 바뀌어 대상이 실제 상위 N건과 달라지면 경고한다. 한 분류는 한 종류(사진·대화방·브라우저·지도·파일·설정)만 담는다.
        /// </summary>
        static void CheckRecentPrefix(ContentDatabase db, ReviewBatchData batch, ReviewGroup group, ProgressStage stage, Action<string, Object> warn)
        {
            var records = group.targets.Where(t => t != null && t.record != null).Select(t => t.record).ToList();
            if (records.Count == 0)
                return;
            string where = $"{group.labelKey} ({Path(batch)})";
            var device = batch.device;
            List<ContentAsset> ordered;
            static List<ContentAsset> ByTime<T>(IEnumerable<T> items) where T : DeviceRecord =>
                items.OrderByDescending(r => r.time.TotalMinutes).ThenByDescending(r => r.sortKey).Cast<ContentAsset>().ToList();
            if (records.All(r => r is PhotoData))
                ordered = PhotoQuery.DevicePhotos(db, device, stage)
                    .OrderByDescending(p => p.takenAt.TotalMinutes).ThenByDescending(p => p.sortKey).Cast<ContentAsset>().ToList();
            else if (records.All(r => r is ThreadData))
                ordered = DeviceQuery.Threads(db, device, stage).Select(e => (ContentAsset)e.thread).ToList();
            else if (records.All(r => r is BrowserRecord))
            {
                // 검색 기록만 담은 분류는 검색 기록끼리, 방문 기록만 담은 분류는 방문끼리 비교한다.
                var kinds = records.Cast<BrowserRecord>().Select(r => r.kind).Distinct().ToList();
                ordered = ByTime(RecordQuery.For(db.browser, device, stage).Where(r => kinds.Count != 1 || r.kind == kinds[0]));
            }
            else if (records.All(r => r is MapRecord))
                ordered = ByTime(RecordQuery.For(db.maps, device, stage));
            else if (records.All(r => r is FileRecord))
                ordered = ByTime(RecordQuery.For(db.files, device, stage));
            else if (records.All(r => r is SettingRecord))
                ordered = ByTime(RecordQuery.For(db.settings, device, stage));
            else if (records.All(r => r is MailRecord))
                ordered = ByTime(RecordQuery.For(db.mails, device, stage));
            else
            {
                warn($"검토 분류에 서로 다른 종류의 자료가 섞여 있어 최근순 상위 N건인지 확인할 수 없습니다: {where}", batch);
                return;
            }
            var expected = ordered.Take(records.Count).ToList();
            var missing = expected.Where(e => !records.Contains(e)).ToList();
            if (missing.Count == 0)
                return;
            var extra = records.Where(r => !expected.Contains(r)).ToList();
            warn($"검토 분류가 기기 목록의 최근순 상위 {records.Count}건과 다릅니다({stage} 기준) — 빠진 최근 자료: {string.Join(", ", missing.Select(m => m.name))} / 상위 밖 대상: {string.Join(", ", extra.Select(m => m.name))}. 대상을 늘리거나 다시 맞추세요: {where}", batch);
        }

        static bool CompletionReaches(ProgressJobData from, ProgressJobData target, HashSet<ProgressJobData> visited)
        {
            foreach (var e in from.onComplete.Where(e => e != null && e.kind == ProgressEffectKind.CompleteJob && e.job != null))
            {
                if (e.job == target)
                    return true;
                if (visited.Add(e.job) && CompletionReaches(e.job, target, visited))
                    return true;
            }
            return false;
        }

        static readonly Regex Token = new Regex(@"\{([^{}]+)\}");

        /// <summary>알림 문구 키가 있고, 쓸 수 있는 토큰만 쓰며, 필요한 토큰 값(의뢰)이 지정됐는지.</summary>
        static void CheckTemplate(GameConfig config, ProgressEffect e, string where, Object ctx, Action<string, Object> error)
        {
            if (string.IsNullOrEmpty(e.templateKey))
            {
                error($"알림 효과의 문구 키가 비어 있습니다: {where}", ctx);
                return;
            }
            if (config.strings == null || !config.strings.TryGet(e.templateKey, out var template))
            {
                error($"알림 문구 키 '{e.templateKey}'가 문자열 테이블에 없습니다: {where}", ctx);
                return;
            }
            foreach (Match m in Token.Matches(template))
            {
                string token = m.Groups[1].Value;
                if (!UIKeys.NotificationTokens.Contains(token))
                    error($"알림 문구 '{e.templateKey}'에 지원하지 않는 토큰 {{{token}}}이 있습니다: {where}", ctx);
                else if ((token == "caseDisplayName" || token == "count") && e.caseToken == null)
                    error($"알림 문구 '{e.templateKey}'의 {{{token}}}에 쓸 의뢰(caseToken)가 비어 있습니다: {where}", ctx);
            }
        }
    }
}
