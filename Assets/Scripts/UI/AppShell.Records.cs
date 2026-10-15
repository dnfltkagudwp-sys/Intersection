using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>
    /// 브라우저·지도·파일·설정 앱. 네 앱 모두 같은 목록·상세 화면을 쓰고, 항목은 데이터에서 계산한다.
    /// 휴대전화 안에는 기기에서 보이는 정보만, 기록 코드·출처·사망일 기준 시각은 업무 패널에만 둔다.
    /// </summary>
    public partial class AppShell
    {
        static bool IsRecordApp(AppKind kind) =>
            kind == AppKind.Browser || kind == AppKind.Maps || kind == AppKind.Files || kind == AppKind.Settings || kind == AppKind.Mail;

        void RenderRecords(CaseData device, DeviceNavigation nav)
        {
            switch (nav.app)
            {
                case AppKind.Browser: RenderBrowser(device, nav); break;
                case AppKind.Maps: RenderMaps(device, nav); break;
                case AppKind.Files: RenderFiles(device, nav); break;
                case AppKind.Settings: RenderSettings(device, nav); break;
                case AppKind.Mail: RenderMail(device, nav); break;
            }
        }

        static T FindById<T>(List<T> records, string id) where T : ContentAsset =>
            id == null ? null : records.FirstOrDefault(r => r.Id == id);

        void ShowRecordList(DeviceNavigation nav, string title, string backText, List<PhoneListView.Item> items, Action onBack,
            string emptyKey = UIKeys.ListEmpty)
        {
            phone.Show(PhoneView.Screen.RecordList);
            phone.RecordList.Show(title, backText, items, text.Get(emptyKey), Theme, onBack,
                SavedScroll(nav, nav.ListScrollKey(nav.app)));
        }

        /// <summary>상세 화면 한 장의 내용. 휴대전화 상세와 비교 화면이 같은 표현을 쓴다.</summary>
        class DetailModel
        {
            public string title;
            public string headline;
            public PhoneDetailView.Hero hero;
            /// <summary>미리보기 자리에 그대로 그리는 문서(파일 visibleText). 없으면 null.</summary>
            public IReadOnlyList<FileTextBlock> document;
            public List<(string, string)> fields;
            /// <summary>문서를 정보 줄 아래에 둔다(메일 본문). 기본은 미리보기 자리.</summary>
            public bool documentLast;
            /// <summary>정보 줄 값을 말줄임 없이 줄바꿈해 전체를 보여준다(메일 주소·첨부).</summary>
            public bool wrapFields;
        }

        /// <summary>상세 화면을 보여주고 그 기록을 열람한 것으로 기록한다.</summary>
        void ShowRecordDetail(DetailModel d, string backText, RecordRef selection)
        {
            store.MarkViewed(selection);
            phone.Show(PhoneView.Screen.RecordDetail);
            phone.RecordDetail.Show(d.title, backText, d.headline, d.hero, d.fields, Theme, Back, selection, d.document, text, d.documentLast, d.wrapFields);
        }

        DetailModel BrowserDetail(BrowserRecord r) => new DetailModel
        {
            title = BrowserKind(r),
            headline = r.Title,
            hero = new PhoneDetailView.Hero { glyph = Theme.pageGlyph, message = text.Get(UIKeys.BrowserPageUnavailable) },
            fields = new List<(string, string)>
            {
                (text.Get(UIKeys.FieldKind), BrowserKind(r)),
                (text.Get(UIKeys.FieldDateTime), PhoneDateTime(r.time)),
            },
        };

        DetailModel MapDetail(MapRecord r)
        {
            var fields = new List<(string, string)>();
            if (r.kind == MapRecordKind.Route)
            {
                fields.Add((text.Get(UIKeys.MapsFieldFrom), r.RouteFrom));
                fields.Add((text.Get(UIKeys.MapsFieldTo), r.RouteTo));
                fields.Add((text.Get(UIKeys.MapsFieldVia), r.RouteNote));
            }
            else if (!string.IsNullOrEmpty(r.PlaceLabel))
                fields.Add((text.Get(UIKeys.MapsFieldPlace), r.PlaceLabel));
            fields.Add((text.Get(UIKeys.FieldDateTime), MapTimeText(r)));
            return new DetailModel { title = MapSectionTitle(r.kind), headline = MapTitle(r), hero = MapHero, fields = fields };
        }

        PhoneDetailView.Hero MapHero => new PhoneDetailView.Hero { background = Theme.mapPlaceholder, glyph = Theme.pinGlyph };

        DetailModel FileDetail(FileRecord f)
        {
            string locationLabel = text.Get(UIKeys.FilesLocationKey(f.source));
            // 미리보기 이미지가 없고 표시 문구가 있으면 그 문구를 문서로 그린다(검색과 같은 원본).
            // 이미지가 있으면 visibleText는 이미지 글자의 전사본이라 따로 그리지 않는다. 둘 다 없으면 지금처럼 `미리보기 없음`.
            var document = f.RenderedDocument(time.NoteDate);
            return new DetailModel
            {
                title = f.FileName,
                headline = f.FileName,
                document = document,
                hero = document != null ? null : new PhoneDetailView.Hero
                {
                    texture = f.preview,
                    glyph = IsImageFile(f.FileName) ? Theme.photoGlyph : Theme.documentGlyph,
                    message = text.Get(UIKeys.FilesPreviewUnavailable),
                },
                fields = new List<(string, string)>
                {
                    (text.Get(UIKeys.FilesFieldName), f.FileName),
                    (text.Get(UIKeys.FilesFieldModified), PhoneDateTime(f.time)),
                    (text.Get(UIKeys.FilesFieldWhere), f.FolderPath.Length == 0 ? locationLabel : locationLabel + "/" + f.FolderPath),
                },
            };
        }

        DetailModel SettingDetail(SettingRecord s, CaseData device)
        {
            string item = RecordQuery.SettingItemLabel(s, device);
            return new DetailModel
            {
                title = s.CategoryLabel,
                headline = item,
                hero = null,
                fields = new List<(string, string)>
                {
                    (text.Get(UIKeys.SettingsFieldItem), item),
                    (text.Get(UIKeys.SettingsFieldBefore), s.ValueBefore),
                    (text.Get(UIKeys.SettingsFieldAfter), s.ValueAfter),
                    (text.Get(UIKeys.FieldDateTime), PhoneDateTime(s.time)),
                },
            };
        }

        /// <summary>날짜가 바뀔 때마다 구역 제목(오늘·어제·요일·날짜)을 넣는다.</summary>
        void AddDateSections<T>(List<PhoneListView.Item> items, IEnumerable<T> newestFirst, Func<T, RelativeTime> timeOf,
            Func<T, PhoneListView.Item> toItem)
        {
            string current = null;
            foreach (var r in newestFirst)
            {
                string label = time.DateLabel(timeOf(r));
                if (label != current)
                {
                    items.Add(PhoneListView.Item.Section(label));
                    current = label;
                }
                items.Add(toItem(r));
            }
        }

        string PhoneDateTime(RelativeTime t) =>
            text.Format(UIKeys.SeparatorTemplate, ("date", time.DateLabel(t)), ("time", time.Time(t)));

        // ───────────── 업무 패널 ─────────────

        void ShowCollectionPanel(string titleKey, CaseData device, int count) => Present(() =>
        {
            workPanel.Show(text.Get(titleKey), new[]
            {
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelRecordCount,
                    text.Format(UIKeys.PanelRecordCountValue, ("count", count.ToString(time.Culture))), Theme.regularFont),
            });
        }, null);

        /// <summary>기록 한 건의 업무 패널. 증거 ID와 작가 메모는 표시하지 않는다. record는 핀·분류·메모 대상.</summary>
        void ShowRecordPanel(string title, string prefixKey, string recordId, CaseData device,
            string source, string timeLabelKey, string timeValue, string integrityKey, RecordRef record) => Present(() =>
        {
            workPanel.Show(title, new List<WorkPanelView.Row>
            {
                Row(UIKeys.PanelRecordId, RecordCode.Format(text, prefixKey, recordId, device.Id), Theme.monoFont),
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelSource, source, Theme.regularFont),
                Row(timeLabelKey, timeValue, Theme.regularFont),
                Row(UIKeys.PanelIntegrity, string.IsNullOrEmpty(integrityKey) ? null : text.Get(integrityKey), Theme.regularFont),
            });
        }, record);

        void ShowRecordPanel(DeviceRecord r, string title, string prefixKey, CaseData device) =>
            ShowRecordPanel(title, prefixKey, r.Id, device, SourceText(r.source, null),
                UIKeys.PanelRecordTime, time.SinceDeathWithTime(r.time), r.integrityKey, RecordRef.ForRecord(r));

        // ───────────── 브라우저 ─────────────

        void RenderBrowser(CaseData device, DeviceNavigation nav)
        {
            var records = RecordQuery.For(config.database.browser, device, session.Stage);
            var open = FindById(records, nav.OpenRecord(AppKind.Browser));
            nav.SetOpenRecord(AppKind.Browser, open?.Id);
            string appTitle = text.Get(UIKeys.BrowserTitle);

            if (open != null)
            {
                ShowRecordDetail(BrowserDetail(open), appTitle, RecordRef.ForRecord(open));
                ShowRecordPanel(open, open.Title, UIKeys.RecordPrefixWeb, device);
                return;
            }

            var items = new List<PhoneListView.Item>();
            AddDateSections(items, RecordQuery.Newest(records), r => r.time, r => new PhoneListView.Item
            {
                title = r.Title,
                subtitle = BrowserKind(r),
                trailing = time.Time(r.time),
                icon = r.kind == BrowserRecordKind.Search ? Theme.searchGlyph : Theme.pageGlyph,
                selection = RecordRef.ForRecord(r),
                onClick = () => OpenRecordDetail(AppKind.Browser, RecordRef.ForRecord(r)),
            });
            ShowRecordList(nav, appTitle, null, items, null);
            ShowCollectionPanel(UIKeys.PanelTitleBrowser, device, records.Count);
        }

        string BrowserKind(BrowserRecord r) =>
            text.Get(r.kind == BrowserRecordKind.Search ? UIKeys.BrowserKindSearch : UIKeys.BrowserKindVisit);

        // ───────────── 지도 ─────────────

        void RenderMaps(CaseData device, DeviceNavigation nav)
        {
            var records = RecordQuery.For(config.database.maps, device, session.Stage);
            var pins = RecordQuery.SharedPins(config.database, device, session.Stage);
            string openId = nav.OpenRecord(AppKind.Maps);
            var open = FindById(records, openId);
            var openPin = open == null && openId != null ? pins.FirstOrDefault(p => p.message.Id == openId) : null;
            if (open == null && openPin == null)
                nav.SetOpenRecord(AppKind.Maps, null);
            string appTitle = text.Get(UIKeys.MapsTitle);

            if (open != null)
            {
                ShowRecordDetail(MapDetail(open), appTitle, RecordRef.ForRecord(open));

                bool period = open.kind == MapRecordKind.LocationHistory;
                ShowRecordPanel(MapTitle(open), UIKeys.RecordPrefixMap, open.Id, device, SourceText(open.source, null),
                    period ? UIKeys.PanelPeriod : UIKeys.PanelRecordTime,
                    period ? time.SinceDeathTimeRange(open.time, open.endTime) : time.SinceDeathWithTime(open.time),
                    open.integrityKey, RecordRef.ForRecord(open));
                return;
            }
            if (openPin != null)
            {
                ShowRecordDetail(new DetailModel
                {
                    title = text.Get(UIKeys.MapsSectionShared),
                    headline = PlaceLabel(openPin.message.AttachmentLabel),
                    hero = MapHero,
                    fields = new List<(string, string)>
                    {
                        (text.Get(UIKeys.MapsFieldShared), SharedLabel(openPin, device)),
                        (text.Get(UIKeys.FieldDateTime), PhoneDateTime(openPin.message.time)),
                    },
                }, appTitle, RecordRef.ForMessage(openPin.entry.device, openPin.entry.thread, openPin.message));
                ShowRecordPanel(PlaceLabel(openPin.message.AttachmentLabel), UIKeys.RecordPrefixMap, openPin.message.Id, device,
                    SourceText(openPin.entry.state.source, openPin.entry.state.serviceKey),
                    UIKeys.PanelRecordTime, time.SinceDeathWithTime(openPin.message.time), openPin.entry.state.integrityKey,
                    RecordRef.ForMessage(openPin.entry.device, openPin.entry.thread, openPin.message));
                return;
            }

            var items = new List<PhoneListView.Item>();
            void AddKind(MapRecordKind kind, Sprite icon)
            {
                var ofKind = RecordQuery.Newest(records).Where(r => r.kind == kind).ToList();
                if (ofKind.Count == 0)
                    return;
                items.Add(PhoneListView.Item.Section(MapSectionTitle(kind)));
                foreach (var r in ofKind)
                {
                    items.Add(new PhoneListView.Item
                    {
                        title = MapTitle(r),
                        subtitle = r.kind == MapRecordKind.Route ? r.RouteNote : null,
                        trailing = r.kind == MapRecordKind.LocationHistory ? MapTimeText(r) : time.ListLabel(r.time),
                        icon = icon,
                        selection = RecordRef.ForRecord(r),
                        onClick = () => OpenRecordDetail(AppKind.Maps, RecordRef.ForRecord(r)),
                    });
                }
            }
            AddKind(MapRecordKind.PlaceSearch, Theme.searchGlyph);
            AddKind(MapRecordKind.Route, Theme.routeGlyph);
            if (pins.Count > 0)
            {
                items.Add(PhoneListView.Item.Section(text.Get(UIKeys.MapsSectionShared)));
                foreach (var p in pins)
                {
                    items.Add(new PhoneListView.Item
                    {
                        title = PlaceLabel(p.message.AttachmentLabel),
                        subtitle = SharedLabel(p, device),
                        trailing = time.ListLabel(p.message.time),
                        icon = Theme.pinGlyph,
                        selection = RecordRef.ForMessage(p.entry.device, p.entry.thread, p.message),
                        onClick = () => OpenRecordDetail(AppKind.Maps, RecordRef.ForMessage(p.entry.device, p.entry.thread, p.message)),
                    });
                }
            }
            AddKind(MapRecordKind.LocationHistory, Theme.historyGlyph);
            ShowRecordList(nav, appTitle, null, items, null);
            ShowCollectionPanel(UIKeys.PanelTitleMaps, device, records.Count + pins.Count);
        }

        string MapSectionTitle(MapRecordKind kind)
        {
            switch (kind)
            {
                case MapRecordKind.Route: return text.Get(UIKeys.MapsSectionRoutes);
                case MapRecordKind.LocationHistory: return text.Get(UIKeys.MapsSectionHistory);
                default: return text.Get(UIKeys.MapsSectionSearches);
            }
        }

        string MapTitle(MapRecord r) =>
            r.kind == MapRecordKind.Route
                ? text.Format(UIKeys.MapsRouteFormat, ("from", PlaceLabel(r.RouteFrom)), ("to", PlaceLabel(r.RouteTo)))
                : PlaceLabel(r.PlaceLabel);

        /// <summary>장소명이 정해지지 않았으면 중립 표기를 쓴다.</summary>
        string PlaceLabel(string label) => string.IsNullOrEmpty(label) ? text.Get(UIKeys.MapsPlaceUnknown) : label;

        string MapTimeText(MapRecord r) =>
            r.kind == MapRecordKind.LocationHistory
                ? text.Format(UIKeys.MapsPeriodFormat, ("date", time.DateLabel(r.time)), ("start", time.Time(r.time)), ("end", time.Time(r.endTime)))
                : PhoneDateTime(r.time);

        string SharedLabel(SharedPin p, CaseData device) =>
            text.Format(p.message.sender == device.owner ? UIKeys.MapsSharedSent : UIKeys.MapsSharedReceived,
                ("name", p.entry.displayName));

        // ───────────── 파일 ─────────────

        void RenderFiles(CaseData device, DeviceNavigation nav)
        {
            var db = config.database;
            var locations = RecordQuery.FileLocations(db, device, session.Stage);
            if (nav.filesLocation.HasValue && !locations.Contains(nav.filesLocation.Value))
            {
                nav.filesLocation = null;
                nav.filesPath = string.Empty;
            }
            string appTitle = text.Get(UIKeys.FilesTitle);

            if (!nav.filesLocation.HasValue)
            {
                nav.SetOpenRecord(AppKind.Files, null);
                var roots = locations.Select(loc => new PhoneListView.Item
                {
                    title = text.Get(UIKeys.FilesLocationKey(loc)),
                    subtitle = ItemCount(RecordQuery.CountUnder(db, device, session.Stage, loc, string.Empty)),
                    icon = Theme.folderGlyph,
                    onClick = () => OpenFileLocation(loc),
                }).ToList();
                ShowRecordList(nav, appTitle, null, roots, null);
                ShowCollectionPanel(UIKeys.PanelTitleFiles, device, RecordQuery.For(db.files, device, session.Stage).Count);
                return;
            }

            var location = nav.filesLocation.Value;
            var contents = RecordQuery.Folder(db, device, session.Stage, location, nav.filesPath);
            string locationLabel = text.Get(UIKeys.FilesLocationKey(location));
            string folderTitle = nav.filesPath.Length == 0 ? locationLabel : RecordQuery.FolderName(nav.filesPath);
            var open = FindById(contents.files, nav.OpenRecord(AppKind.Files));
            nav.SetOpenRecord(AppKind.Files, open?.Id);

            if (open != null)
            {
                ShowRecordDetail(FileDetail(open), folderTitle, RecordRef.ForRecord(open));
                ShowRecordPanel(open, open.FileName, UIKeys.RecordPrefixFile, device);
                return;
            }

            string parent = RecordQuery.Parent(nav.filesPath);
            string backText = nav.filesPath.Length == 0 ? appTitle
                : parent.Length == 0 ? locationLabel : RecordQuery.FolderName(parent);
            var items = new List<PhoneListView.Item>();
            foreach (var folder in contents.subfolders)
            {
                string path = folder;
                items.Add(new PhoneListView.Item
                {
                    title = RecordQuery.FolderName(folder),
                    subtitle = ItemCount(RecordQuery.CountUnder(db, device, session.Stage, location, folder)),
                    icon = Theme.folderGlyph,
                    onClick = () => OpenFolder(path),
                });
            }
            foreach (var f in contents.files)
            {
                items.Add(new PhoneListView.Item
                {
                    title = f.FileName,
                    subtitle = PhoneDateTime(f.time),
                    icon = IsImageFile(f.FileName) ? Theme.photoGlyph : Theme.documentGlyph,
                    selection = RecordRef.ForRecord(f),
                    onClick = () => OpenRecordDetail(AppKind.Files, RecordRef.ForRecord(f)),
                });
            }
            ShowRecordList(nav, folderTitle, backText, items, Back);
            ShowCollectionPanel(UIKeys.PanelTitleFiles, device, contents.files.Count);
        }

        string ItemCount(int n) => text.FormatCount(UIKeys.FilesItemCount, n, ("count", n.ToString(time.Culture)));

        static bool IsImageFile(string name)
        {
            string ext = System.IO.Path.GetExtension(name ?? string.Empty).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".heic" || ext == ".gif";
        }

        void OpenFileLocation(RecordSource location)
        {
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            nav.filesLocation = location;
            nav.filesPath = string.Empty;
            nav.scroll.Remove(nav.ListScrollKey(AppKind.Files));
            RenderCenter();
        }

        void OpenFolder(string path)
        {
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            nav.filesPath = path;
            nav.scroll.Remove(nav.ListScrollKey(AppKind.Files));
            RenderCenter();
        }

        // ───────────── 설정 ─────────────

        void RenderSettings(CaseData device, DeviceNavigation nav)
        {
            var records = RecordQuery.For(config.database.settings, device, session.Stage);
            var open = FindById(records, nav.OpenRecord(AppKind.Settings));
            nav.SetOpenRecord(AppKind.Settings, open?.Id);
            string appTitle = text.Get(UIKeys.SettingsTitle);

            if (open != null)
            {
                var detail = SettingDetail(open, device);
                ShowRecordDetail(detail, appTitle, RecordRef.ForRecord(open));
                ShowRecordPanel(open, detail.headline, UIKeys.RecordPrefixSetting, device);
                return;
            }

            var items = new List<PhoneListView.Item>();
            var current = RecordQuery.CurrentSettings(records);
            if (current.Count > 0)
            {
                items.Add(PhoneListView.Item.Section(text.Get(UIKeys.SettingsSectionCurrent)));
                foreach (var s in current)
                {
                    items.Add(new PhoneListView.Item
                    {
                        title = RecordQuery.SettingItemLabel(s, device),
                        subtitle = s.CategoryLabel,
                        trailing = s.ValueAfter,
                        selection = RecordRef.ForRecord(s),
                        onClick = () => OpenRecordDetail(AppKind.Settings, RecordRef.ForRecord(s)),
                    });
                }
            }
            if (records.Count > 0)
            {
                items.Add(PhoneListView.Item.Section(text.Get(UIKeys.SettingsSectionHistory)));
                foreach (var s in RecordQuery.Newest(records))
                {
                    items.Add(new PhoneListView.Item
                    {
                        title = RecordQuery.SettingItemLabel(s, device),
                        subtitle = text.Format(UIKeys.SettingsChangeFormat, ("before", s.ValueBefore), ("after", s.ValueAfter)),
                        trailing = time.ListLabel(s.time),
                        selection = RecordRef.ForRecord(s),
                        onClick = () => OpenRecordDetail(AppKind.Settings, RecordRef.ForRecord(s)),
                    });
                }
            }
            ShowRecordList(nav, appTitle, null, items, null);
            ShowCollectionPanel(UIKeys.PanelTitleSettings, device, records.Count);
        }

        // ───────────── 메일 ─────────────

        /// <summary>
        /// 읽기 전용 메일 앱: 메일함 구역별 목록(최신이 위) → 상세 → 뒤로. 작성·답장·전달·삭제·첨부 내려받기는 없다.
        /// 보이는 문구는 모두 데이터의 표시용 접근자(현재 언어)와 문자열 테이블에서 온다.
        /// </summary>
        void RenderMail(CaseData device, DeviceNavigation nav)
        {
            var records = RecordQuery.For(config.database.mails, device, session.Stage);
            var open = FindById(records, nav.OpenRecord(AppKind.Mail));
            nav.SetOpenRecord(AppKind.Mail, open?.Id);
            string appTitle = text.Get(UIKeys.MailTitle);

            if (open != null)
            {
                ShowRecordDetail(MailDetail(open), appTitle, RecordRef.ForRecord(open));
                ShowRecordPanel(open, RecordResolver.MailSubject(open, text), UIKeys.RecordPrefixMail, device);
                return;
            }

            var items = new List<PhoneListView.Item>();
            // 계정 줄은 의뢰 데이터에 키가 있을 때만(예: 연결 해제된 계정의 복구 메일함). 계정 주소·소유자는 추정하지 않는다.
            if (!string.IsNullOrEmpty(device.mailAccountKey) && records.Count > 0)
                items.Add(PhoneListView.Item.Note(text.Get(device.mailAccountKey)));
            var order = UIKeys.MailboxOrder.ToList();
            foreach (var box in records.Select(r => r.mailboxKey ?? string.Empty).Distinct()
                         .OrderBy(k => order.IndexOf(k) < 0 ? int.MaxValue : order.IndexOf(k)).ThenBy(k => k, StringComparer.Ordinal))
            {
                items.Add(PhoneListView.Item.Section(MailboxName(box)));
                foreach (var m in RecordQuery.Newest(records).Where(r => (r.mailboxKey ?? string.Empty) == box))
                {
                    items.Add(new PhoneListView.Item
                    {
                        title = MailSender(m),
                        subtitle = RecordResolver.MailSubject(m, text),
                        trailing = time.ListLabel(m.time),
                        icon = Theme.documentGlyph,
                        selection = RecordRef.ForRecord(m),
                        onClick = () => OpenRecordDetail(AppKind.Mail, RecordRef.ForRecord(m)),
                    });
                }
            }
            ShowRecordList(nav, appTitle, null, items, null, UIKeys.MailEmpty);
            ShowCollectionPanel(UIKeys.PanelTitleMail, device, records.Count);
        }

        string MailboxName(string key) => string.IsNullOrEmpty(key) ? text.Get(UIKeys.MailBoxInbox) : text.Get(key);

        /// <summary>목록의 보낸 사람: 표시명, 없으면 주소, 둘 다 없으면 중립 표기.</summary>
        string MailSender(MailRecord m) =>
            !string.IsNullOrEmpty(m.FromName) ? m.FromName
            : !string.IsNullOrEmpty(m.fromAddress) ? m.fromAddress
            : text.Get(UIKeys.MailFromUnknown);

        /// <summary>상세의 보낸 사람: 표시명과 주소가 모두 있으면 함께 보여준다(주소는 원문 그대로).</summary>
        string MailSenderFull(MailRecord m) =>
            !string.IsNullOrEmpty(m.FromName) && !string.IsNullOrEmpty(m.fromAddress)
                ? text.Format(UIKeys.MailFromFormat, ("name", m.FromName), ("address", m.fromAddress))
                : MailSender(m);

        string MailRecipient(MailRecord m) =>
            !string.IsNullOrEmpty(m.ToText) ? m.ToText
            : !string.IsNullOrEmpty(m.toKey) ? text.Get(m.toKey)
            : text.Get(UIKeys.MailToNone);

        /// <summary>
        /// 메일 상세 (휴대전화와 비교 화면이 같은 표현을 쓴다).
        /// 순서: 제목 → 보낸 사람(표시명·전체 주소) → 받는 사람·일시·상태 → 첨부 → 본문. 주소·첨부는 말줄임 없이 줄바꿈한다.
        /// 본문이 없으면 본문 자리에 중립 문구를 둔다.
        /// </summary>
        DetailModel MailDetail(MailRecord m)
        {
            var body = m.Body;
            var fields = new List<(string, string)>
            {
                (text.Get(UIKeys.MailFieldFrom), MailSenderFull(m)),
                (text.Get(UIKeys.MailFieldTo), MailRecipient(m)),
                (text.Get(UIKeys.FieldDateTime), PhoneDateTime(m.time)),
            };
            if (!string.IsNullOrEmpty(m.statusKey))
                fields.Add((text.Get(UIKeys.MailFieldStatus), text.Get(m.statusKey)));
            var attachments = m.AttachmentLines.ToList();
            if (attachments.Count > 0)
                fields.Add((text.Get(UIKeys.MailFieldAttachments), string.Join("\n", attachments)));
            return new DetailModel
            {
                title = MailboxName(m.mailboxKey),
                headline = RecordResolver.MailSubject(m, text),
                document = body.Count > 0 ? body
                    : new List<FileTextBlock> { new FileTextBlock { text = text.Get(UIKeys.MailBodyEmpty) } },
                fields = fields,
                documentLast = true,
                wrapFields = true,
            };
        }

        // ───────────── 탐색 ─────────────

        /// <summary>고르기 모드면 그 기록을 고르고, 아니면 상세 화면을 연다. 열린 기록은 불변 ID로 기억한다.</summary>
        void OpenRecordDetail(AppKind app, RecordRef record)
        {
            if (TrySelectInstead(record))
                return;
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            nav.SetOpenRecord(app, record.recordId);
            RenderCenter();
        }

        /// <summary>상세 → 목록, 파일 앱은 하위 폴더 → 상위 폴더 → 위치 목록 순으로 되돌아간다.</summary>
        void BackRecords(DeviceNavigation nav)
        {
            if (nav.OpenRecord(nav.app) != null)
            {
                nav.SetOpenRecord(nav.app, null);
                RenderCenter();
                return;
            }
            if (nav.app != AppKind.Files || !nav.filesLocation.HasValue)
                return;
            SaveScroll();
            if (nav.filesPath.Length == 0)
                nav.filesLocation = null;
            else
                nav.filesPath = RecordQuery.Parent(nav.filesPath);
            RenderCenter();
        }

        int RecordAppCount(AppKind kind, CaseData device)
        {
            var db = config.database;
            switch (kind)
            {
                case AppKind.Browser: return RecordQuery.For(db.browser, device, session.Stage).Count;
                case AppKind.Maps:
                    return RecordQuery.For(db.maps, device, session.Stage).Count
                        + RecordQuery.SharedPins(db, device, session.Stage).Count;
                case AppKind.Files: return RecordQuery.For(db.files, device, session.Stage).Count;
                case AppKind.Settings: return RecordQuery.For(db.settings, device, session.Stage).Count;
                case AppKind.Mail: return RecordQuery.For(db.mails, device, session.Stage).Count;
                default: return 0;
            }
        }
    }
}
