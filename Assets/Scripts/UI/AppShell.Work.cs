using System.Collections.Generic;
using System.IO;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 고정(화면 용어, 코드·저장 이름은 pin)·분류·작업메모·기록 고르기·초반 업무 안내 (UI-05, 안내는 단계 없는 한 줄로 간소화).
    /// 우측 패널은 기본적으로 휴대전화에 열린 기록(현재 열람)을, 작업함이 열려 있으면 작업함에서 고른 자료를 보여주며 바로 고정·분류·메모할 수 있다.
    /// 기록 고르기 모드는 개별 메시지처럼 화면 안의 더 작은 단위를 고를 때만 쓴다.
    /// 휴대전화 화면에는 고르기 모드에서만 얇은 테두리·체크·핀 표시가 보인다.
    /// </summary>
    public partial class AppShell
    {
        [Header("기록 고르기·작업메모")]
        [SerializeField] Button selectToggle;
        [SerializeField] TMP_Text selectToggleLabel;
        [SerializeField] Image selectToggleBackground;
        [SerializeField] TMP_Text selectStatus;
        [SerializeField] RectTransform requestListRoot;

        /// <summary>우측 패널이 무엇을 보여주는지.</summary>
        enum PanelFocus
        {
            /// <summary>휴대전화에 열린 화면 (현재 열람).</summary>
            Current,
            /// <summary>기록 고르기 모드에서 고른 기록 (선택한 기록).</summary>
            Picked,
            /// <summary>고정 자료 빠른 목록에서 원본으로 이동하지 못한 기록(누락·중복·접근 불가). 휴대전화 화면과 기기는 그대로다.</summary>
            WorkRecord,
            /// <summary>좌측에서 연 의뢰 요청.</summary>
            Request,
        }

        WorkStore store;
        bool selecting;
        PanelFocus focus = PanelFocus.Current;
        RecordRef focusRecord;
        System.Action currentDraw;
        RecordRef currentRecord;
        /// <summary>`원본 열기`로 연 기록. 대화방 안의 개별 메시지처럼 화면보다 작은 단위면 패널은 이 기록을 현재 열람으로 보여준다.</summary>
        RecordRef sourceFocus;
        RecordRef pendingSource;
        bool memoWasFocused;
        readonly List<SidebarItemView> requestItems = new List<SidebarItemView>();

        /// <summary>검수·테스트용 업무 상태 저장소.</summary>
        public WorkStore Store => store;

        /// <summary>
        /// 고정·분류·메모 조작이 적용되는 기록 (패널에 보이는 기록). 작업함이 열려 있으면 작업함에서 고른 자료,
        /// 아무것도 고르지 않았으면 없다(P·1·2·0 무시).
        /// </summary>
        RecordRef ActionTarget => WorkbenchOpen ? workbenchSelected
            : focus != PanelFocus.Current ? focusRecord : sourceFocus ?? currentRecord;

        /// <summary>우측 패널에 띄운 기록(작업함에서 고른 자료·작업함 고정 목록에서 연 자료·의뢰 요청). 현재 열람이면 null.</summary>
        RecordRef ShownRecord => WorkbenchOpen ? workbenchSelected
            : focus == PanelFocus.WorkRecord || focus == PanelFocus.Request ? focusRecord : null;

        void InitWork()
        {
            store = new WorkStore(Path.Combine(Application.persistentDataPath, config.workSaveFileName));
            SelectionBus.Reset();
            SelectionBus.IsPinned = store.IsPinned;
            SelectionBus.SelectRequested = Pick;
            selectToggle.onClick.AddListener(() => SetSelecting(!selecting));
            workPanel.BindWorkbench(OpenWorkbenchPinned);
            workRecordsButton.onClick.AddListener(ShowWorkRecords);
            SyncSelection();
        }

        void OnApplicationQuit() => store?.Save();

        /// <summary>휴대전화 화면을 그리면서 그 화면의 패널 내용과 열린 기록을 등록한다. 실제 표시는 RefreshWorkPanel.</summary>
        void Present(System.Action draw, RecordRef record)
        {
            currentDraw = draw;
            currentRecord = record;
        }

        /// <summary>고르기 모드면 이동 대신 고른다. 골랐으면 true.</summary>
        bool TrySelectInstead(RecordRef record)
        {
            if (!selecting || record == null)
                return false;
            Pick(record);
            return true;
        }

        void SetSelecting(bool on)
        {
            if (selecting == on)
                return;
            selecting = on;
            // 기록 고르기는 휴대전화 화면 안을 고르는 모드라 중앙의 검색·작업함을 닫는다.
            if (on)
                CloseCenterScreens();
            // 고르기를 시작하거나 끝내면 패널은 현재 열람으로 돌아간다.
            SaveScroll();
            RenderCenter();
        }

        /// <summary>앱·기기를 바꿀 때 고르기 모드를 끝낸다 (화면은 호출한 쪽이 다시 그린다).</summary>
        void EndSelectingForNavigation() => selecting = false;

        void Pick(RecordRef record)
        {
            if (!selecting || record == null)
                return;
            focus = PanelFocus.Picked;
            focusRecord = record;
            RefreshWorkPanel();
        }

        /// <summary>
        /// 고정 자료를 원본으로 열 수 없을 때(누락·중복·접근 불가) 패널에만 띄워 이유를 보여준다. 휴대전화 화면·현재 기기는 바꾸지 않는다.
        /// 의뢰 요청은 좌측에서 열 때와 같은 내용(상태 줄·진행 작업)을 보여주고, 열람도 같은 기준으로 기록한다.
        /// </summary>
        void OpenWorkRecord(RecordRef target)
        {
            if (selecting)
                SetSelecting(false);
            if (target.kind == RecordKind.Request
                && RecordAccess.Check(target, config.database, session.Stage, text) == AccessState.Accessible)
                store.MarkViewed(target);
            // 휴대전화에 열린 기록과 같아도 패널에 따로 띄운다(원본 열기 불가 이유는 여기서만 보여준다).
            focus = PanelFocus.WorkRecord;
            focusRecord = target;
            compareWarnKey = null;
            RefreshWorkPanel();
        }

        /// <summary>`< 현재 화면` 또는 Esc: 패널만 현재 열람으로 되돌린다.</summary>
        void ReturnPanelToCurrent()
        {
            focus = PanelFocus.Current;
            focusRecord = null;
            RefreshWorkPanel();
        }

        bool PanelShowsOther => focus == PanelFocus.WorkRecord || focus == PanelFocus.Request;

        void SyncSelection()
        {
            // 비교 화면에서는 고르기 표시를 끈다(비교 기록을 선택 표시로 남기지 않음).
            SelectionBus.Set(selecting && !comparing, focus == PanelFocus.Picked ? focusRecord?.Key : null);
            selectToggleLabel.text = text.Get(selecting ? UIKeys.ToolbarSelectEnd : UIKeys.ToolbarSelectStart);
            selectToggleBackground.color = selecting ? Theme.accentSoft : Theme.panelRaised;
            selectStatus.gameObject.SetActive(selecting);
            selectStatus.text = !selecting ? string.Empty
                : focus == PanelFocus.Picked && focusRecord != null
                    ? text.Format(UIKeys.ToolbarPicked, ("kind", text.Get(UIKeys.KindKey(focusRecord.kind))))
                    : text.Get(UIKeys.ToolbarPickHint);
        }

        /// <summary>업무 상태가 바뀐 뒤: 화면 표시(체크·핀), 패널, 작업 기록, 튜토리얼을 다시 그린다.</summary>
        void AfterWorkChange()
        {
            SelectionBus.Notify();
            RefreshWorkPanel();
        }

        /// <summary>
        /// RenderCenter 뒤에 호출된다. 패널이 보여줄 대상에 따라 내용·머리말·조작을 그린다.
        /// 작업함이 열려 있으면 작업함에서 고른 자료의 상세(돌아가기 `< 현재 화면`은 작업함을 닫음),
        /// 아니면 현재 열람·고른 기록·고정 목록에서 연 기록·의뢰 요청.
        /// 다시 그린 뒤 패널에 있던 키보드 포커스를 같은 기록·조작으로 되살린다.
        /// </summary>
        void RefreshWorkPanel()
        {
            var keyboardFocus = workPanel.CaptureFocus();
            if (focus == PanelFocus.Picked && !selecting)
                focus = PanelFocus.Current;
            if (focus != PanelFocus.Current && focusRecord == null)
                focus = PanelFocus.Current;
            // 비교 슬롯 정리는 보조 행동(비교에 추가/빼기)을 그리기 전에 한다.
            PruneCompareSlots();

            bool bench = WorkbenchOpen;
            workPanel.SetMode(bench ? WorkPanelView.Mode.Workbench : WorkPanelView.Mode.Simple);
            if (bench)
            {
                if (workbenchSelected != null)
                    DrawRecord(workbenchSelected);
                else
                    workPanel.Show(text.Get(UIKeys.WorkbenchNoSelection), text.Get(UIKeys.WorkbenchNoSelectionBody),
                        new WorkPanelView.Row[0]);
                // 머리말에는 기록 종류(사진·의뢰 요청 등)를, 돌아가기는 작업함을 닫는 `< 현재 화면`.
                workPanel.SetHeading(text.Get(workbenchSelected != null ? UIKeys.KindKey(workbenchSelected.kind) : UIKeys.FullViewTitle),
                    CloseWorkbench, text.Get(UIKeys.PanelBackToCurrent));
            }
            else if (focus == PanelFocus.Current)
            {
                if (sourceFocus != null && (currentRecord == null || sourceFocus.Key != currentRecord.Key))
                    DrawRecord(sourceFocus);
                else
                    currentDraw?.Invoke();
                workPanel.SetHeading(text.Get(UIKeys.PanelHeadingCurrent), null);
            }
            else
            {
                DrawRecord(focusRecord);
                string heading = text.Get(focus == PanelFocus.Picked ? UIKeys.PanelHeadingPicked
                    : focus == PanelFocus.Request ? UIKeys.PanelHeadingRequest : UIKeys.PanelHeadingWork);
                workPanel.SetHeading(heading, PanelShowsOther ? ReturnPanelToCurrent : (System.Action)null,
                    text.Get(UIKeys.PanelBackToCurrent));
            }
            RenderRecordTools();
            RenderJobs();

            var target = ActionTarget;
            workPanel.SetActions(target != null ? ActionsFor(target) : null);
            RenderWorkList();
            RenderCompareSlots();
            RenderGuideLine();
            RenderWorkToolbar();
            SyncSelection();
            workPanel.ApplyNavigation();
            workPanel.RestoreFocus(keyboardFocus, null);
        }

        WorkPanelView.Actions ActionsFor(RecordRef target) => new WorkPanelView.Actions
        {
            pinned = store.IsPinned(target.Key),
            // 의뢰 요청은 업무 문서라 개인 자료용 보존·삭제 후보를 두지 않는다.
            showClassification = target.kind != RecordKind.Request,
            classification = store.ClassOf(target.Key),
            memo = store.MemoOf(target.Key),
            pinText = text.Get(UIKeys.ActionPin),
            unpinText = text.Get(UIKeys.ActionUnpin),
            primaryColor = Theme.accentSoft,
            primaryTextColor = Theme.accent,
            onColor = Theme.accentSoft,
            onTextColor = Theme.accent,
            idleColor = Theme.panelRaised,
            idleTextColor = Theme.text,
            togglePin = () => TogglePin(target),
            toggleClass = c => ToggleClass(target, c),
            memoChanged = memo =>
            {
                store.SetMemo(target, memo);
                RenderWorkList();
                // 메모만 있는 기록도 작업 기록이라 툴바 개수를 목록과 함께 맞춘다.
                RenderWorkToolbar();
            },
        };

        void TogglePin(RecordRef target)
        {
            store.SetPinned(target, !store.IsPinned(target.Key));
            AfterWorkChange();
        }

        /// <summary>보존·삭제는 토글이다. 켜진 쪽을 다시 누르면 미분류로 돌아가고, 둘은 동시에 켜지지 않는다.</summary>
        void ToggleClass(RecordRef target, Classification c)
        {
            SetClass(target, store.ClassOf(target.Key) == c ? Classification.Unclassified : c);
        }

        void SetClass(RecordRef target, Classification c)
        {
            if (target.kind == RecordKind.Request)
                return;
            store.SetClassification(target, c);
            AfterWorkChange();
        }

        // ───────────── 원본 열기 ─────────────

        /// <summary>
        /// 패널에 보이는 기록의 보조 행동: `비교에 추가`/`비교에서 빼기`(핀과 무관, 지금 보는 원본에서 바로)와,
        /// 작업 기록으로 띄웠을 때만 `원본 열기`. 의뢰 요청은 휴대전화 자료가 아니라 원본 열기를 숨긴다.
        /// 열거나 비교할 수 없으면 이유를 한 번만 보여준다.
        /// </summary>
        void RenderRecordTools()
        {
            var target = ActionTarget;
            if (target == null)
            {
                workPanel.SetRecordTools(null, null, null, null, null, null);
                return;
            }
            string sourceText = null, sourceNote = null;
            System.Action onSource = null;
            // 원본 열기는 작업함에서 고른 자료와 고정 목록에서 이동하지 못하고 패널에 띄운 자료에만 둔다(현재 열람은 이미 원본 화면).
            if ((WorkbenchOpen || focus == PanelFocus.WorkRecord) && target.kind != RecordKind.Request)
            {
                if (PlanSource(target, out _, out _, out var reasonKey))
                {
                    sourceText = text.Get(target.kind == RecordKind.LocationShare ? UIKeys.ActionOpenSourceMessage : UIKeys.ActionOpenSource);
                    onSource = () => OpenSource(target);
                }
                else if (reasonKey != null)
                    sourceNote = text.Get(reasonKey);
            }
            CompareTool(target, out var compareText, out var onCompare, out var compareNote);
            // 같은 이유(원본 없음 등)로 둘 다 막혔으면 "열거나 비교할 수 없음" 한 줄만 보여준다.
            if (sourceNote != null && onCompare == null && compareNote != null)
                sourceNote = null;
            workPanel.SetRecordTools(sourceText, onSource, sourceNote, compareText, onCompare, compareNote);
        }

        /// <summary>대화방 전체는 날짜 범위, 위치 기록은 시각 범위, 나머지는 한 시점.</summary>
        WorkPanelView.Row TimeRow(RecordRef target, ResolvedRecord res) =>
            !res.hasEnd
                ? Row(UIKeys.PanelRecordTime, time.SinceDeathWithTime(res.time), Theme.regularFont)
                : Row(UIKeys.PanelPeriod, target.kind == RecordKind.Thread
                    ? time.SinceDeathRange(res.time, res.endTime)
                    : time.SinceDeathTimeRange(res.time, res.endTime), Theme.regularFont);

        /// <summary>
        /// 저장된 불변 ID를 해석해 원본이 있는 기기·앱·화면 상태를 계산한다. 열 수 없으면 false와 이유 키.
        /// 기록 정체성(RecordRef)은 바꾸지 않고 탐색 상태만 만든다.
        /// </summary>
        bool PlanSource(RecordRef target, out CaseData device, out System.Action<DeviceNavigation> apply, out string reasonKey)
        {
            apply = null;
            reasonKey = null;
            var db = config.database;
            var stage = session.Stage;
            // 접근 판정은 앱 목록·검색·비교와 같은 RecordAccess를 쓴다. 과거 저장 참조로 현재 단계를 우회하지 못한다.
            var access = RecordAccess.Check(target, db, stage, text, out var res);
            device = res.device;
            if (access == AccessState.Missing || access == AccessState.Duplicate)
            {
                reasonKey = access == AccessState.Duplicate ? UIKeys.SourceDuplicate : UIKeys.SourceMissing;
                return false;
            }
            if (access == AccessState.Unavailable)
            {
                reasonKey = UIKeys.SourceUnavailable;
                return false;
            }
            string id = target.recordId;
            switch (target.kind)
            {
                case RecordKind.Thread:
                case RecordKind.Message:
                case RecordKind.Attachment:
                case RecordKind.LocationShare:
                {
                    // 개별 메시지·첨부·위치 공유는 대화방을 열고 그 메시지 위치로 스크롤한다.
                    string threadId = target.kind == RecordKind.Thread ? id : target.threadId;
                    string focusId = target.kind == RecordKind.Thread ? null : id;
                    if (!DeviceQuery.Threads(db, device, stage).Any(e => e.thread.Id == threadId))
                        break;
                    apply = nav =>
                    {
                        nav.app = AppKind.Messages;
                        nav.openThreadId = threadId;
                        nav.focusMessageId = focusId;
                        nav.scroll.Remove(threadId);
                    };
                    return true;
                }
                case RecordKind.Photo:
                {
                    var all = PhotoQuery.DevicePhotos(db, device, stage);
                    var photo = all.FirstOrDefault(p => p.Id == id);
                    // 사진이 들어 있는 첫 앨범(앨범 순서 기준)에서 한 장 보기로 연다.
                    var album = photo == null ? null
                        : PhotoQuery.Albums(db, device).FirstOrDefault(a => PhotoQuery.AlbumPhotos(a, all).Contains(photo));
                    if (album == null)
                        break;
                    apply = nav =>
                    {
                        nav.app = AppKind.Photos;
                        nav.photoAlbumId = album.Id;
                        nav.openPhotoId = photo.Id;
                    };
                    return true;
                }
                case RecordKind.Browser:
                    if (RecordQuery.For(db.browser, device, stage).Any(r => r.Id == id))
                        return OpenRecordPlan(AppKind.Browser, id, out apply);
                    break;
                case RecordKind.Map:
                    if (RecordQuery.For(db.maps, device, stage).Any(r => r.Id == id))
                        return OpenRecordPlan(AppKind.Maps, id, out apply);
                    break;
                case RecordKind.Setting:
                    if (RecordQuery.For(db.settings, device, stage).Any(r => r.Id == id))
                        return OpenRecordPlan(AppKind.Settings, id, out apply);
                    break;
                case RecordKind.File:
                {
                    // 파일의 출처(위치)와 폴더로 이동해 상세를 연다.
                    var file = RecordQuery.For(db.files, device, stage).FirstOrDefault(f => f.Id == id);
                    if (file == null)
                        break;
                    apply = nav =>
                    {
                        nav.app = AppKind.Files;
                        nav.filesLocation = file.source;
                        nav.filesPath = file.FolderPath;
                        nav.SetOpenRecord(AppKind.Files, id);
                    };
                    return true;
                }
                default:
                    return false;
            }
            reasonKey = UIKeys.SourceUnavailable;
            return false;
        }

        static bool OpenRecordPlan(AppKind app, string id, out System.Action<DeviceNavigation> apply)
        {
            apply = nav =>
            {
                nav.app = app;
                nav.SetOpenRecord(app, id);
            };
            return true;
        }

        /// <summary>
        /// 사용자가 `원본 열기`를 눌렀을 때만 기기·앱을 바꿔 원본을 연다. 바꾸기 전 화면 상태는 기기별로 그대로 보존된다.
        /// 연 뒤 패널은 현재 열람으로 돌아가고, 고르기 모드는 켜지지 않으며, 열람 외 업무 상태는 바꾸지 않는다.
        /// </summary>
        void OpenSource(RecordRef target)
        {
            if (!PlanSource(target, out var device, out var apply, out _))
                return;
            SaveScroll();
            // 원본은 중앙 휴대전화에 열리므로 검색·작업함을 닫는다(작업함의 필터·스크롤·선택은 다음에 열 때 그대로).
            CloseCenterScreens();
            EndSelectingForNavigation();
            session.CurrentCase = device;
            apply(session.Nav(device));
            if (target.kind == RecordKind.Message || target.kind == RecordKind.Attachment || target.kind == RecordKind.LocationShare)
            {
                // 메시지를 열면 그 대화방도 연 것이므로 목록에서 대화방을 열 때와 같이 열람으로 기록한다.
                var thread = config.database.threads.FirstOrDefault(t => t != null && t.Id == target.threadId);
                if (thread != null)
                    store.MarkViewed(RecordRef.ForThread(device, thread));
            }
            store.MarkViewed(target);
            pendingSource = target;
            Refresh();
        }

        // ───────────── 패널에 띄운 기록 (고른 기록·작업 기록·의뢰 요청) ─────────────

        void DrawRecord(RecordRef target)
        {
            var res = RecordResolver.Resolve(target, config.database, text);
            var entry = store.Get(target.Key);
            string code = RecordCode.Format(text, RecordResolver.PrefixFor(target.kind), target.recordId, target.deviceId);
            if (!res.found)
            {
                // 원본이 없어도 다른 기록으로 대체하지 않고, 저장된 ID 그대로 누락 상태로 보여준다.
                workPanel.Show(text.Get(res.duplicate ? UIKeys.PanelMissingDuplicate : UIKeys.PanelMissing),
                    text.Get(res.duplicate ? UIKeys.PanelMissingDuplicateBody : UIKeys.PanelMissingBody), new[] { Row(UIKeys.PanelRecordId, code, Theme.monoFont) });
                return;
            }
            if (RecordAccess.Check(target, config.database, session.Stage, text) == AccessState.Unavailable)
            {
                // 저장된 과거 참조라도 현재 단계에서 접근할 수 없는 기록은 제목·파일명·연락처·미리보기를 보여주지 않는다.
                workPanel.Show(text.Get(UIKeys.PanelUnavailable), text.Get(UIKeys.PanelUnavailableBody),
                    new[] { Row(UIKeys.PanelRecordId, code, Theme.monoFont) });
                return;
            }

            // 의뢰 요청은 본문 바로 아래에 업무 상태(대상의 현재 분류)를 먼저 보여준다.
            var rows = new List<WorkPanelView.Row>();
            if (target.kind == RecordKind.Request)
                rows.AddRange(GuideRows(target));
            rows.Add(Row(UIKeys.PanelRecordId, code, Theme.monoFont));
            rows.Add(Row(UIKeys.PanelOwner, OwnerName(res.device), Theme.regularFont));
            if (target.kind != RecordKind.Request)
                rows.Add(Row(UIKeys.PanelSource, SourceText(res.source, res.serviceKey), Theme.regularFont));
            if (res.hasTime)
                rows.Add(TimeRow(target, res));
            rows.Add(Row(UIKeys.PanelIntegrity, string.IsNullOrEmpty(res.integrityKey) ? null : text.Get(res.integrityKey), Theme.regularFont));
            rows.Add(Row(UIKeys.PanelViewed, text.Get(entry != null && entry.viewed ? UIKeys.ViewedYes : UIKeys.ViewedNo), Theme.regularFont));
            workPanel.Show(res.title, res.body, rows);
        }

        // ───────────── 의뢰 요청·초반 업무 안내 ─────────────

        IEnumerable<WorkRequestData> Requests(CaseData device) =>
            config.database.requests
                .Where(q => q != null && q.device == device && session.Stage >= q.availableFrom)
                .OrderBy(q => q.order);

        void RenderRequests()
        {
            foreach (var item in requestItems)
                UIPool.Discard(item.gameObject);
            requestItems.Clear();
            var device = session.CurrentCase;
            if (device == null)
                return;
            foreach (var request in Requests(device))
            {
                var q = request;
                var item = Instantiate(appItemPrefab, requestListRoot);
                item.Bind(null, q.title, null, false, true, Theme.accentSoft, Color.clear, () => OpenRequest(q));
                requestItems.Add(item);
            }
        }

        /// <summary>의뢰 요청은 업무 프로그램 기록이라 휴대전화가 아니라 우측 패널에 열리고, 바로 핀할 수 있다.</summary>
        void OpenRequest(WorkRequestData request)
        {
            // 좌측 요청은 좌측 탐색이라 중앙의 검색·작업함을 닫고 패널에 요청을 띄운다.
            CloseCenterScreens();
            if (selecting)
                SetSelecting(false);
            var target = RecordRef.ForRequest(request);
            store.MarkViewed(target);
            focus = PanelFocus.Request;
            focusRecord = target;
            RefreshWorkPanel();
        }

        IEnumerable<TutorialData> Guides() => config.database.tutorials.Where(t => t != null && t.request != null);

        /// <summary>
        /// 요청 패널의 업무 상태 한 줄: 대상 이름 · 지금 분류(미분류 / 보존 지정됨 / 삭제 후보). 단계·완료율·체크리스트는 없다.
        /// 단계와 무관하게 늘 현재 분류를 그대로 보여준다.
        /// </summary>
        IEnumerable<WorkPanelView.Row> GuideRows(RecordRef request)
        {
            foreach (var guide in Guides().Where(g => RecordRef.ForRequest(g.request).Key == request.Key))
            {
                var target = RecordRef.ForAsset(guide.target);
                if (target == null)
                    continue;
                yield return new WorkPanelView.Row
                {
                    label = guide.targetLabel,
                    value = text.Get(UIKeys.GuideStateKey(store.ClassOf(target.Key))),
                    font = Theme.regularFont,
                };
            }
        }

        /// <summary>
        /// 하단 안내 한 줄. 요청을 읽은 뒤, 대상이 아직 보존이 아니고 안내 종료 단계(hintUntil) 전일 때만 데이터의 안내 문구를 보여준다.
        /// 요청을 읽기 전에는 조작을 먼저 지시하지 않고, 보존하거나 다음 단계로 넘어가면 숨긴다(이후 재분류해도 돌아오지 않음).
        /// </summary>
        void RenderGuideLine()
        {
            string line = null;
            foreach (var guide in Guides())
            {
                var target = RecordRef.ForAsset(guide.target);
                if (target == null || session.Stage < guide.request.availableFrom || session.Stage >= guide.hintUntil)
                    continue;
                if (!store.IsViewed(RecordRef.ForRequest(guide.request).Key) || store.ClassOf(target.Key) == Classification.Keep)
                    continue;
                line = guide.hint;
                break;
            }
            workPanel.ShowTutorial(line);
        }

        // ───────────── 키보드 ─────────────

        /// <summary>
        /// Esc 우선순위: 비교 화면이면 비교 종료 → 알림 목록 닫기 → 검색 닫기 → 메모 입력 중이면 입력값을 저장하고 포커스만 해제
        /// → 작업함이 열려 있으면 작업함 닫기(보던 휴대전화로) → 패널에 다른 기록을 띄웠으면 현재 열람으로
        /// → 고르기 모드면 종료 → 휴대전화 뒤로. 한 번의 Esc는 한 단계만 처리하고 아래 단계로 넘기지 않는다.
        /// </summary>
        void HandleEscape()
        {
            if (comparing)
                ExitCompare();
            else if (notificationPanel.IsOpen)
                CloseNotifications();
            else if (searchView.IsOpen)
                CloseSearch();
            else if (workPanel.IsEditingMemo || memoWasFocused)
                workPanel.EndMemoEdit();
            else if (WorkbenchOpen)
                CloseWorkbench();
            else if (PanelShowsOther)
                ReturnPanelToCurrent();
            else if (selecting)
                SetSelecting(false);
            else
                Back();
        }

        /// <summary>패널에 보이는 기록에 적용: P 핀, 1 보존 토글, 2 삭제 토글, 0 미분류. 입력 중에는 무시한다.</summary>
        void HandleWorkKeys(UnityEngine.InputSystem.Keyboard keyboard)
        {
            var target = ActionTarget;
            if (target == null || IsTyping())
                return;
            if (keyboard.pKey.wasPressedThisFrame)
                TogglePin(target);
            else if (keyboard.digit1Key.wasPressedThisFrame)
                ToggleClass(target, Classification.Keep);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                ToggleClass(target, Classification.Delete);
            else if (keyboard.digit0Key.wasPressedThisFrame)
                SetClass(target, Classification.Unclassified);
        }

        bool IsTyping()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var current = events != null ? events.currentSelectedGameObject : null;
            var field = current != null ? current.GetComponent<TMP_InputField>() : null;
            return field != null && field.isFocused;
        }
    }
}
