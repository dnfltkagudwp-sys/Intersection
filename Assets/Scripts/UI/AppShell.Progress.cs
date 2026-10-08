using System.Collections.Generic;
using System.IO;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>
    /// 진행 연동 (UI-07). 접근 단계·의뢰별 작업 상태·업무 알림은 ProgressStore(별도 저장 파일)에 두고,
    /// 시작 조건·효과·문구는 진행 작업 데이터(ProgressJobData)에서 읽는다. 코드는 특정 의뢰를 알지 못한다.
    /// 작업은 플레이어가 의뢰 요청 패널에서 직접 시작하며, 진행·알림은 다음에 볼 앱이나 결론을 알려주지 않는다.
    /// </summary>
    public partial class AppShell
    {
        [Header("진행·검색·알림")]
        [SerializeField] SearchView searchView;
        [SerializeField] NotificationPanelView notificationPanel;

        ProgressStore progressStore;
        Progression progression;

        /// <summary>검수·테스트용.</summary>
        public Progression Progress => progression;

        void InitProgress()
        {
            progressStore = new ProgressStore(Path.Combine(Application.persistentDataPath, config.progressSaveFileName),
                config.EffectiveStartStage);
            progression = new Progression(config.database, store, progressStore, text);
            session.Stage = progression.Stage;
            topBar.Bind(OnSearchChanged, OnSearchSelected, ToggleNotifications);
        }

        /// <summary>진행이 바뀐 뒤: 접근 단계를 맞추고 전체 화면·알림을 다시 그린다.</summary>
        void AfterProgressChange()
        {
            session.Stage = progression.Stage;
            if (session.CurrentCase == null || !session.CurrentCase.IsAvailable(session.Stage))
                session.CurrentCase = Cases().FirstOrDefault(c => c.IsAvailable(session.Stage));
            // 진행은 휴대전화 화면을 이동한 것이 아니므로, 패널에 띄운 의뢰 요청·작업 기록은 그대로 둔다.
            var keepFocus = focus;
            var keepRecord = focusRecord;
            Refresh();
            if ((keepFocus == PanelFocus.Request || keepFocus == PanelFocus.WorkRecord) && keepRecord != null)
            {
                focus = keepFocus;
                focusRecord = keepRecord;
                RefreshWorkPanel();
            }
            if (searchView.IsOpen)
                RenderSearch(false);
            RenderNotifications();
        }

        void StartJob(ProgressJobData job)
        {
            if (comparing || !progression.Start(job))
                return;
            AfterProgressChange();
        }

        // ───────────── 의뢰 요청 패널의 진행 작업 ─────────────

        /// <summary>
        /// 의뢰 요청을 띄웠을 때 그 요청에 속한 작업(인벤토리 생성 등). 조건 전에는 업무 상태만 보여주고 다음 행동을 지시하지 않는다.
        /// 시작 뒤에는 같은 버튼을 다시 누를 수 없다.
        /// </summary>
        void RenderJobs()
        {
            var items = new List<WorkPanelView.JobItem>();
            if (focus == PanelFocus.Request && focusRecord != null)
            {
                var request = config.database.requests.FirstOrDefault(q => q != null && q.Id == focusRecord.recordId);
                foreach (var job in request == null ? Enumerable.Empty<ProgressJobData>() : progression.JobsForRequest(request))
                {
                    if (job.device == null || !job.device.IsAvailable(session.Stage) || !progression.Visible(job))
                        continue;
                    var state = progression.StateOf(job);
                    bool ready = state == JobState.NotStarted && progression.ConditionsMet(job);
                    var target = job;
                    items.Add(new WorkPanelView.JobItem
                    {
                        buttonText = text.Get(job.actionKey),
                        statusText = state == JobState.Running ? text.Get(job.runningKey)
                            : state == JobState.Completed ? text.Get(job.doneKey)
                            : ready ? null : text.Get(job.notReadyKey),
                        onStart = ready && !comparing ? () => StartJob(target) : (System.Action)null,
                    });
                }
            }
            workPanel.SetJobs(items, new WorkPanelView.JobStyle
            {
                readyColor = Theme.accentSoft,
                readyTextColor = Theme.accent,
                idleColor = Theme.panelRaised,
                idleTextColor = Theme.subText,
            });
        }

        // ───────────── 상단 상태 ─────────────

        void RenderTopBar()
        {
            var c = session.CurrentCase;
            if (c == null)
            {
                topBar.Show(string.Empty, string.Empty, false, 0f);
                return;
            }
            var status = progression.StatusOf(c);
            topBar.Show(
                text.Format(UIKeys.TopCaseStatus, ("case", c.DisplayName), ("status", text.Get(status.sourcesKey))),
                text.Get(status.statusKey),
                status.progress == StatusProgress.Indeterminate,
                status.progress == StatusProgress.Done ? 1f : 0f);
        }

        // ───────────── 전역 검색 ─────────────

        string searchQuery = string.Empty;

        void OnSearchSelected()
        {
            if (comparing)
                return;
            if (!searchView.IsOpen)
                OpenSearch();
        }

        void OnSearchChanged(string value)
        {
            if (comparing)
                return;
            if (!searchView.IsOpen)
                OpenSearch();
            searchQuery = value ?? string.Empty;
            RenderSearch(true);
        }

        /// <summary>검색 결과 화면을 연다. 휴대전화·우측 패널 상태는 그대로 두고 중앙만 덮는다.</summary>
        void OpenSearch()
        {
            CloseNotifications();
            if (selecting)
                SetSelecting(false);
            searchView.Open(CloseSearch);
            RenderSearch(true);
        }

        /// <summary>검색을 닫으면 검색 전의 기기·앱·상세·스크롤·우측 패널 그대로다.</summary>
        void CloseSearch()
        {
            if (!searchView.IsOpen)
                return;
            searchView.Close();
            searchQuery = string.Empty;
            topBar.SetSearchText(string.Empty);
            topBar.ReleaseSearchFocus();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }

        void RenderSearch(bool resetScroll)
        {
            string q = searchQuery.Trim();
            if (q.Length == 0)
            {
                searchView.Show(text.Get(UIKeys.SearchTitle), null, text.Get(UIKeys.SearchHint), new List<SearchView.Group>(), resetScroll);
                return;
            }
            var hits = GlobalSearch.Run(config.database, session.Stage, q, text, time);
            var appOrder = config.apps.apps.Where(a => a != null).ToDictionary(a => a.kind, a => a.order);
            var groups = hits
                .GroupBy(h => (device: h.device, app: h.app))
                .OrderBy(g => g.Key.device.order)
                .ThenBy(g => g.Key.app.HasValue ? (appOrder.TryGetValue(g.Key.app.Value, out var o) ? o : int.MaxValue) : -1)
                .Select(g => new SearchView.Group
                {
                    header = text.Format(UIKeys.SearchGroup, ("case", g.Key.device.DisplayName), ("app", SearchAppName(g.Key.app))),
                    rows = g.OrderByDescending(h => h.hasTime ? h.time.TotalMinutes : int.MinValue)
                        .ThenByDescending(h => h.sortKey)
                        .Select(SearchRow)
                        .ToList(),
                })
                .ToList();
            searchView.Show(text.Get(UIKeys.SearchTitle),
                text.Format(UIKeys.SearchCount, ("count", hits.Count.ToString(time.Culture))),
                text.Get(UIKeys.GlobalSearchEmpty), groups, resetScroll);
        }

        SearchView.Row SearchRow(SearchHit h)
        {
            string source = string.IsNullOrEmpty(h.serviceKey)
                ? text.Get(UIKeys.SourceKey(h.source))
                : text.Format(UIKeys.PanelSourceFormat, ("source", text.Get(UIKeys.SourceKey(h.source))), ("service", text.Get(h.serviceKey)));
            string meta = h.hasTime
                ? text.Format(UIKeys.SearchMeta, ("app", SearchAppName(h.app)), ("source", source),
                    ("date", time.ToDateTime(h.time).ToString(text.Get(UIKeys.FormatFullDate), time.Culture) + " " + time.Time(h.time)),
                    ("since", time.SinceDeath(h.time)))
                : text.Format(UIKeys.SearchMetaNoTime, ("app", SearchAppName(h.app)), ("source", source));
            var hit = h;
            return new SearchView.Row
            {
                title = h.title,
                snippet = Snippet(h.matchText, h.matchIndex, h.matchLength),
                meta = meta,
                onOpen = () => OpenSearchHit(hit),
            };
        }

        string SearchAppName(AppKind? app) => app.HasValue ? AppName(KindFor(app.Value)) : text.Get(UIKeys.CompareAppWork);

        static RecordKind KindFor(AppKind app)
        {
            switch (app)
            {
                case AppKind.Photos: return RecordKind.Photo;
                case AppKind.Browser: return RecordKind.Browser;
                case AppKind.Maps: return RecordKind.Map;
                case AppKind.Files: return RecordKind.File;
                case AppKind.Settings: return RecordKind.Setting;
                default: return RecordKind.Thread;
            }
        }

        /// <summary>일치한 문구 주변만 잘라 일치 부분을 강조한다. 원문은 리치 텍스트로 해석되지 않게 noparse로 감싼다.</summary>
        string Snippet(string source, int index, int length)
        {
            if (string.IsNullOrEmpty(source) || index < 0)
                return null;
            source = source.Replace("\r", string.Empty).Replace('\n', ' ');
            const int context = 28;
            int start = Mathf.Max(0, index - context);
            int end = Mathf.Min(source.Length, index + length + context);
            string before = (start > 0 ? "…" : string.Empty) + source.Substring(start, index - start);
            string match = source.Substring(index, Mathf.Min(length, source.Length - index));
            string after = source.Substring(index + match.Length, end - index - match.Length) + (end < source.Length ? "…" : string.Empty);
            string accent = ColorUtility.ToHtmlStringRGB(Theme.accent);
            return $"<noparse>{before}</noparse><color=#{accent}><b><noparse>{match}</noparse></b></color><noparse>{after}</noparse>";
        }

        /// <summary>
        /// 결과를 연다. 기존 `원본 열기`와 같은 불변 RecordRef 해석 경로를 쓰고, 지금 접근할 수 없게 됐으면 열지 않고 결과를 다시 계산한다.
        /// 여는 것은 새 자료를 만들거나 접근 단계를 올리지 않는다.
        /// </summary>
        void OpenSearchHit(SearchHit hit)
        {
            if (comparing)
                return;
            if (RecordAccess.Check(hit.target, config.database, session.Stage, text) != AccessState.Accessible)
            {
                RenderSearch(false);
                return;
            }
            if (hit.target.kind == RecordKind.Request)
            {
                var request = config.database.requests.FirstOrDefault(q => q != null && q.Id == hit.target.recordId && q.device == hit.device);
                if (request == null)
                    return;
                CloseSearch();
                if (session.CurrentCase != hit.device)
                {
                    SaveScroll();
                    EndSelectingForNavigation();
                    session.CurrentCase = hit.device;
                    Refresh();
                }
                OpenRequest(request);
                return;
            }
            if (!PlanSource(hit.target, out _, out _, out _))
            {
                RenderSearch(false);
                return;
            }
            CloseSearch();
            OpenSource(hit.target);
        }

        // ───────────── 업무 알림 ─────────────

        void ToggleNotifications()
        {
            if (notificationPanel.IsOpen)
                CloseNotifications();
            else
            {
                notificationPanel.Open(CloseNotifications);
                RenderNotifications();
            }
        }

        void CloseNotifications()
        {
            if (!notificationPanel.IsOpen)
                return;
            notificationPanel.Close();
            RenderNotifications();
        }

        /// <summary>읽지 않은 수 배지와 (열려 있으면) 목록. 알림을 눌러도 앱·기록을 열지 않고 읽음으로만 바꾼다.</summary>
        void RenderNotifications()
        {
            int unread = progressStore.UnreadCount;
            topBar.ShowUnread(unread, unread.ToString(time.Culture), notificationPanel.IsOpen, Theme.accentSoft, Theme.panelRaised);
            if (!notificationPanel.IsOpen)
                return;
            var items = progressStore.Notifications
                .OrderByDescending(n => n.seq)
                .Select(n =>
                {
                    var entry = n;
                    return new NotificationPanelView.Item
                    {
                        text = NotificationText(n),
                        read = n.read,
                        onClick = () =>
                        {
                            progressStore.MarkRead(entry);
                            RenderNotifications();
                        },
                    };
                })
                .ToList();
            notificationPanel.Show(text.Get(UIKeys.NotificationsTitle), text.Get(UIKeys.NotificationsMarkAll),
                text.Get(UIKeys.NotificationsEmpty), items,
                () =>
                {
                    progressStore.MarkAllRead();
                    RenderNotifications();
                },
                Theme.text, Theme.subText);
        }

        /// <summary>저장한 템플릿 키와 토큰으로 지금 문구를 만든다. 의뢰 표시명은 현재 데이터에서 읽는다.</summary>
        string NotificationText(NotificationEntry n)
        {
            var device = config.database.cases.FirstOrDefault(c => c != null && c.Id == n.caseId);
            return text.Format(n.templateKey,
                ("caseDisplayName", device != null ? device.DisplayName : string.Empty),
                ("count", n.count.ToString(time.Culture)));
        }

        // ───────────── 에디터 검수 ─────────────

        /// <summary>검수용: 효과·알림 없이 접근 단계를 바꾼다.</summary>
        public void DebugSetStage(ProgressStage stage)
        {
            progression.DebugSetStage(stage);
            AfterProgressChange();
        }

        /// <summary>검수용: 효과·알림 없이 작업 상태를 바꾼다.</summary>
        public void DebugSetJob(ProgressJobData job, JobState state)
        {
            progression.DebugSetJob(job, state);
            AfterProgressChange();
        }

        /// <summary>검수용: 이후 정식 콘텐츠가 할 작업 완료를 대신한다 (완료 효과·알림 실행).</summary>
        public void DebugCompleteJob(ProgressJobData job)
        {
            progression.Complete(job);
            AfterProgressChange();
        }

        /// <summary>검수용: 진행 상태를 처음(시작 단계)으로 되돌린다. 업무 상태(WorkStore)는 그대로다.</summary>
        public void DebugResetProgress()
        {
            progressStore.ResetAll(config.EffectiveStartStage);
            AfterProgressChange();
        }
    }
}
