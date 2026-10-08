using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Intersection.UI
{
    /// <summary>
    /// 비교 모드 (UI-06). 핀한 기록 두 개를 비교 슬롯에 넣고, 중앙을 덮는 PC 업무 화면에 같은 크기로 나란히 본다.
    /// 슬롯은 임시 UI 상태라 저장하지 않고, 비교 화면을 실제로 연 쌍만 비교 이력(불변 RecordRef.Key)으로 남긴다.
    /// 두 기록의 관계·일치 여부는 판정하지 않는다.
    /// </summary>
    public partial class AppShell
    {
        [Header("비교")]
        [SerializeField] CompareView compareView;
        [SerializeField] CanvasGroup sidebarGroup;
        [SerializeField] CanvasGroup workPanelGroup;

        readonly RecordRef[] compareSlots = new RecordRef[2];
        string compareWarnKey;
        bool comparing;

        int SlotOf(string key) => System.Array.FindIndex(compareSlots, s => s != null && s.Key == key);

        // ───────────── 비교 대상 고르기 ─────────────

        /// <summary>핀한 기록만 비교할 수 있다. 원본 누락·중복·현재 접근 불가면 막는 이유 키, 비교할 수 있으면 null.</summary>
        string CompareBlockReason(RecordRef target)
        {
            if (target.kind == RecordKind.Request)
            {
                switch (RecordAccess.Check(target, config.database, session.Stage, text))
                {
                    case AccessState.Missing: return UIKeys.CompareBlockedMissing;
                    case AccessState.Duplicate: return UIKeys.CompareBlockedDuplicate;
                    case AccessState.Unavailable: return UIKeys.CompareBlockedUnavailable;
                    default: return null;
                }
            }
            if (PlanSource(target, out _, out _, out var reason))
                return null;
            return reason == UIKeys.SourceMissing ? UIKeys.CompareBlockedMissing
                : reason == UIKeys.SourceDuplicate ? UIKeys.CompareBlockedDuplicate
                : UIKeys.CompareBlockedUnavailable;
        }

        /// <summary>핀이 풀렸거나 접근할 수 없게 된 기록은 슬롯에서 뺀다(저장된 과거 핀으로 접근 범위를 우회하지 못하게).</summary>
        void PruneCompareSlots()
        {
            for (int i = 0; i < compareSlots.Length; i++)
            {
                var s = compareSlots[i];
                if (s != null && (!store.IsPinned(s.Key) || CompareBlockReason(s) != null))
                    compareSlots[i] = null;
            }
        }

        /// <summary>작업 기록 패널의 `비교에 추가`/`비교에서 빼기`. 핀하지 않은 기록이면 표시하지 않는다.</summary>
        void CompareTool(RecordRef target, out string label, out System.Action action, out string note)
        {
            label = null;
            action = null;
            note = null;
            if (!store.IsPinned(target.Key))
                return;
            int slot = SlotOf(target.Key);
            if (slot >= 0)
            {
                label = text.Get(UIKeys.ActionCompareRemove);
                action = () =>
                {
                    compareSlots[slot] = null;
                    RefreshWorkPanel();
                };
                return;
            }
            var reason = CompareBlockReason(target);
            if (reason != null)
            {
                note = text.Get(reason);
                return;
            }
            label = text.Get(UIKeys.ActionCompareAdd);
            action = () => AddToCompare(target);
            // 두 칸이 차 있을 때 추가하려 했다면 자동으로 바꾸지 않고 먼저 빼도록 안내한다.
            if (compareWarnKey == target.Key && compareSlots.All(s => s != null))
                note = text.Get(UIKeys.CompareFull);
        }

        /// <summary>작업 기록 목록의 `+ 비교` / `비교 중`: 우측 상세를 거치지 않고 칸에 넣거나 뺀다. 칸이 차 있으면 아무것도 바꾸지 않는다.</summary>
        void ToggleCompareSlot(RecordRef target)
        {
            int slot = SlotOf(target.Key);
            if (slot >= 0)
            {
                compareSlots[slot] = null;
                RefreshWorkPanel();
                return;
            }
            if (!store.IsPinned(target.Key) || CompareBlockReason(target) != null || compareSlots.All(s => s != null))
                return;
            AddToCompare(target);
        }

        void AddToCompare(RecordRef target)
        {
            int empty = System.Array.IndexOf(compareSlots, null);
            if (empty < 0)
                compareWarnKey = target.Key;
            else if (SlotOf(target.Key) < 0)
            {
                compareSlots[empty] = target;
                compareWarnKey = null;
            }
            RefreshWorkPanel();
        }

        void RenderCompareSlots()
        {
            PruneCompareSlots();
            var items = new List<WorkPanelView.SlotItem>();
            for (int i = 0; i < compareSlots.Length; i++)
            {
                var target = compareSlots[i];
                int index = i;
                var res = target == null ? null : RecordResolver.Resolve(target, config.database, text);
                // 슬롯은 필터로 목록에서 숨겨져도 그대로 남으므로 칸 안에 제목·의뢰 표시명·기록 코드를 함께 보여준다.
                // 칸을 누르면 작업 기록을 연다(전체 보기 중이면 전체 보기 안의 상세).
                items.Add(target == null ? null : new WorkPanelView.SlotItem
                {
                    title = res.title,
                    meta = WorkMeta(target, res),
                    onOpen = () => OpenWorkRecord(target),
                    onRemove = () =>
                    {
                        compareSlots[index] = null;
                        RefreshWorkPanel();
                    },
                });
            }
            int filled = compareSlots.Count(s => s != null);
            bool full = filled == compareSlots.Length;
            // 0개: 기본 / 1개: 하나 더 고르세요 / 2개: 비교할 수 있습니다 (비교 화면은 사용자가 `비교`를 눌러야 열린다)
            string headerKey = full ? UIKeys.CompareSlotsReady : filled > 0 ? UIKeys.CompareSlotsOneMore : UIKeys.CompareSlotsHeader;
            workPanel.ShowCompareSlots(
                text.Format(headerKey, ("count", filled.ToString(time.Culture)),
                    ("total", compareSlots.Length.ToString(time.Culture))),
                full ? text.Get(UIKeys.CompareSlotsFullNote) : null,
                items, text.Get(UIKeys.CompareSlotEmpty), text.Get(UIKeys.CompareSlotRemove),
                filled == compareSlots.Length ? EnterCompare : (System.Action)null,
                Theme.panelRaised, Theme.subText, Theme.text);
        }

        // ───────────── 비교 화면 ─────────────

        /// <summary>
        /// 비교 화면을 연다. 휴대전화 탐색 상태·우측 패널·작업 기록은 건드리지 않고 덮기만 하며,
        /// 그동안 좌측·우측 패널 조작을 막아 종료 후 그대로 돌아오게 한다.
        /// </summary>
        void EnterCompare()
        {
            PruneCompareSlots();
            if (comparing || compareSlots.Any(s => s == null))
            {
                RefreshWorkPanel();
                return;
            }
            CloseSearch();
            CloseNotifications();
            var a = compareSlots[0];
            var b = compareSlots[1];
            comparing = true;
            topBar.SetSearchEnabled(false);
            SelectionBus.Set(false, null);
            EventSystem.current?.SetSelectedGameObject(null);
            SetSideInteractable(false);
            compareView.Open(ExitCompare);
            bool shown = FillPane(compareView.Left, a) & FillPane(compareView.Right, b);
            // 두 기록이 실제로 화면에 표시된 뒤에만 비교 이력을 남긴다(사실 기록만, 판정 없음).
            if (shown)
                store.RecordComparison(a, b);
        }

        void ExitCompare()
        {
            if (!comparing)
                return;
            comparing = false;
            compareView.Close();
            topBar.SetSearchEnabled(true);
            SetSideInteractable(true);
            RefreshWorkPanel();
        }

        void SetSideInteractable(bool on)
        {
            foreach (var group in new[] { sidebarGroup, workPanelGroup })
            {
                group.interactable = on;
                group.blocksRaycasts = on;
                group.alpha = on ? 1f : Theme.compareDimAlpha;
            }
        }

        /// <summary>한쪽 패널: 기록 정보 + 원본 표현. 원본을 찾지 못하면 false.</summary>
        bool FillPane(ComparePaneView pane, RecordRef r)
        {
            var db = config.database;
            var res = RecordResolver.Resolve(r, db, text);
            if (!res.found)
                return false;
            var device = res.device;
            var rows = new List<WorkPanelView.Row>
            {
                Row(UIKeys.CompareRowKind, text.Format(UIKeys.CompareKindFormat,
                    ("kind", text.Get(UIKeys.KindKey(r.kind))), ("app", AppName(r.kind))), Theme.regularFont),
            };
            if (r.kind != RecordKind.Request)
                rows.Add(Row(UIKeys.PanelSource, SourceText(res.source, res.serviceKey), Theme.regularFont));
            if (res.hasTime)
                rows.Add(TimeRow(r, res));
            rows.Add(Row(UIKeys.PanelRecordId, RecordCode.Format(text, RecordResolver.PrefixFor(r.kind), r.recordId, r.deviceId), Theme.monoFont));
            pane.SetMeta(OwnerName(device), rows);

            System.Action none = () => { };
            switch (r.kind)
            {
                case RecordKind.Thread:
                {
                    var t = db.threads.First(x => x != null && x.Id == r.recordId);
                    pane.ShowChat().Show(t, device, DeviceQuery.DisplayName(t, device), string.Empty, config, text, time, none, null, null);
                    return true;
                }
                case RecordKind.Message:
                case RecordKind.Attachment:
                case RecordKind.LocationShare:
                {
                    // 해당 말풍선과 앞뒤 최소한의 대화 맥락만 보여준다.
                    var t = db.threads.First(x => x != null && x.Id == r.threadId);
                    pane.ShowChat().Show(t, device, DeviceQuery.DisplayName(t, device), string.Empty, config, text, time, none,
                        null, r.recordId, config.compareMessageContext);
                    return true;
                }
                case RecordKind.Photo:
                {
                    var p = db.photos.First(x => x != null && x.Id == r.recordId && x.device == device);
                    pane.ShowPhoto().Show(p, string.Empty, time.DateLabel(p.takenAt), time.Time(p.takenAt), false, false, Theme, none, none, none);
                    return true;
                }
                case RecordKind.Browser:
                    return ShowDetailPane(pane, BrowserDetail(db.browser.First(x => x != null && x.Id == r.recordId && x.device == device)), r);
                case RecordKind.Map:
                    return ShowDetailPane(pane, MapDetail(db.maps.First(x => x != null && x.Id == r.recordId && x.device == device)), r);
                case RecordKind.File:
                    return ShowDetailPane(pane, FileDetail(db.files.First(x => x != null && x.Id == r.recordId && x.device == device)), r);
                case RecordKind.Setting:
                    return ShowDetailPane(pane, SettingDetail(db.settings.First(x => x != null && x.Id == r.recordId && x.device == device), device), r);
                case RecordKind.Request:
                    pane.ShowCard(res.title, res.body);
                    return true;
                default:
                    return false;
            }
        }

        bool ShowDetailPane(ComparePaneView pane, DetailModel d, RecordRef r)
        {
            pane.ShowDetail().Show(d.title, string.Empty, d.headline, d.hero, d.fields, Theme, () => { }, r);
            return true;
        }

        /// <summary>원래 앱 표시명. 의뢰 요청은 휴대전화 앱이 아니라 업무 프로그램 기록이다.</summary>
        string AppName(RecordKind kind)
        {
            AppKind app;
            switch (kind)
            {
                case RecordKind.Photo: app = AppKind.Photos; break;
                case RecordKind.Browser: app = AppKind.Browser; break;
                case RecordKind.Map: app = AppKind.Maps; break;
                case RecordKind.File: app = AppKind.Files; break;
                case RecordKind.Setting: app = AppKind.Settings; break;
                case RecordKind.Request: return text.Get(UIKeys.CompareAppWork);
                default: app = AppKind.Messages; break;
            }
            var def = config.apps.apps.FirstOrDefault(a => a != null && a.kind == app);
            return def != null ? text.Get(def.nameKey) : string.Empty;
        }
    }
}
