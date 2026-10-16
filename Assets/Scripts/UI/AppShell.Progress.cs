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
    /// 작업은 플레이어가 상단 업무 상태 컨트롤에서 직접 시작하며, 진행·알림은 다음에 볼 앱이나 결론을 알려주지 않는다.
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
            // 세이브 복원 뒤: 진행 중 작업의 완료 조건과 아직 시작하지 않은 작업의 준비 효과를 다시 평가한다(이미 충족된 채 종료했을 수 있다).
            progression.Evaluate();
            session.Stage = progression.Stage;
            // 열람 상태가 바뀌는 모든 경로(대화방·사진·상세·원본 열기·검색 결과 열기)는 WorkStore.MarkViewed 하나를 거치므로,
            // 호출부마다 판정을 두지 않고 업무 상태 변경 뒤 한 곳에서 완료 조건·준비 효과를 평가한다.
            store.Changed += OnWorkChanged;
            topBar.Bind(OnSearchChanged, OnSearchSelected, ToggleNotifications, OnStatusClicked);
        }

        /// <summary>자동 완료·준비 알림으로 진행이 바뀌어 다음 프레임에 화면을 다시 그려야 하는지.</summary>
        bool progressRefreshPending;
        /// <summary>열람 수만 바뀐 경우 상단 진행 표시만 다시 그린다.</summary>
        bool topRefreshPending;

        /// <summary>
        /// 업무 상태(열람·고정·분류·메모)가 바뀐 뒤. 완료·준비 판정과 저장은 바로 하고(데이터만 바꿈),
        /// 화면 갱신은 지금 진행 중인 화면 전환(원본 열기 등)을 끊지 않도록 프레임 끝으로 미룬다.
        /// </summary>
        void OnWorkChanged()
        {
            if (progression != null && progression.Evaluate())
                progressRefreshPending = true;
            else
                topRefreshPending = true;
        }

        void LateUpdate()
        {
            // 비교 중에는 화면을 바꾸지 않고, 비교를 마친 뒤 갱신한다.
            if (progressRefreshPending && !comparing)
                AfterProgressChange();
            else if (topRefreshPending && !comparing)
            {
                topRefreshPending = false;
                RenderTopBar();
            }
        }

        /// <summary>진행이 바뀐 뒤: 접근 단계를 맞추고 전체 화면·알림을 다시 그린다.</summary>
        void AfterProgressChange()
        {
            // 단계가 바뀌면 그 단계에서 열 수 있게 된 원본으로 완료·시작 조건이 충족될 수 있다(같은 판정 경로, 이미 완료한 작업·실행한 효과는 건너뜀).
            progression.Evaluate();
            progressRefreshPending = false;
            topRefreshPending = false;
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
            // 작업 완료로 처음 만들어진 연동 결과 요약은 한 번 자동으로 보인다(비교 중이면 비교를 마친 뒤).
            if (!ShowPendingFinale())
                ShowPendingSummary();
        }

        void StartJob(ProgressJobData job)
        {
            if (comparing || presenting || !progression.Start(job))
                return;
            // 진행 데이터(시작·자동 완료·단계·알림·요약 기록)는 Start가 이미 한 트랜잭션으로 저장했다. 연출은 화면 표시일 뿐이다.
            if (!string.IsNullOrEmpty(job.startPresentationKey))
                BeginStartPresentation(job);
            AfterProgressChange();
        }

        // ───────────── 작업 시작 연출 ─────────────

        /// <summary>작업 시작 직후 처리 연출 중인지. 저장하지 않으므로 재실행하면 연출은 반복되지 않는다.</summary>
        bool presenting;
        float presentingUntil;
        int presentingFrame;
        bool presentingSkipped;
        CanvasGroup topBarGroup;

        /// <summary>
        /// 작업 데이터에 연출 문구 키가 있으면 중앙에 처리 화면을 잠깐 보여주고 입력을 막는다(클릭·Enter로 건너뛰기, Esc는 무시).
        /// 그동안 결과 요약 자동 표시는 보류되며, 연출이 끝나면 확인하지 않은 요약을 연다.
        /// 휴대전화 탐색·작업함·검색 상태와 업무 기록은 바꾸지 않는다(중앙을 덮기만 함).
        /// </summary>
        void BeginStartPresentation(ProgressJobData job)
        {
            CloseNotifications();
            CloseReviewPopover();
            presenting = true;
            presentingUntil = Time.unscaledTime + Mathf.Max(0f, job.startPresentationSeconds);
            presentingFrame = Time.frameCount;
            presentingSkipped = false;
            // 시작 버튼에 남은 선택을 지워 연출 중·직후의 Enter가 그 버튼으로 제출되지 않게 한다.
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            var device = job.device;
            summaryView.Open();
            summaryView.ShowProcessing(text.Get(job.startPresentationKey),
                device != null ? text.Format(UIKeys.TopCaseStatus, ("case", device.DisplayName), ("status", text.Get(job.runningKey))) : null);
            SetPresentationInputBlocked(true);
        }

        void EndStartPresentation()
        {
            if (!presenting)
                return;
            presenting = false;
            SetPresentationInputBlocked(false);
            // 연출 화면만 닫는다(요약 확인 처리 없음). 이어서 보류된 결과 요약이 있으면 연다.
            summaryView.Close();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            ShowPendingSummary();
            RenderTopBar();
        }

        void SetPresentationInputBlocked(bool blocked)
        {
            SetSideInteractable(!blocked);
            if (topBarGroup == null && !topBar.TryGetComponent(out topBarGroup))
                topBarGroup = topBar.gameObject.AddComponent<CanvasGroup>();
            topBarGroup.interactable = !blocked;
            topBarGroup.blocksRaycasts = !blocked;
        }

        /// <summary>
        /// 연출 중 입력: 클릭·Enter로 건너뛰고, 시간이 지나면 끝낸다. 다른 키(Esc 포함)는 아무 화면도 바꾸지 않는다.
        /// 건너뛰기·시간 만료 뒤에도 Enter·왼쪽 버튼을 누르고 있는 동안은 닫지 않는다. 닫히는 순간 같은 프레임에 보류된 결과 요약이
        /// 열리므로, 누른 입력이 남아 있으면 UI 입력 모듈이 그 입력을 요약의 버튼(누름·제출)으로 받을 수 있기 때문이다.
        /// 누른 순간에는 아직 연출 화면(버튼 없음)과 막힌 상단·좌우 패널만 있어 아무 버튼도 눌리지 않는다.
        /// </summary>
        void UpdateStartPresentation()
        {
            // 시작을 누른 Enter·클릭이 같은 프레임에 건너뛰기로 읽히지 않게 한다.
            if (Time.frameCount == presentingFrame)
                return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool skip = (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                        || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (skip)
                presentingSkipped = true;
            if (!presentingSkipped && Time.unscaledTime < presentingUntil)
                return;
            bool held = (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed))
                        || (mouse != null && mouse.leftButton.isPressed);
            if (!held)
                EndStartPresentation();
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

        /// <summary>
        /// 검색 결과 화면을 연다. 휴대전화·우측 패널 상태는 그대로 두고 중앙만 덮는다.
        /// 검색·작업함·비교는 중앙에 하나만 보이므로 작업함이 열려 있으면 닫는다(쌓아 두지 않아 검색을 닫으면 휴대전화로 돌아간다).
        /// </summary>
        void OpenSearch()
        {
            CloseNotifications();
            CloseReviewPopover();
            CloseSummary();
            if (selecting)
                SetSelecting(false);
            bool hadWorkbench = WorkbenchOpen;
            if (hadWorkbench)
            {
                workbench.Close();
                workbench.KeyboardFocus = false;
                compareWarnKey = null;
            }
            searchView.Open(CloseSearch);
            RenderSearch(true);
            if (hadWorkbench)
                RefreshWorkPanel();
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
            var hits = GlobalSearch.Run(config.database, session.Stage, q, text, time, progression.RequestVisible);
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
                text.FormatCount(UIKeys.SearchCount, hits.Count, ("count", hits.Count.ToString(time.Culture))),
                text.Get(UIKeys.GlobalSearchEmpty), groups, resetScroll);
        }

        SearchView.Row SearchRow(SearchHit h)
        {
            string source = string.IsNullOrEmpty(h.serviceKey)
                ? text.Get(UIKeys.SourceKey(h.source))
                : text.Format(UIKeys.PanelSourceFormat, ("source", text.Get(UIKeys.SourceKey(h.source))), ("service", text.Get(h.serviceKey)));
            string meta = h.hasTime
                ? text.Format(UIKeys.SearchMeta, ("app", SearchAppName(h.app)), ("source", source),
                    ("date", time.FullDateTime(h.time)))
                : text.Format(UIKeys.SearchMetaNoTime, ("app", SearchAppName(h.app)), ("source", source));
            if (store.IsViewed(h.target.Key))
                meta = "✓  " + meta;
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
                case AppKind.Mail: return RecordKind.Mail;
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
                CloseReviewPopover();
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
