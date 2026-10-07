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
            text = new UIText(config.strings);
            time = new PhoneTime(config, text);
            session = new SessionState(config.EffectiveStartStage);
            foreach (var label in GetComponentsInChildren<LocalizedText>(true))
                label.Apply(text);
            session.CurrentCase = Cases().FirstOrDefault(c => c.IsAvailable(session.Stage));
            phone.MessageList.QueryChanged += OnQueryChanged;
        }

        void Start()
        {
            phone.Init(ClockFactory.Create(config), text, time.Culture);
            Refresh();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
                Back();
            if (phone.Current == PhoneView.Screen.PhotoDetail)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                    StepPhoto(-1);
                else if (keyboard.rightArrowKey.wasPressedThisFrame)
                    StepPhoto(1);
            }
            // 화면을 열 때 특정 행을 미리 선택하지 않는다. 키보드 조작을 시작하면 그때 첫 항목에 포커스를 준다.
            bool navigate = keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.tabKey.wasPressedThisFrame;
            var events = EventSystem.current;
            if (navigate && events != null && events.currentSelectedGameObject == null)
            {
                var first = phone.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.IsInteractable());
                if (first != null)
                    first.Select();
            }
        }

        IEnumerable<CaseData> Cases() =>
            config.database.cases.Where(c => c != null).OrderBy(c => c.order);

        void Refresh()
        {
            RenderSidebar();
            RenderTopBar();
            RenderCenter();
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
                    text.Get(available ? UIKeys.CaseStatusLocal : UIKeys.CaseStatusWaiting),
                    c == session.CurrentCase, available, Theme.accentSoft, Color.clear,
                    () => SelectCase(device));
                sidebarItems.Add(item);
            }

            var current = session.CurrentCase;
            var nav = session.Nav(current);
            foreach (var app in config.apps.apps.Where(a => a != null).OrderBy(a => a.order))
            {
                var def = app;
                // 개수는 실제로 조회 가능한 데이터가 있는 앱만 계산해 표시한다.
                int? n = app.implemented ? AppItemCount(app.kind, current) : null;
                string count = n.HasValue ? n.Value.ToString(time.Culture) : null;
                var item = Instantiate(appItemPrefab, appListRoot);
                item.Bind(null, text.Get(app.nameKey), count, current != null && nav.app == app.kind,
                    app.implemented && current != null, Theme.accentSoft, Color.clear, () => OpenApp(def.kind));
                sidebarItems.Add(item);
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
                default: return null;
            }
        }

        void RenderTopBar()
        {
            var c = session.CurrentCase;
            if (c == null)
            {
                topBar.Show(string.Empty, string.Empty, 0f);
                return;
            }
            bool indexed = session.Stage >= c.localIndexedFrom;
            topBar.Show(
                text.Format(UIKeys.TopCaseStatus, ("case", c.DisplayName), ("status", text.Get(UIKeys.CaseStatusLocal))),
                text.Get(indexed ? UIKeys.TopIndexDone : UIKeys.TopIndexRunning),
                indexed ? 1f : 0f);
        }

        void RenderCenter()
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
                list.Show(entries, text, time, Theme, OpenThread, listScroll);
                return;
            }
            var threadResults = new List<SearchResult>();
            var messageResults = new List<SearchResult>();
            MessageSearch.Run(entries, device, nav.searchQuery, text, time, threadResults, messageResults);
            list.ShowResults(threadResults, messageResults, text, time, Theme, OpenThread, listScroll);
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

        void ShowListPanel(CaseData device, List<ThreadEntry> entries)
        {
            workPanel.Show(text.Get(UIKeys.PanelTitleMessageList), new[]
            {
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelSource, text.Get(UIKeys.SourceLocal), Theme.regularFont),
                Row(UIKeys.PanelThreadCount,
                    text.Format(UIKeys.PanelThreadCountValue, ("count", entries.Count.ToString(time.Culture))),
                    Theme.regularFont),
            });
        }

        void ShowThreadPanel(CaseData device, ThreadEntry entry)
        {
            var ordered = DeviceQuery.Ordered(entry.thread);
            workPanel.Show(text.Format(UIKeys.PanelTitleThread, ("name", entry.displayName)), new[]
            {
                // 증거 마스터 ID(evidenceRef)는 표시하지 않는다. 모든 대화가 같은 형식의 중립 코드를 가진다.
                Row(UIKeys.PanelRecordId, RecordCode.Format(text, UIKeys.RecordPrefixThread, entry.thread.Id, device.Id), Theme.monoFont),
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelSource, SourceText(entry.state.source, entry.state.serviceKey), Theme.regularFont),
                Row(UIKeys.PanelPeriod, time.SinceDeathRange(ordered[0].time, ordered[ordered.Count - 1].time), Theme.regularFont),
                Row(UIKeys.PanelIntegrity, string.IsNullOrEmpty(entry.state.integrityKey) ? null : text.Get(entry.state.integrityKey), Theme.regularFont),
            });
        }

        static string OwnerName(CaseData device) => device.owner != null ? device.owner.fullName : device.DisplayName;

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
            if (device == session.CurrentCase || !device.IsAvailable(session.Stage))
                return;
            SaveScroll();
            session.CurrentCase = device;
            Refresh();
        }

        void OpenApp(AppKind kind)
        {
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            // 좌측 앱 버튼은 해당 앱의 최상위 화면을 연다.
            nav.app = kind;
            if (kind == AppKind.Messages)
                nav.openThreadId = null;
            else if (kind == AppKind.Photos)
            {
                nav.photoAlbumId = null;
                nav.openPhotoId = null;
            }
            Refresh();
        }

        /// <summary>대화방을 연다. 검색 결과의 메시지에서 열었으면 그 메시지 위치로 이동한다.</summary>
        void OpenThread(ThreadEntry entry, MessageData message)
        {
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
            }
        }
    }
}
