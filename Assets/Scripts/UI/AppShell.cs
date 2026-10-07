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
    /// UI-01 외곽 셸. 원본 데이터(ContentDatabase) → 표시 상태(SessionState) → 각 뷰로 흘려보낸다.
    /// 화면 문구는 모두 StringTable, 콘텐츠는 모두 데이터 에셋에서 온다.
    /// </summary>
    public class AppShell : MonoBehaviour
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
            session = new SessionState(config.startStage);
            foreach (var label in GetComponentsInChildren<LocalizedText>(true))
                label.Apply(text);
            session.CurrentCase = Cases().FirstOrDefault(c => c.IsAvailable(session.Stage));
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
                Destroy(item.gameObject);
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
            int threadCount = DeviceQuery.Threads(config.database, current, session.Stage).Count;
            foreach (var app in config.apps.apps.Where(a => a != null).OrderBy(a => a.order))
            {
                var def = app;
                // 개수는 실제로 조회 가능한 데이터가 있는 앱만 계산해 표시한다.
                string count = app.kind == AppKind.Messages && app.implemented ? threadCount.ToString(time.Culture) : null;
                var item = Instantiate(appItemPrefab, appListRoot);
                item.Bind(null, text.Get(app.nameKey), count, current != null && nav.app == app.kind,
                    app.implemented && current != null, Theme.accentSoft, Color.clear, () => OpenApp(def.kind));
                sidebarItems.Add(item);
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
            var entries = DeviceQuery.Threads(config.database, device, session.Stage);
            var open = nav.openThreadId != null ? entries.FirstOrDefault(e => e.thread.Id == nav.openThreadId) : null;
            if (open == null)
            {
                nav.openThreadId = null;
                phone.ShowList();
                nav.scroll.TryGetValue(string.Empty, out var listScroll);
                phone.MessageList.Show(entries, text, time, Theme, OpenThread,
                    nav.scroll.ContainsKey(string.Empty) ? listScroll : (float?)null);
                ShowListPanel(device, entries);
            }
            else
            {
                phone.ShowChat();
                int unread = DeviceQuery.UnreadExcept(entries, open.thread);
                string back = unread > 0
                    ? text.Format(UIKeys.ChatBackUnread, ("count", unread.ToString(time.Culture)))
                    : text.Get(UIKeys.ChatBack);
                float? scroll = nav.scroll.TryGetValue(open.thread.Id, out var s) ? s : (float?)null;
                phone.Chat.Show(open.thread, device, open.displayName, back, config, text, time, Back, scroll);
                ShowThreadPanel(device, open);
            }
        }

        void ShowListPanel(CaseData device, List<ThreadEntry> entries)
        {
            workPanel.Show(text.Get(UIKeys.PanelTitleMessageList), new[]
            {
                Row(UIKeys.PanelOwner, device.owner != null ? device.owner.fullName : device.DisplayName, Theme.regularFont),
                Row(UIKeys.PanelSource, text.Get(UIKeys.SourceLocal), Theme.regularFont),
                Row(UIKeys.PanelThreadCount,
                    text.Format(UIKeys.PanelThreadCountValue, ("count", entries.Count.ToString(time.Culture))),
                    Theme.regularFont),
            });
        }

        void ShowThreadPanel(CaseData device, ThreadEntry entry)
        {
            var ordered = DeviceQuery.Ordered(entry.thread);
            string source = string.IsNullOrEmpty(entry.state.serviceKey)
                ? text.Get(UIKeys.SourceKey(entry.state.source))
                : text.Format(UIKeys.PanelSourceFormat,
                    ("source", text.Get(UIKeys.SourceKey(entry.state.source))),
                    ("service", text.Get(entry.state.serviceKey)));
            workPanel.Show(text.Format(UIKeys.PanelTitleThread, ("name", entry.displayName)), new[]
            {
                // 증거 마스터 ID(evidenceRef)는 표시하지 않는다. 모든 대화가 같은 형식의 중립 코드를 가진다.
                Row(UIKeys.PanelRecordId, RecordCode.Format(text, UIKeys.RecordPrefixThread, entry.thread.Id, device.Id), Theme.monoFont),
                Row(UIKeys.PanelOwner, device.owner != null ? device.owner.fullName : device.DisplayName, Theme.regularFont),
                Row(UIKeys.PanelSource, source, Theme.regularFont),
                Row(UIKeys.PanelPeriod, time.SinceDeathRange(ordered[0].time, ordered[ordered.Count - 1].time), Theme.regularFont),
                Row(UIKeys.PanelIntegrity, string.IsNullOrEmpty(entry.state.integrityKey) ? null : text.Get(entry.state.integrityKey), Theme.regularFont),
            });
        }

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
            nav.openThreadId = null;
            Refresh();
        }

        void OpenThread(ThreadEntry entry)
        {
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            nav.openThreadId = entry.thread.Id;
            nav.scroll.Remove(entry.thread.Id);
            RenderCenter();
        }

        void Back()
        {
            var nav = session.Nav(session.CurrentCase);
            if (nav.openThreadId == null)
                return;
            SaveScroll();
            nav.openThreadId = null;
            RenderCenter();
        }

        void SaveScroll()
        {
            if (session.CurrentCase == null)
                return;
            var nav = session.Nav(session.CurrentCase);
            if (nav.openThreadId == null)
                nav.scroll[string.Empty] = phone.MessageList.ScrollPosition;
            else
                nav.scroll[nav.openThreadId] = phone.Chat.ScrollPosition;
        }
    }
}
