using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>
    /// 상단 업무 상태 컨트롤 · 검토 진행 팝오버 · 연동 결과 요약.
    /// 상단 컨트롤은 현재 선택된 의뢰의 통합 업무 상태다: 요청 미열람 → (초반 업무 대상 상태 | 검토 묶음 진행률) → 연동 시작 → 인덱싱 중 → 연동 완료.
    /// 무엇을 보여줄지는 진행 작업·검토 묶음·초반 업무 데이터에서 계산하며 코드는 특정 의뢰·기록을 알지 못한다.
    /// P0·P1 온보딩에서만 중립적인 자료 종류·검토 수량을 보여주고, 증거 ID·관계·정답은 보여주지 않는다.
    /// </summary>
    public partial class AppShell
    {
        [Header("업무 상태 컨트롤·연동 결과 요약")]
        [SerializeField] ReviewPopoverView reviewPopover;
        [SerializeField] SyncSummaryView summaryView;

        /// <summary>지금 상단 컨트롤을 눌렀을 때 할 일 (RenderTopBar가 정한다).</summary>
        System.Action statusAction;
        /// <summary>팝오버가 보여주는 검토 묶음 (현재 의뢰의 다음 작업).</summary>
        ReviewBatchData popoverBatch;
        /// <summary>요약 화면이 보여주는 완료 작업.</summary>
        ProgressJobData summaryJob;
        /// <summary>이번 실행 중 미완료로 표시한 검토 묶음 ID. 완료로 바뀌는 순간 완료 팝오버를 한 번 보여주는 데만 쓴다(저장 안 함).</summary>
        readonly HashSet<string> reviewIncomplete = new HashSet<string>();
        bool showDonePopover;

        bool SummaryOpen => summaryView.IsOpen;

        // ───────────── 상단 업무 상태 ─────────────

        void RenderTopBar()
        {
            var c = session.CurrentCase;
            var palette = new TopBarView.Palette
            {
                linkColor = Theme.panelRaised,
                openColor = Theme.accentSoft,
                primaryColor = Theme.accentSoft,
                plainText = Theme.subText,
                linkText = Theme.text,
                primaryText = Theme.accent,
            };
            statusAction = null;
            popoverBatch = null;
            if (c == null)
            {
                topBar.Show(string.Empty, new TopBarView.Status { text = string.Empty }, palette);
                CloseReviewPopover();
                return;
            }
            var status = progression.StatusOf(c);
            showDonePopover = false;
            var view = TopStatus(c, status);
            topBar.Show(text.Format(UIKeys.TopCaseStatus, ("case", c.DisplayName), ("status", text.Get(status.sourcesKey))),
                view, palette);
            if (showDonePopover && !reviewPopover.IsOpen)
            {
                CloseNotifications();
                reviewPopover.Open(CloseReviewPopover);
            }
            // 팝오버는 지금 의뢰에 검토 묶음이 있을 때만 유지한다.
            if (reviewPopover.IsOpen)
            {
                if (popoverBatch == null)
                    CloseReviewPopover();
                else
                    RenderReviewPopover();
            }
        }

        /// <summary>현재 의뢰의 상단 상태와 누르면 할 일. 진행 표시는 실제 비율을 알 때만 비율로 보여준다.</summary>
        TopBarView.Status TopStatus(CaseData c, CaseWorkStatus status)
        {
            var plain = new TopBarView.Status
            {
                text = text.Get(status.statusKey),
                style = TopBarView.StatusStyle.Plain,
                indeterminate = status.progress == StatusProgress.Indeterminate,
                progress = status.progress == StatusProgress.Done ? 1f : 0f,
            };
            // 기기 접근 전·로컬 인덱싱 중·연동 작업 진행 중은 누를 것이 없다(중복 실행 불가).
            if (!c.IsAvailable(session.Stage) || session.Stage < c.localIndexedFrom
                || progression.JobsFor(c).Any(j => progression.StateOf(j) == JobState.Running))
                return plain;

            // 다른 의뢰의 공유 작업(showOnAllCases)이 시작 가능하면 이 의뢰 화면에도 같은 시작 버튼을 보여준다.
            var shared = progression.SharedReadyJob(c);
            if (shared != null)
            {
                statusAction = () => StartJob(shared);
                return new TopBarView.Status { text = text.Get(shared.actionKey), style = TopBarView.StatusStyle.Primary, progress = 1f };
            }

            var next = progression.NextJob(c);
            if (next != null)
            {
                var request = progression.UnviewedRequest(next);
                if (request != null)
                {
                    statusAction = () => OpenRequestFromTop(request);
                    return Link(text.Get(UIKeys.TopRequestNeeded), 0f);
                }
                if (progression.ConditionsMet(next))
                {
                    var job = next;
                    statusAction = () => StartJob(job);
                    // 검토를 이번 실행 중에 마쳤으면 팝오버를 완료 상태(`로컬 1차 검토 완료`)로 한 번 보여준다.
                    // 상단 컨트롤은 시작 동작이 되므로, 팝오버는 바깥을 눌러 닫은 뒤 시작한다.
                    var doneBatch = Progression.BatchOf(next);
                    if (doneBatch != null)
                    {
                        popoverBatch = doneBatch;
                        if (reviewIncomplete.Remove(doneBatch.Id) && !comparing && !SummaryOpen)
                            showDonePopover = true;
                    }
                    return new TopBarView.Status
                    {
                        text = text.Get(next.actionKey),
                        style = TopBarView.StatusStyle.Primary,
                        progress = 1f,
                    };
                }
                var batch = Progression.BatchOf(next);
                if (batch != null)
                {
                    var review = progression.Review(batch);
                    popoverBatch = batch;
                    reviewIncomplete.Add(batch.Id);
                    statusAction = ToggleReviewPopover;
                    var s = Link(ReviewTitle(batch, review), review.Total > 0 ? (float)review.Done / review.Total : 0f);
                    s.open = reviewPopover.IsOpen;
                    return s;
                }
                var guide = Guides().FirstOrDefault(g => g.request == next.request && RecordRef.ForAsset(g.target) != null);
                if (guide != null)
                {
                    var target = RecordRef.ForAsset(guide.target);
                    var guideRequest = guide.request;
                    statusAction = () => OpenRequestFromTop(guideRequest);
                    return Link(text.Format(UIKeys.TopGuideState, ("target", guide.TargetLabel),
                        ("state", text.Get(UIKeys.GuideStateKey(store.ClassOf(target.Key))))), 0f);
                }
                return new TopBarView.Status { text = text.Get(next.notReadyKey), style = TopBarView.StatusStyle.Plain };
            }

            var summary = progression.SummaryJob(c);
            if (summary != null)
            {
                statusAction = () => OpenSummary(summary);
                var s = Link(text.Get(summary.doneKey), 1f);
                s.open = SummaryOpen && summaryJob == summary;
                return s;
            }
            return plain;
        }

        static TopBarView.Status Link(string value, float progress) =>
            new TopBarView.Status { text = value, style = TopBarView.StatusStyle.Link, progress = progress };

        string ReviewTitle(ReviewBatchData batch, ReviewProgress review) =>
            text.Format(UIKeys.TopReview, ("title", text.Get(batch.titleKey)),
                ("done", review.Done.ToString(time.Culture)), ("total", review.Total.ToString(time.Culture)));

        void OnStatusClicked()
        {
            if (comparing)
                return;
            statusAction?.Invoke();
        }

        /// <summary>상단 `유족 요청 확인 필요`·초반 업무 상태: 그 요청을 우측 패널에 연다(좌측 의뢰 요청과 같은 경로).</summary>
        void OpenRequestFromTop(WorkRequestData request)
        {
            CloseNotifications();
            CloseReviewPopover();
            OpenRequest(request);
        }

        // ───────────── 검토 진행 팝오버 ─────────────

        void ToggleReviewPopover()
        {
            if (reviewPopover.IsOpen)
            {
                CloseReviewPopover();
                return;
            }
            CloseNotifications();
            reviewPopover.Open(CloseReviewPopover);
            RenderTopBar();
        }

        void CloseReviewPopover()
        {
            if (!reviewPopover.IsOpen)
                return;
            reviewPopover.Close();
            RenderTopBar();
        }

        void RenderReviewPopover()
        {
            var batch = popoverBatch;
            var review = progression.Review(batch);
            var rows = review.groups.Select(g =>
            {
                var pending = g.pending.Count > 0 ? g.pending[0].target : null;
                return new ReviewPopoverView.Row
                {
                    label = text.Get(g.group.labelKey),
                    count = text.Format(UIKeys.ReviewGroupCount,
                        ("done", g.done.ToString(time.Culture)), ("total", g.total.ToString(time.Culture))),
                    done = g.done == g.total,
                    onOpen = pending != null ? () => OpenReviewTarget(pending) : (System.Action)null,
                };
            }).ToList();
            var next = review.pending.FirstOrDefault();
            reviewPopover.Show(
                review.Complete ? text.Get(batch.doneTitleKey) : ReviewTitle(batch, review),
                review.Complete ? text.FormatCount(batch.doneBodyKey, review.Total, ("total", review.Total.ToString(time.Culture))) : null,
                rows, text.Get(UIKeys.ReviewContinue),
                next != null ? () => OpenReviewTarget(next) : (System.Action)null,
                new ReviewPopoverView.Palette
                {
                    rowColor = Theme.panel,
                    text = Theme.text,
                    doneText = Theme.subText,
                    accent = Theme.accent,
                });
        }

        /// <summary>
        /// 분류 행·`검토 계속`: 그 원본을 `원본 열기`와 같은 경로로 연다(열람 기록도 수동으로 열 때와 같다).
        /// 열 수 없게 됐으면 열지 않고 팝오버를 다시 그린다.
        /// </summary>
        void OpenReviewTarget(RecordRef target)
        {
            if (comparing)
                return;
            if (!PlanSource(target, out _, out _, out _))
            {
                RenderReviewPopover();
                return;
            }
            reviewPopover.Close();
            OpenSource(target);
        }

        // ───────────── 연동 결과 요약 ─────────────

        /// <summary>
        /// 작업 완료로 만들어졌지만 아직 확인하지 않은 요약이 있으면 한 번 보여준다.
        /// 비교 중에는 열지 않고(비교를 마치면 다시 확인), 이미 요약을 보고 있으면 바꾸지 않는다.
        /// </summary>
        void ShowPendingSummary()
        {
            if (comparing || SummaryOpen)
                return;
            foreach (var entry in progressStore.Summaries.Where(s => !s.seen).OrderBy(s => s.seq))
            {
                var job = config.database.jobs.FirstOrDefault(j => j != null && j.Id == entry.jobId);
                if (job == null || job.device == null || !job.device.IsAvailable(session.Stage)
                    || progression.StateOf(job) != JobState.Completed)
                    continue;
                OpenSummary(job);
                return;
            }
        }

        /// <summary>
        /// 요약을 중앙에 연다. 검색·작업함·비교처럼 중앙 PC 화면 하나만 보이므로 검색·작업함을 닫는다.
        /// 휴대전화·우측 패널 상태는 그대로 두어 닫으면 이전 화면으로 돌아간다.
        /// </summary>
        void OpenSummary(ProgressJobData job)
        {
            if (comparing || job == null)
                return;
            CloseNotifications();
            CloseReviewPopover();
            if (selecting)
                SetSelecting(false);
            bool hadWorkbench = WorkbenchOpen;
            CloseSearch();
            if (hadWorkbench)
            {
                workbench.Close();
                workbench.KeyboardFocus = false;
                compareWarnKey = null;
            }
            SaveScroll();
            summaryJob = job;
            summaryView.Open();
            RenderSummary();
            if (hadWorkbench)
                RefreshWorkPanel();
            RenderTopBar();
        }

        /// <summary>요약을 닫는다(`나중에 확인`·Esc·좌측 이동·분류 행). 닫으면 확인한 것으로 저장해 다시 자동으로 띄우지 않는다.</summary>
        void CloseSummary()
        {
            if (!SummaryOpen)
                return;
            summaryView.Close();
            var job = summaryJob;
            summaryJob = null;
            foreach (var entry in progressStore.Summaries.Where(s => !s.seen && job != null && s.jobId == job.Id).ToList())
                progressStore.MarkSummarySeen(entry);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            RenderTopBar();
        }

        /// <summary>
        /// 요약 내용: 그 작업이 연 출처의 자료 중 지금 접근 가능한 원본을 앱(자료 종류)별로 센다. 0건인 분류는 넣지 않는다.
        /// 수량·분류는 매번 현재 데이터에서 계산하며 증거 ID·중요도·추천은 없다.
        /// </summary>
        void RenderSummary()
        {
            var job = summaryJob;
            var device = job.device;
            var records = progression.SummaryRecords(job);
            var appOrder = config.apps.apps.Where(a => a != null).ToDictionary(a => a.kind, a => a.order);
            var groups = records
                .GroupBy(r => AppFor(r.kind))
                .OrderBy(g => appOrder.TryGetValue(g.Key, out var o) ? o : int.MaxValue)
                .ToList();
            var rows = groups.Select(g =>
            {
                var app = g.Key;
                var sources = g.Select(r => SourceOf(r)).Distinct().ToList();
                return new SyncSummaryView.Row
                {
                    label = text.Get(UIKeys.SummaryAppKey(app)),
                    count = text.Format(UIKeys.SummaryRowCount, ("count", g.Count().ToString(time.Culture))),
                    onOpen = () => OpenSummaryApp(device, app, sources.Count == 1 ? sources[0] : (RecordSource?)null),
                };
            }).ToList();
            var effect = Progression.SummaryEffect(job);
            var tokenCase = effect != null && effect.caseToken != null ? effect.caseToken : device;
            System.Action openFirst = null;
            if (rows.Count > 0)
                openFirst = rows[0].onOpen;
            summaryView.Show(text.Get(KeyOr(effect?.summaryHeadingKey, UIKeys.SummaryHeading)),
                text.Format(effect != null && !string.IsNullOrEmpty(effect.templateKey) ? effect.templateKey : job.doneKey,
                    ("caseDisplayName", tokenCase.DisplayName)),
                text.FormatCount(KeyOr(effect?.summaryCountKey, UIKeys.SummaryCount), records.Count, ("count", records.Count.ToString(time.Culture))),
                text.Get(UIKeys.SummaryEmpty), rows,
                text.Get(KeyOr(effect?.summaryOpenKey, UIKeys.SummaryOpen)), openFirst,
                text.Get(UIKeys.SummaryLater), CloseSummary);
        }

        /// <summary>요약 효과에 지정한 문구 키, 비어 있으면 기본 연동 결과 요약 키.</summary>
        static string KeyOr(string key, string fallback) => string.IsNullOrEmpty(key) ? fallback : key;

        RecordSource SourceOf(RecordRef r)
        {
            RecordAccess.Check(r, config.database, session.Stage, text, out var res);
            return res.source;
        }

        static AppKind AppFor(RecordKind kind)
        {
            switch (kind)
            {
                case RecordKind.Photo: return AppKind.Photos;
                case RecordKind.Browser: return AppKind.Browser;
                case RecordKind.Map: return AppKind.Maps;
                case RecordKind.File: return AppKind.Files;
                case RecordKind.Setting: return AppKind.Settings;
                case RecordKind.Mail: return AppKind.Mail;
                default: return AppKind.Messages;
            }
        }

        /// <summary>
        /// 요약 분류 행: 요약을 닫고 그 의뢰의 기존 앱 목록(최상위)을 연다. 파일은 새 자료가 한 출처에만 있으면 그 위치 목록으로 연다.
        /// 특정 자료 하나를 열거나 강조하지 않는다.
        /// </summary>
        void OpenSummaryApp(CaseData device, AppKind app, RecordSource? source)
        {
            if (device == null || !device.IsAvailable(session.Stage))
                return;
            CloseSummary();
            SaveScroll();
            EndSelectingForNavigation();
            session.CurrentCase = device;
            var nav = session.Nav(device);
            nav.app = app;
            if (app == AppKind.Messages)
                nav.openThreadId = null;
            else if (app == AppKind.Photos)
            {
                nav.photoAlbumId = null;
                nav.openPhotoId = null;
            }
            else if (IsRecordApp(app))
            {
                nav.SetOpenRecord(app, null);
                if (app == AppKind.Files)
                {
                    nav.filesLocation = source;
                    nav.filesPath = string.Empty;
                }
            }
            Refresh();
        }
    }
}
