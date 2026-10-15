using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    public enum StatusProgress
    {
        /// <summary>진행 표시 없음 (기기 접근 대기).</summary>
        None,
        /// <summary>완료 시점이 사건 진행 이벤트라 비율을 알 수 없는 진행 중.</summary>
        Indeterminate,
        /// <summary>완료 (100%).</summary>
        Done,
    }

    /// <summary>한 의뢰의 현재 업무 상태. 모든 값은 진행 데이터와 저장 상태에서 계산한다.</summary>
    public struct CaseWorkStatus
    {
        /// <summary>상단 상태 문자열 키.</summary>
        public string statusKey;
        /// <summary>좌측 의뢰 행의 짧은 상태 문자열 키.</summary>
        public string shortKey;
        /// <summary>지금 확인할 수 있는 자료 범위 (예: 휴대전화 로컬 / 로컬 · 연동). 진행 중인 작업은 끝나기 전까지 넣지 않는다.</summary>
        public string sourcesKey;
        public StatusProgress progress;
    }

    /// <summary>검토 묶음 분류 하나의 진행률.</summary>
    public class ReviewGroupProgress
    {
        public ReviewGroup group;
        public int done;
        public int total;
        /// <summary>아직 열지 않은 대상 (정렬값 순서).</summary>
        public List<(int order, int index, RecordRef target)> pending = new List<(int, int, RecordRef)>();
    }

    /// <summary>검토 묶음 전체 진행률. 상단 진행률·검토 팝오버·진행 판정이 같은 값을 쓴다.</summary>
    public class ReviewProgress
    {
        public ReviewBatchData batch;
        public List<ReviewGroupProgress> groups = new List<ReviewGroupProgress>();
        public int Done;
        public int Total;
        /// <summary>묶음 전체에서 아직 열지 않은 대상 (정렬값 → 분류 순서 → 분류 안 순서).</summary>
        public List<RecordRef> pending = new List<RecordRef>();
        public bool Complete => Total > 0 && Done == Total;
    }

    /// <summary>
    /// 진행 연동. 의뢰별 작업(인벤토리·복구)의 시작 조건과 효과를 데이터에서 읽어 접근 단계·작업 상태·알림을 바꾼다.
    /// 코드는 특정 의뢰·기록 ID를 비교하지 않는다. 검색·알림·진행은 플레이어 대신 추리하거나 다음에 볼 앱을 지시하지 않는다.
    /// </summary>
    public class Progression
    {
        readonly ContentDatabase db;
        readonly WorkStore work;
        readonly ProgressStore store;
        readonly UIText text;

        public Progression(ContentDatabase db, WorkStore work, ProgressStore store, UIText text)
        {
            this.db = db;
            this.work = work;
            this.store = store;
            this.text = text;
        }

        public ProgressStage Stage => store.Stage;
        public ProgressStore Store => store;

        public IEnumerable<ProgressJobData> JobsFor(CaseData device) =>
            db.jobs.Where(j => j != null && j.device == device).OrderBy(j => j.order);

        public IEnumerable<ProgressJobData> JobsForRequest(WorkRequestData request) =>
            db.jobs.Where(j => j != null && j.request == request).OrderBy(j => j.order);

        public JobState StateOf(ProgressJobData job) => job == null ? JobState.NotStarted : store.StateOf(job.Id);

        /// <summary>시작 조건을 모두 만족했는지. 기기에 접근할 수 없으면 만족하지 않는다.</summary>
        /// <summary>
        /// 패널에 보여줄 작업인지. 다른 작업의 완료를 기다리는 작업은 그 작업이 끝나기 전까지 보이지 않는다
        /// (아직 오지 않은 업무를 미리 알려주지 않는다). 시작한 작업은 늘 보인다.
        /// </summary>
        public bool Visible(ProgressJobData job) =>
            job != null && (StateOf(job) != JobState.NotStarted
                            || job.startConditions.Where(c => c != null && c.kind == ProgressConditionKind.JobCompleted).All(c => StateOf(c.job) == JobState.Completed));

        public bool ConditionsMet(ProgressJobData job)
        {
            if (job == null || job.device == null || !job.device.IsAvailable(Stage))
                return false;
            return job.startConditions.All(c => c != null && IsMet(job, c));
        }

        /// <summary>
        /// 진행 중인 작업의 완료 조건을 모두 만족했는지. 완료 조건이 없거나, 진행 중이 아니거나, 기기에 접근할 수 없으면 false.
        /// 열람 기준은 시작 조건과 같다(저장된 열람 상태 + 지금 접근 가능한 원본). 이미 완료한 작업은 되돌리지 않는다.
        /// </summary>
        public bool CompletionMet(ProgressJobData job) =>
            job != null && job.completionConditions.Count > 0 && StateOf(job) == JobState.Running
            && job.device != null && job.device.IsAvailable(Stage)
            && job.completionConditions.All(c => c != null && IsMet(job, c));

        /// <summary>조건 하나의 충족 여부 (에디터 진행 검수 창용, 판정은 시작·완료 조건과 같다).</summary>
        public bool ConditionMet(ProgressJobData job, ProgressCondition c) => c != null && IsMet(job, c);

        /// <summary>
        /// 자동 완료가 켜진 진행 중 작업 중 완료 조건을 모두 만족한 것을 완료한다(작업마다 Complete 한 트랜잭션, 완료 효과 한 번).
        /// 완료가 다른 작업을 준비 상태로 만들 수 있어 바뀐 것이 없을 때까지 반복한다. 무엇이든 완료했으면 true.
        /// 열람 상태가 바뀐 뒤·세이브 복원 뒤·진행이 바뀐 뒤 같은 경로로 부른다. 이미 완료한 작업은 다시 완료하지 않는다.
        /// </summary>
        public bool AutoComplete()
        {
            bool any = false;
            for (int guard = 0; guard <= db.jobs.Count; guard++)
            {
                var ready = db.jobs
                    .Where(j => j != null && j.autoCompleteWhenReady && CompletionMet(j))
                    .OrderBy(j => j.device.order).ThenBy(j => j.order)
                    .FirstOrDefault();
                if (ready == null || !Complete(ready))
                    break;
                any = true;
            }
            return any;
        }

        /// <summary>
        /// 아직 시작하지 않은 작업 중 시작 조건을 모두 만족한 작업의 준비 효과(onReady)를 실행한다. 효과는 불변 효과 ID로 한 번만
        /// 실행된다(이미 실행된 효과는 건너뜀). 이미 시작·완료한 작업은 대상이 아니다. 실행할 것이 있으면 한 트랜잭션으로 처리해
        /// 전부 반영됐을 때만 한 번 저장하고, 무엇이든 실행했으면 true.
        /// </summary>
        public bool ApplyReady()
        {
            var due = db.jobs
                .Where(j => j != null && StateOf(j) == JobState.NotStarted
                            && j.onReady.Any(ReadyPending) && ConditionsMet(j))
                .OrderBy(j => j.device.order).ThenBy(j => j.order)
                .ToList();
            if (due.Count == 0)
                return false;
            return store.Transact(() =>
            {
                foreach (var job in due)
                    Apply(job, job.onReady.Where(ReadyPending).ToList());
            });
        }

        /// <summary>
        /// 아직 실행하지 않은 준비 효과. 준비 효과는 업무 알림만 쓴다(효과 ID로 한 번만 만들어지는 효과). 다른 종류는 검증 도구가 오류로 잡고
        /// 실행 중에도 건너뛴다 — 작업 시작·단계 변경은 플레이어가 상단에서 직접 한다.
        /// </summary>
        bool ReadyPending(ProgressEffect e) => e != null && e.kind == ProgressEffectKind.Notify && !store.HasFired(e.Id);

        /// <summary>
        /// 진행 평가 한 번: 자동 완료 → 준비 효과. 열람 상태 변경·세이브 복원·진행 변경 뒤 같은 경로로 부른다.
        /// 여러 번 불러도 이미 완료한 작업·이미 실행한 효과는 다시 처리하지 않는다. 무엇이든 바뀌었으면 true.
        /// </summary>
        public bool Evaluate()
        {
            bool changed = AutoComplete();
            changed |= ApplyReady();
            return changed;
        }

        /// <summary>
        /// 열람 묶음에서 실제로 연 서로 다른 원본 수. 같은 원본(RecordRef.Key)은 한 번만 세고,
        /// 누락·중복·현재 접근 불가 원본은 열람 이력이 있어도 세지 않는다.
        /// </summary>
        public int GroupViewedCount(ProgressRecordGroupData group)
        {
            if (group == null)
                return 0;
            var keys = new HashSet<string>();
            foreach (var t in group.targets)
            {
                var target = t != null ? TargetRef(t.device, t.record) : null;
                if (target != null && Opened(target))
                    keys.Add(target.Key);
            }
            return keys.Count;
        }

        /// <summary>실제로 원본을 연 기록(WorkStore viewed)이면서 지금 접근 가능하고 하나로 정해지는지.</summary>
        bool Opened(RecordRef target) =>
            target != null && work.IsViewed(target.Key) && RecordAccess.Check(target, db, Stage, text) == AccessState.Accessible;

        bool IsMet(ProgressJobData job, ProgressCondition c)
        {
            switch (c.kind)
            {
                case ProgressConditionKind.RecordKept:
                {
                    // 지금 분류만 본다. 한 번 보존했다가 해제했으면 조건을 만족하지 않는다(이미 시작한 작업은 되돌리지 않음).
                    // 원본이 지금 접근 가능하고 하나로 정해질 때만 인정한다(앱 목록·검색과 같은 RecordAccess 판정).
                    var target = RecordRef.ForAsset(c.record);
                    return target != null && work.ClassOf(target.Key) == Classification.Keep
                           && RecordAccess.Check(target, db, Stage, text) == AccessState.Accessible;
                }
                case ProgressConditionKind.RequestViewed:
                    return c.request != null && work.IsViewed(RecordRef.ForRequest(c.request).Key);
                case ProgressConditionKind.RecordViewed:
                {
                    // 실제로 원본을 연 기록(WorkStore의 viewed)만 인정한다. 검색 결과·고정·비교 슬롯은 viewed를 남기지 않는다.
                    // 원본이 지금 접근 가능하고 하나로 정해질 때만 인정한다(누락·중복·접근 불가 원본은 저장 이력이 있어도 불인정).
                    return Opened(ViewTarget(job, c));
                }
                case ProgressConditionKind.RecordGroupViewed:
                    // 서로 다른 원본 수만 센다. 비교·고정·분류·검색 노출은 열람 상태를 만들지 않는다.
                    return c.group != null && c.group.targets.Count > 0 && GroupViewedCount(c.group) >= Math.Max(1, c.group.minCount);
                case ProgressConditionKind.ReviewBatchViewed:
                    // 상단 진행률·검토 팝오버와 같은 대상 목록·같은 열람 기준을 쓴다.
                    return c.batch != null && Review(c.batch).Complete;
                case ProgressConditionKind.ClassifiedRecords:
                    return ClassifiedCount(job.device, c.source) >= c.minCount;
                case ProgressConditionKind.JobCompleted:
                    return StateOf(c.job) == JobState.Completed;
                default:
                    return false;
            }
        }

        /// <summary>
        /// RecordViewed 조건의 열람 키. 대화방은 같은 원본이 여러 기기에 있으므로 조건의 device 기기(비우면 작업 의뢰 기기)의 대화방
        /// (<see cref="RecordRef.ForThread"/>)으로 정하고, 그 외에는 <see cref="RecordRef.ForAsset"/>과 같다. 지원하지 않으면 null.
        /// </summary>
        public static RecordRef ViewTarget(ProgressJobData job, ProgressCondition c) =>
            c == null ? null : TargetRef(ViewDevice(job, c), c.record);

        /// <summary>
        /// RecordViewed 조건 대상을 판정할 기기 (런타임 판정과 검증 도구가 함께 쓰는 규칙). 대화방은 조건의 device, 없으면 작업 의뢰 기기.
        /// 사진·기록은 조건의 device(지정했을 때만, 원본 기기와 같아야 함) — 비우면 null이고 원본 자신의 기기로 판정한다.
        /// </summary>
        public static CaseData ViewDevice(ProgressJobData job, ProgressCondition c) =>
            c == null ? null
            : c.record is ThreadData ? (c.device != null ? c.device : job?.device)
            : c.device;

        /// <summary>
        /// 기기와 원본 에셋으로 정한 열람 키. 대화방은 그 기기의 대화방(기기가 없으면 null), 사진·기록은 원본 자신의 기기
        /// (<see cref="RecordRef.ForAsset"/>, 지정한 기기와 다르면 null — 다른 기기의 원본으로 대신 인정하지 않는다).
        /// </summary>
        public static RecordRef TargetRef(CaseData device, ContentAsset record)
        {
            if (record is ThreadData thread)
                return device != null ? RecordRef.ForThread(device, thread) : null;
            var target = RecordRef.ForAsset(record);
            if (target != null && device != null && target.deviceId != device.Id)
                return null;
            return target;
        }

        // ───────────── 검토 묶음 (온보딩) ─────────────

        /// <summary>검토 묶음 대상의 열람 키. 대화방은 묶음 의뢰 기기의 대화방, 그 외는 <see cref="RecordRef.ForAsset"/>. 지원하지 않으면 null.</summary>
        public static RecordRef ReviewTargetRef(ReviewBatchData batch, ContentAsset record) =>
            record is ThreadData thread
                ? (batch != null && batch.device != null ? RecordRef.ForThread(batch.device, thread) : null)
                : RecordRef.ForAsset(record);

        /// <summary>
        /// 검토 묶음의 진행률. 실제로 원본을 연 기록(WorkStore viewed)이면서 지금 접근 가능하고 하나로 정해지는 대상만 확인으로 센다
        /// (RecordViewed 조건과 같은 기준). 아직 열지 않은 대상은 정렬값 순서로 돌려준다.
        /// </summary>
        public ReviewProgress Review(ReviewBatchData batch)
        {
            var result = new ReviewProgress { batch = batch };
            if (batch == null)
                return result;
            var pending = new List<(int order, int group, int index, RecordRef target)>();
            for (int g = 0; g < batch.groups.Count; g++)
            {
                var group = batch.groups[g];
                if (group == null)
                    continue;
                var gp = new ReviewGroupProgress { group = group };
                for (int i = 0; i < group.targets.Count; i++)
                {
                    var t = group.targets[i];
                    var target = t != null ? ReviewTargetRef(batch, t.record) : null;
                    gp.total++;
                    if (target != null && work.IsViewed(target.Key)
                        && RecordAccess.Check(target, db, Stage, text) == AccessState.Accessible)
                        gp.done++;
                    else if (target != null)
                    {
                        gp.pending.Add((t.order, i, target));
                        pending.Add((t.order, g, i, target));
                    }
                }
                gp.pending.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.index.CompareTo(b.index));
                result.groups.Add(gp);
                result.Done += gp.done;
                result.Total += gp.total;
            }
            result.pending = pending
                .OrderBy(p => p.order).ThenBy(p => p.group).ThenBy(p => p.index)
                .Select(p => p.target).ToList();
            return result;
        }

        /// <summary>작업의 시작 조건에 걸린 검토 묶음 (없으면 null).</summary>
        public static ReviewBatchData BatchOf(ProgressJobData job) =>
            job?.startConditions.FirstOrDefault(c => c != null && c.kind == ProgressConditionKind.ReviewBatchViewed)?.batch;

        /// <summary>작업의 시작 조건 중 아직 열람하지 않은 의뢰 요청 (없으면 null).</summary>
        public WorkRequestData UnviewedRequest(ProgressJobData job) =>
            job?.startConditions
                .Where(c => c != null && c.kind == ProgressConditionKind.RequestViewed && c.request != null)
                .Select(c => c.request)
                .FirstOrDefault(r => !work.IsViewed(RecordRef.ForRequest(r).Key));

        /// <summary>이 의뢰에서 아직 시작하지 않았고 지금 보이는 첫 작업 (순서 기준).</summary>
        public ProgressJobData NextJob(CaseData device) =>
            JobsFor(device).FirstOrDefault(j => StateOf(j) == JobState.NotStarted && Visible(j));

        // ───────────── 연동 결과 요약 ─────────────

        /// <summary>작업 완료 효과 중 연동 결과 요약 효과 (없으면 null).</summary>
        public static ProgressEffect SummaryEffect(ProgressJobData job) =>
            job?.onComplete.FirstOrDefault(e => e != null && e.kind == ProgressEffectKind.SyncSummary);

        /// <summary>
        /// 연동 결과 요약의 자료 목록: 작업 의뢰 기기에서 그 작업이 연 출처(unlocksSources)의 기록 중 지금 RecordAccess로 접근 가능한 원본.
        /// 같은 원본은 RecordRef.Key로 한 번만 센다. 저장하지 않고 매번 현재 데이터에서 계산한다.
        /// </summary>
        public List<RecordRef> SummaryRecords(ProgressJobData job)
        {
            var result = new List<RecordRef>();
            var device = job?.device;
            if (device == null || job.unlocksSources.Count == 0)
                return result;
            var sources = new HashSet<RecordSource>(job.unlocksSources);
            var seen = new HashSet<string>();
            void Add(RecordRef r)
            {
                if (r != null && RecordAccess.Check(r, db, Stage, text) == AccessState.Accessible && seen.Add(r.Key))
                    result.Add(r);
            }
            foreach (var e in DeviceQuery.Threads(db, device, Stage).Where(e => sources.Contains(e.state.source)))
                Add(RecordRef.ForThread(device, e.thread));
            foreach (var p in PhotoQuery.DevicePhotos(db, device, Stage).Where(p => sources.Contains(p.source)))
                Add(RecordRef.ForPhoto(p));
            foreach (var r in RecordQuery.For(db.browser, device, Stage).Where(r => sources.Contains(r.source)))
                Add(RecordRef.ForRecord(r));
            foreach (var r in RecordQuery.For(db.maps, device, Stage).Where(r => sources.Contains(r.source)))
                Add(RecordRef.ForRecord(r));
            foreach (var r in RecordQuery.For(db.files, device, Stage).Where(r => sources.Contains(r.source)))
                Add(RecordRef.ForRecord(r));
            foreach (var r in RecordQuery.For(db.settings, device, Stage).Where(r => sources.Contains(r.source)))
                Add(RecordRef.ForRecord(r));
            foreach (var r in RecordQuery.For(db.mails, device, Stage).Where(r => sources.Contains(r.source)))
                Add(RecordRef.ForRecord(r));
            return result;
        }

        /// <summary>이 의뢰에서 연동 결과 요약을 다시 열 수 있는 가장 나중에 완료된 작업 (없으면 null).</summary>
        public ProgressJobData SummaryJob(CaseData device) =>
            JobsFor(device).Where(j => StateOf(j) == JobState.Completed && SummaryEffect(j) != null).LastOrDefault();

        /// <summary>이 의뢰에서 지금 접근할 수 있는 source 출처 기록 중 보존·삭제 후보로 분류한 수 (의뢰 요청 제외).</summary>
        public int ClassifiedCount(CaseData device, RecordSource source) =>
            work.All.Count(e => e.classification != Classification.Unclassified && e.target != null
                                && e.target.deviceId == device.Id && e.target.kind != RecordKind.Request
                                && RecordAccess.Check(e.target, db, Stage, text, out var res) == AccessState.Accessible
                                && res.source == source);

        /// <summary>열람했지만 아직 보존·삭제를 정하지 않은 이 의뢰의 접근 가능한 기록 수 (알림의 {count}).</summary>
        public int UnclassifiedCount(CaseData device) =>
            work.All.Count(e => e.viewed && e.classification == Classification.Unclassified && e.target != null
                                && e.target.deviceId == device.Id && e.target.kind != RecordKind.Request
                                && RecordAccess.Check(e.target, db, Stage, text) == AccessState.Accessible);

        /// <summary>
        /// 플레이어가 작업을 시작한다. 조건을 만족하지 않았거나 이미 시작했으면 false.
        /// 작업 상태·시작 효과(단계 변경·다른 작업 완료·알림)를 한 트랜잭션으로 처리해 전부 반영됐을 때만 한 번 저장한다.
        /// 도중에 실패하면 작업은 시작 전 상태로 남아 다시 시작할 수 있다.
        /// </summary>
        public bool Start(ProgressJobData job)
        {
            if (StateOf(job) != JobState.NotStarted || !ConditionsMet(job))
                return false;
            return store.Transact(() =>
            {
                store.SetJob(job.Id, JobState.Running);
                ProgressStore.Fault(ProgressFaultPoint.JobState, job.Id);
                Apply(job, job.onStart);
                // 시작하는 순간 완료 조건까지 이미 충족되는 자동 완료 작업은 같은 트랜잭션에서 완료한다
                // (완료 효과·단계·알림·요약 기록이 시작과 함께 한 번에 저장됨). 그렇지 않은 작업은 기존과 같다.
                if (job.autoCompleteWhenReady && CompletionMet(job))
                    CompleteCore(job);
            });
        }

        /// <summary>작업을 완료한다(다른 작업의 효과 또는 이후 정식 콘텐츠·에디터 검수). 완료 효과를 한 번 실행한다.
        /// 연쇄 완료·단계 변경·알림까지 한 트랜잭션이며, 실패하면 아무것도 바뀌지 않은 상태로 false를 돌려준다.</summary>
        public bool Complete(ProgressJobData job)
        {
            if (job == null || StateOf(job) == JobState.Completed)
                return false;
            return store.Transact(() => CompleteCore(job));
        }

        /// <summary>트랜잭션 안에서만 부른다. CompleteJob 효과의 연쇄 완료도 같은 트랜잭션에 포함된다.</summary>
        void CompleteCore(ProgressJobData job)
        {
            if (job == null || StateOf(job) == JobState.Completed)
                return;
            store.SetJob(job.Id, JobState.Completed);
            ProgressStore.Fault(ProgressFaultPoint.JobState, job.Id);
            Apply(job, job.onComplete);
        }

        void Apply(ProgressJobData owner, IEnumerable<ProgressEffect> effects)
        {
            foreach (var e in effects.Where(e => e != null))
            {
                switch (e.kind)
                {
                    case ProgressEffectKind.RaiseStage:
                        if (e.stage > store.Stage)
                        {
                            store.SetStage(e.stage);
                            ProgressStore.Fault(ProgressFaultPoint.Stage, e.stage.ToString());
                        }
                        break;
                    case ProgressEffectKind.CompleteJob:
                        CompleteCore(e.job);
                        ProgressStore.Fault(ProgressFaultPoint.ChainComplete, e.job != null ? e.job.Id : null);
                        break;
                    case ProgressEffectKind.Notify:
                        ProgressStore.Fault(ProgressFaultPoint.NotifyBefore, e.Id);
                        store.AddNotification(e.Id, e.templateKey, e.caseToken != null ? e.caseToken.Id : null,
                            e.caseToken != null ? UnclassifiedCount(e.caseToken) : 0);
                        ProgressStore.Fault(ProgressFaultPoint.NotifyAfter, e.Id);
                        break;
                    case ProgressEffectKind.SyncSummary:
                        // 요약 내용은 저장하지 않고 표시할 때 계산한다. 저장하는 것은 만들어졌다는 사실과 확인 여부뿐이다.
                        store.AddSummary(e.Id, owner != null ? owner.Id : null, e.caseToken != null ? e.caseToken.Id : null);
                        ProgressStore.Fault(ProgressFaultPoint.Summary, e.Id);
                        break;
                }
            }
        }

        /// <summary>
        /// 의뢰의 업무 상태: 접근 대기 → 로컬 인덱싱 중 → 로컬 데이터 확인 가능 → (가장 나중에 시작한 작업의) 진행 중/완료.
        /// 비율을 지어내지 않고 진행 중은 Indeterminate, 끝난 상태는 Done으로 돌려준다.
        /// </summary>
        public CaseWorkStatus StatusOf(CaseData device)
        {
            if (device == null || !device.IsAvailable(Stage))
                return new CaseWorkStatus { statusKey = UIKeys.CaseStatusWaiting, shortKey = UIKeys.CaseStatusWaiting, sourcesKey = UIKeys.CaseStatusWaiting, progress = StatusProgress.None };
            var done = JobsFor(device).Where(j => StateOf(j) == JobState.Completed).LastOrDefault();
            string sources = done != null ? done.shortDoneKey : UIKeys.CaseStatusLocal;
            if (Stage < device.localIndexedFrom)
                return new CaseWorkStatus { statusKey = UIKeys.TopIndexRunning, shortKey = UIKeys.CaseStatusLocal, sourcesKey = sources, progress = StatusProgress.Indeterminate };
            var latest = JobsFor(device).Where(j => StateOf(j) != JobState.NotStarted).LastOrDefault();
            if (latest == null)
                return new CaseWorkStatus { statusKey = UIKeys.StatusLocalReady, shortKey = UIKeys.CaseStatusLocal, sourcesKey = sources, progress = StatusProgress.Done };
            bool running = StateOf(latest) == JobState.Running;
            return new CaseWorkStatus
            {
                statusKey = running ? latest.runningKey : latest.doneKey,
                shortKey = running ? latest.shortRunningKey : latest.shortDoneKey,
                sourcesKey = sources,
                progress = running ? StatusProgress.Indeterminate : StatusProgress.Done,
            };
        }

        // ───────────── 에디터 검수 ─────────────

        /// <summary>검수용: 효과·알림 없이 접근 단계만 바꾼다.</summary>
        public void DebugSetStage(ProgressStage stage) => store.SetStage(stage);

        /// <summary>검수용: 효과·알림 없이 작업 상태만 바꾼다.</summary>
        public void DebugSetJob(ProgressJobData job, JobState state)
        {
            if (job != null)
                store.SetJob(job.Id, state);
        }
    }
}
