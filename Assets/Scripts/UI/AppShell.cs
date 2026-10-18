using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 메인 셸. 원본 데이터(ContentDatabase) → 표시 상태(SessionState) → 각 뷰로 흘려보낸다.
    /// 화면 문구는 모두 StringTable, 콘텐츠는 모두 데이터 에셋에서 온다.
    /// 사진 앱 부분은 AppShell.Photos.cs에 있다.
    /// </summary>
    public partial class AppShell : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] TopBarView topBar;
        [SerializeField] RectTransform caseListRoot;
        [SerializeField] RectTransform appListRoot;
        [SerializeField] SidebarItemView caseItemPrefab;
        [SerializeField] SidebarItemView appItemPrefab;
        [SerializeField] TMP_Text deviceLabel;
        [SerializeField] PhoneView phone;
        [SerializeField] WorkPanelView workPanel;

        UIText text;
        PhoneTime time;
        SessionState session;
        readonly List<SidebarItemView> sidebarItems = new List<SidebarItemView>();

        UITheme Theme => config.theme;

        void Awake()
        {
            // 플레이어가 타이틀에서 고른 언어(진행 저장과 별개로 보관)를 먼저 적용한다. 없으면 GameConfig 기본 언어.
            config.UseLanguage(SavedLanguage());
            // 콘텐츠 원문 언어면 번역 테이블 없이 에셋 원문 그대로 보인다.
            ContentText.Use(config.ActiveContentTable);
            config.ApplyPathTokens();
            text = new UIText(config.ActiveStrings);
            time = new PhoneTime(config, text);
            session = new SessionState(config.EffectiveStartStage);
            foreach (var label in GetComponentsInChildren<LocalizedText>(true))
                label.Apply(text);
            phone.MessageList.QueryChanged += OnQueryChanged;
            ApplyUiPostProcessing();
            InitWork();
            // 접근 단계는 진행 저장 파일에서 읽는다(없으면 시작 단계).
            InitProgress();
            session.CurrentCase = Cases().FirstOrDefault(c => c.IsAvailable(session.Stage));
        }

        /// <summary>
        /// UI를 그리는 카메라를 씬의 전역 후처리(톤 매핑·블룸·비네트)에서 분리한다(테마 설정).
        /// 씬 파일은 바꾸지 않고 실행 중 카메라 설정만 맞춘다.
        /// </summary>
        void ApplyUiPostProcessing()
        {
            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null ? canvas.rootCanvas.worldCamera : null;
            if (cam != null && cam.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var data))
                data.renderPostProcessing = Theme.uiPostProcessing;
        }

        // 도메인 리로드를 끈 에디터에서는 정적 값이 플레이 뒤에도 남는다. 에디터 도구(검증·캡처 렌더러)가 원문을 쓰도록 되돌린다.
        void OnDestroy()
        {
            ContentText.Use(null);
            config.UseLanguage(null);
        }

        void Start()
        {
            phone.Init(ClockFactory.Create(config), text, time.Culture);
            Refresh();
            RenderNotifications();
            // 타이틀(또는 새로 시작한 직후의 첫 요청 장면)부터 보여준다. 확인 전에 종료했던 연락·연동 결과 요약은 시작 화면을 닫은 뒤 다시 보여준다.
            BeginEntry();
        }

        void Update()
        {
            // 타이틀·유족 요청 장면은 업무 화면 전체를 덮는다. 그동안 업무 단축키·Esc·휴대전화 조작은 받지 않는다.
            if (UpdateEntry())
                return;
            if (UpdateFinalePresentation())
                return;
            // 작업 시작 연출 중에는 건너뛰기(클릭·Enter)만 받는다. Esc·단축키·휴대전화 조작으로 이전 화면이 바뀌지 않게 막는다.
            if (presenting)
            {
                UpdateStartPresentation();
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            // 비교 화면에서는 Esc(비교 종료)만 받는다. 휴대전화·업무 단축키와 키보드 이동은 막는다.
            if (comparing)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    HandleEscape();
                memoWasFocused = false;
                return;
            }
            bool typing = IsTyping();
            // 펼친 의뢰 드롭다운은 방향키·Enter·Esc를 스스로 처리한다(Esc는 목록만 닫고 아래 단계로 넘기지 않는다).
            bool dropdownOpen = workbench.CaseFilterExpanded;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                workPanel.KeyboardFocus = false;
                workbench.KeyboardFocus = false;
            }
            // 검색·작업함·연동 결과 요약이 휴대전화를 덮고 있으면 사진 넘기기·휴대전화 자동 탐색·기록 고르기를 하지 않는다.
            bool phoneCovered = searchView.IsOpen || WorkbenchOpen || SummaryOpen;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (!dropdownOpen)
                    HandleEscape();
            }
            else if (!typing && !dropdownOpen && !phoneCovered && keyboard.sKey.wasPressedThisFrame)
                SetSelecting(!selecting);
            if (!dropdownOpen)
                HandleWorkKeys(keyboard);

            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            // 패널에 키보드 포커스가 있으면 방향키는 패널 탐색에만 쓴다(사진 넘기기·휴대전화 자동 탐색 없음).
            bool panelFocused = workPanel.Owns(selected);
            bool benchFocused = WorkbenchOpen && workbench.Owns(selected);
            if (!typing && !panelFocused && !phoneCovered && phone.Current == PhoneView.Screen.PhotoDetail)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                    StepPhoto(-1);
                else if (keyboard.rightArrowKey.wasPressedThisFrame)
                    StepPhoto(1);
            }
            bool tab = keyboard.tabKey.wasPressedThisFrame;
            bool arrows = keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
            bool submit = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (tab && !typing && !dropdownOpen && panelFocused)
            {
                // 패널 안 Tab 순서는 WorkPanelView.Lines()의 명시적 순서를 쓴다.
                workPanel.KeyboardFocus = true;
                workPanel.FocusNext(shift);
            }
            else if (tab && !typing && !dropdownOpen && WorkbenchOpen)
            {
                // 작업함 Tab 순서: 닫기 → 비교 칸·비교 시작 → 상태 필터 → 의뢰 필터 → 자료 행. 처음 Tab은 켜진 필터로.
                workbench.KeyboardFocus = true;
                workbench.FocusNext(shift);
            }
            else if (benchFocused)
            {
                if (arrows || submit)
                    workbench.KeyboardFocus = true;
            }
            else if (!panelFocused && !phoneCovered)
            {
                // 화면을 열 때 특정 행을 미리 선택하지 않는다. 키보드 조작을 시작하면 그때 첫 항목에 포커스를 준다.
                bool navigate = keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame || tab;
                if (navigate && events != null && selected == null)
                {
                    var first = phone.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.IsInteractable());
                    if (first != null)
                        first.Select();
                }
            }
            else if (arrows || submit)
                workPanel.KeyboardFocus = true;
            // 입력창이 같은 프레임에 먼저 Esc를 처리해 포커스가 풀렸어도 메모 Esc로 인식하도록 기억한다.
            memoWasFocused = workPanel.IsEditingMemo;
        }

        IEnumerable<CaseData> Cases() =>
            config.database.cases.Where(c => c != null).OrderBy(c => c.order);

        void Refresh()
        {
            RenderSidebar();
            RenderRequests();
            RenderCenter();
        }

        /// <summary>
        /// 휴대전화 화면과 우측 패널을 그린다. 휴대전화 화면이 바뀌면 패널은 현재 열람으로 돌아간다
        /// (기록 고르기 모드에서 고른 기록만 모드가 끝날 때까지 유지).
        /// </summary>
        void RenderCenter()
        {
            // 작업함이 열려 있는 동안에는 작업함을 열기 전 패널 상태(현재 열람·의뢰 요청 등)를 그대로 두어 닫으면 돌아간다.
            if (!WorkbenchOpen && !(focus == PanelFocus.Picked && selecting))
            {
                focus = PanelFocus.Current;
                focusRecord = null;
            }
            currentDraw = null;
            currentRecord = null;
            // `원본 열기` 직후 한 번만 그 기록을 현재 열람으로 유지한다. 다른 화면으로 이동하면 사라진다.
            sourceFocus = pendingSource;
            pendingSource = null;
            RenderPhone();
            RefreshWorkPanel();
        }

        void RenderSidebar()
        {
            foreach (var item in sidebarItems)
                UIPool.Discard(item.gameObject);
            sidebarItems.Clear();

            foreach (var c in Cases())
            {
                var device = c;
                bool available = c.IsAvailable(session.Stage);
                var item = Instantiate(caseItemPrefab, caseListRoot);
                item.Bind(Initial(c.DisplayName), c.DisplayName,
                    FinaleShortStatus(c) ?? text.Get(progression.StatusOf(c).shortKey),
                    c == session.CurrentCase, available, Theme.accentSoft, Color.clear,
                    () => SelectCase(device));
                sidebarItems.Add(item);
            }

            var current = session.CurrentCase;
            var nav = session.Nav(current);
            var appItems = new List<SidebarItemView>();
            foreach (var app in config.apps.apps.Where(a => a != null).OrderBy(a => a.order))
            {
                var def = app;
                // 개수는 실제로 조회 가능한 데이터가 있는 앱만 계산해 표시한다.
                int? n = app.implemented ? AppItemCount(app.kind, current) : null;
                // 기록이 생기는 단계부터 나타나는 앱(예: 메일)은 지금 이 기기에서 볼 기록이 없으면 숨긴다(지금 열려 있으면 그대로 둔다).
                if (app.hideWhenEmpty && (n ?? 0) == 0 && !(current != null && nav.app == app.kind))
                    continue;
                string count = n.HasValue ? n.Value.ToString(time.Culture) : null;
                var item = Instantiate(appItemPrefab, appListRoot);
                item.Bind(null, text.Get(app.nameKey), count, current != null && nav.app == app.kind,
                    app.implemented && current != null, Theme.accentSoft, Color.clear, () => OpenApp(def.kind));
                sidebarItems.Add(item);
                appItems.Add(item);
            }
            FitAppItems(appItems);
        }

        /// <summary>
        /// 좌측 앱 목록 영역(빌더가 만든 고정 높이) 안에 앱 행이 모두 들어가게 행 높이를 맞춘다.
        /// 들어가면 프리팹 높이 그대로이고, 넘칠 때만 행 높이를 줄인다(아래 의뢰 요청 구역과 겹치지 않음). 씬은 바꾸지 않는다.
        /// </summary>
        void FitAppItems(List<SidebarItemView> items)
        {
            if (items.Count == 0)
                return;
            var layout = appListRoot.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 0f;
            float padding = layout != null ? layout.padding.top + layout.padding.bottom : 0f;
            var prefabElement = appItemPrefab.GetComponent<LayoutElement>();
            float natural = prefabElement != null && prefabElement.preferredHeight > 0 ? prefabElement.preferredHeight : 46f;
            float available = appListRoot.rect.height - padding - spacing * (items.Count - 1);
            float height = Mathf.Min(natural, available / items.Count);
            foreach (var item in items)
            {
                var element = item.GetComponent<LayoutElement>();
                if (element != null)
                    element.preferredHeight = height;
            }
        }

        int? AppItemCount(AppKind kind, CaseData device)
        {
            if (device == null)
                return null;
            switch (kind)
            {
                case AppKind.Messages: return DeviceQuery.Threads(config.database, device, session.Stage).Count;
                case AppKind.Photos: return PhotoQuery.DevicePhotos(config.database, device, session.Stage).Count;
                default: return IsRecordApp(kind) ? RecordAppCount(kind, device) : (int?)null;
            }
        }

        void RenderPhone()
        {
            var device = session.CurrentCase;
            deviceLabel.text = device != null
                ? text.Format(UIKeys.PhoneDeviceLabel, ("owner", device.DisplayName))
                : string.Empty;
            if (device == null)
                return;

            var nav = session.Nav(device);
            // 다른 화면이 열려 있어도 숨겨진 검색창을 이 기기의 검색어로 맞춘다.
            phone.MessageList.SetQuery(nav.searchQuery);
            if (nav.app == AppKind.Photos)
            {
                RenderPhotos(device, nav);
                return;
            }
            if (IsRecordApp(nav.app))
            {
                RenderRecords(device, nav);
                return;
            }

            var entries = DeviceQuery.Threads(config.database, device, session.Stage);
            var open = nav.openThreadId != null ? entries.FirstOrDefault(e => e.thread.Id == nav.openThreadId) : null;
            if (open == null)
            {
                nav.openThreadId = null;
                phone.Show(PhoneView.Screen.MessageList);
                RenderList(device, nav, entries);
                ShowListPanel(device, entries);
            }
            else
            {
                phone.Show(PhoneView.Screen.Chat);
                int unread = DeviceQuery.UnreadExcept(entries, open.thread);
                string back = unread > 0
                    ? text.Format(UIKeys.ChatBackUnread, ("count", unread.ToString(time.Culture)))
                    : text.Get(UIKeys.ChatBack);
                float? scroll = nav.scroll.TryGetValue(open.thread.Id, out var s) ? s : (float?)null;
                string focus = nav.focusMessageId;
                nav.focusMessageId = null;
                phone.Chat.Show(open.thread, device, open.displayName, back, config, text, time, Back, scroll, focus);
                ShowThreadPanel(device, open);
            }
        }

        /// <summary>대화 목록 또는 이 기기의 검색어에 대한 검색 결과.</summary>
        void RenderList(CaseData device, DeviceNavigation nav, List<ThreadEntry> entries)
        {
            var list = phone.MessageList;
            float? listScroll = nav.scroll.TryGetValue(string.Empty, out var ls) ? ls : (float?)null;
            if (string.IsNullOrWhiteSpace(nav.searchQuery))
            {
                list.Show(entries, text, time, Theme, OpenThread, r => store.IsViewed(r.Key), listScroll);
                return;
            }
            var threadResults = new List<SearchResult>();
            var messageResults = new List<SearchResult>();
            MessageSearch.Run(entries, device, nav.searchQuery, text, time, threadResults, messageResults);
            list.ShowResults(threadResults, messageResults, text, time, Theme, OpenThread,
                r => store.IsViewed(r.Key), listScroll);
        }

        void OnQueryChanged(string query)
        {
            var device = session.CurrentCase;
            if (device == null)
                return;
            var nav = session.Nav(device);
            nav.searchQuery = query ?? string.Empty;
            nav.scroll.Remove(string.Empty);
            RenderList(device, nav, DeviceQuery.Threads(config.database, device, session.Stage));
        }

        void ShowListPanel(CaseData device, List<ThreadEntry> entries) => Present(() =>
        {
            workPanel.Show(text.Get(UIKeys.PanelTitleMessageList), new[]
            {
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelSource, text.Get(UIKeys.SourceLocal), Theme.regularFont),
                Row(UIKeys.PanelThreadCount,
                    text.Format(UIKeys.PanelThreadCountValue, ("count", entries.Count.ToString(time.Culture))),
                    Theme.regularFont),
            });
        }, null);

        void ShowThreadPanel(CaseData device, ThreadEntry entry) => Present(() =>
        {
            var ordered = DeviceQuery.Ordered(entry.thread);
            workPanel.Show(text.Format(UIKeys.PanelTitleThread, ("name", entry.displayName)), new[]
            {
                // 증거 마스터 ID(evidenceRef)는 표시하지 않는다. 모든 대화가 같은 형식의 중립 코드를 가진다.
                Row(UIKeys.PanelRecordId, RecordCode.Format(text, UIKeys.RecordPrefixThread, entry.thread.Id, device.Id), Theme.monoFont),
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelSource, SourceText(entry.state.source, entry.state.serviceKey), Theme.regularFont),
                Row(UIKeys.PanelPeriod, time.FullDateRange(ordered[0].time, ordered[ordered.Count - 1].time), Theme.regularFont),
                Row(UIKeys.PanelIntegrity, string.IsNullOrEmpty(entry.state.integrityKey) ? null : text.Get(entry.state.integrityKey), Theme.regularFont),
            });
        }, RecordRef.ForThread(device, entry.thread));

        static string OwnerName(CaseData device) => device.owner != null ? device.owner.FullName : device.DisplayName;

        /// <summary>"로컬 · 문자(SMS)"처럼 출처와 확정된 생성 경로. 경로가 없으면 출처만.</summary>
        string SourceText(RecordSource source, string serviceKey) =>
            string.IsNullOrEmpty(serviceKey)
                ? text.Get(UIKeys.SourceKey(source))
                : text.Format(UIKeys.PanelSourceFormat,
                    ("source", text.Get(UIKeys.SourceKey(source))), ("service", text.Get(serviceKey)));

        WorkPanelView.Row Row(string labelKey, string value, TMP_FontAsset font) =>
            new WorkPanelView.Row { label = text.Get(labelKey), value = value, font = font };

        static string Initial(string name) => string.IsNullOrEmpty(name) ? string.Empty : name.Substring(0, 1);

        void SelectCase(CaseData device)
        {
            if (!device.IsAvailable(session.Stage))
                return;
            // 좌측 의뢰를 누르면 중앙의 검색·작업함을 닫고 휴대전화 화면으로 돌아간다(같은 의뢰면 닫기만).
            bool covered = searchView.IsOpen || WorkbenchOpen || SummaryOpen;
            CloseCenterScreens();
            if (device == session.CurrentCase)
            {
                if (covered)
                    RefreshWorkPanel();
                return;
            }
            SaveScroll();
            EndSelectingForNavigation();
            session.CurrentCase = device;
            Refresh();
        }

        void OpenApp(AppKind kind)
        {
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            EndSelectingForNavigation();
            // 좌측 앱을 누르면 중앙의 검색·작업함을 닫고 그 앱의 최상위 화면을 연다.
            CloseCenterScreens();
            nav.app = kind;
            if (kind == AppKind.Messages)
                nav.openThreadId = null;
            else if (kind == AppKind.Photos)
            {
                nav.photoAlbumId = null;
                nav.openPhotoId = null;
            }
            else if (IsRecordApp(kind))
            {
                nav.SetOpenRecord(kind, null);
                if (kind == AppKind.Files)
                {
                    nav.filesLocation = null;
                    nav.filesPath = string.Empty;
                }
            }
            Refresh();
        }

        /// <summary>대화방을 연다. 검색 결과의 메시지에서 열었으면 그 메시지 위치로 이동한다.</summary>
        void OpenThread(ThreadEntry entry, MessageData message)
        {
            // 고르기 모드에서는 목록 행이 대화방 전체(또는 검색 결과의 개별 메시지)를 고른다.
            if (TrySelectInstead(message == null
                    ? RecordRef.ForThread(entry.device, entry.thread)
                    : RecordRef.ForMessage(entry.device, entry.thread, message)))
                return;
            store.MarkViewed(RecordRef.ForThread(entry.device, entry.thread));
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            nav.openThreadId = entry.thread.Id;
            nav.focusMessageId = message?.Id;
            nav.scroll.Remove(entry.thread.Id);
            RenderCenter();
        }

        /// <summary>
        /// 한 단계씩 되돌아간다.
        /// 메시지: 대화방 → (검색 결과 또는) 목록 → 검색어 지우기 / 사진: 한 장 보기 → 그리드 → 앨범 목록
        /// </summary>
        void Back()
        {
            var device = session.CurrentCase;
            if (device == null)
                return;
            var nav = session.Nav(device);
            if (nav.app == AppKind.Photos)
            {
                BackPhotos(nav);
                return;
            }
            if (IsRecordApp(nav.app))
            {
                BackRecords(nav);
                return;
            }
            if (nav.openThreadId != null)
            {
                SaveScroll();
                nav.openThreadId = null;
                RenderCenter();
            }
            else if (!string.IsNullOrEmpty(nav.searchQuery))
            {
                nav.searchQuery = string.Empty;
                nav.scroll.Remove(string.Empty);
                RenderCenter();
            }
        }

        /// <summary>지금 보이는 화면의 스크롤 위치를 그 화면의 키로 저장한다.</summary>
        void SaveScroll()
        {
            if (session.CurrentCase == null)
                return;
            var nav = session.Nav(session.CurrentCase);
            switch (phone.Current)
            {
                case PhoneView.Screen.MessageList:
                    nav.scroll[string.Empty] = phone.MessageList.ScrollPosition;
                    break;
                case PhoneView.Screen.Chat:
                    if (nav.openThreadId != null)
                        nav.scroll[nav.openThreadId] = phone.Chat.ScrollPosition;
                    break;
                case PhoneView.Screen.AlbumList:
                    nav.scroll[DeviceNavigation.AlbumListScrollKey] = phone.AlbumList.ScrollPosition;
                    break;
                case PhoneView.Screen.PhotoGrid:
                    if (nav.photoAlbumId != null)
                        nav.scroll[nav.photoAlbumId] = phone.PhotoGrid.ScrollPosition;
                    break;
                case PhoneView.Screen.RecordList:
                    nav.scroll[nav.ListScrollKey(nav.app)] = phone.RecordList.ScrollPosition;
                    break;
            }
        }
    }
}
