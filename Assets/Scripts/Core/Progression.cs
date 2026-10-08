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

        bool IsMet(ProgressJobData job, ProgressCondition c)
        {
            switch (c.kind)
            {
                case ProgressConditionKind.TutorialCompleted:
                {
                    if (c.tutorial == null)
                        return false;
                    var steps = TutorialProgress.Evaluate(c.tutorial, work);
                    return steps.Count > 0 && steps.All(s => s.status == StepStatus.Done);
                }
                case ProgressConditionKind.RequestViewed:
                    return c.request != null && work.IsViewed(RecordRef.ForRequest(c.request).Key);
                case ProgressConditionKind.ClassifiedRecords:
                    return ClassifiedCount(job.device, c.source) >= c.minCount;
                case ProgressConditionKind.JobCompleted:
                    return StateOf(c.job) == JobState.Completed;
                default:
                    return false;
            }
        }

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

        /// <summary>플레이어가 작업을 시작한다. 조건을 만족하지 않았거나 이미 시작했으면 false.</summary>
        public bool Start(ProgressJobData job)
        {
            if (StateOf(job) != JobState.NotStarted || !ConditionsMet(job))
                return false;
            store.SetJob(job.Id, JobState.Running);
            Apply(job.onStart);
            return true;
        }

        /// <summary>작업을 완료한다(다른 작업의 효과 또는 이후 정식 콘텐츠·에디터 검수). 완료 효과를 한 번 실행한다.</summary>
        public void Complete(ProgressJobData job)
        {
            if (job == null || StateOf(job) == JobState.Completed)
                return;
            store.SetJob(job.Id, JobState.Completed);
            Apply(job.onComplete);
        }

        void Apply(IEnumerable<ProgressEffect> effects)
        {
            foreach (var e in effects.Where(e => e != null))
            {
                switch (e.kind)
                {
                    case ProgressEffectKind.RaiseStage:
                        if (e.stage > store.Stage)
                            store.SetStage(e.stage);
                        break;
                    case ProgressEffectKind.CompleteJob:
                        Complete(e.job);
                        break;
                    case ProgressEffectKind.Notify:
                        store.AddNotification(e.Id, e.templateKey, e.caseToken != null ? e.caseToken.Id : null,
                            e.caseToken != null ? UnclassifiedCount(e.caseToken) : 0);
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
