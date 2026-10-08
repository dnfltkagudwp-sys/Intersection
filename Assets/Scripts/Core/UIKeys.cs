namespace Intersection.Core
{
    /// <summary>
    /// 코드가 사용하는 문자열 키 목록. 문구 자체는 StringTable 에셋에 있다.
    /// 검증 도구가 이 목록의 키가 모두 테이블에 있는지 검사한다.
    /// </summary>
    public static class UIKeys
    {
        // 날짜·시각 형식 (.NET 사용자 지정 형식)
        public const string FormatTime = "format.time";
        public const string FormatStatusClock = "format.statusClock";
        public const string FormatMonthDay = "format.monthDay";
        public const string FormatFullDate = "format.fullDate";
        public const string FormatWeekday = "format.weekday";
        public const string DateToday = "date.today";
        public const string DateYesterday = "date.yesterday";
        public const string SeparatorTemplate = "chat.separator";

        // 사망일 기준 표기
        public const string DeathBefore = "death.before";
        public const string DeathDayOf = "death.dayOf";
        public const string DeathAfter = "death.after";
        public const string DeathWithTime = "death.withTime";
        public const string DeathRange = "death.range";

        // 상단 바
        public const string TopCaseStatus = "top.caseStatus";
        public const string TopIndexDone = "top.index.done";
        public const string TopIndexRunning = "top.index.running";

        // 좌측
        public const string CaseStatusLocal = "case.status.local";
        public const string CaseStatusWaiting = "case.status.waiting";

        // 중앙 툴바
        public const string PhoneDeviceLabel = "phone.deviceLabel";

        // 메시지 앱
        public const string PreviewImage = "preview.image";
        public const string PreviewLocation = "preview.location";
        public const string ChatBack = "chat.back";
        public const string ChatBackUnread = "chat.backUnread";
        public const string AttachmentImage = "chat.attachment.image";
        public const string AttachmentLocation = "chat.attachment.location";
        public const string MessagesEmpty = "messages.empty";
        public const string SearchSectionThreads = "search.section.threads";
        public const string SearchSectionMessages = "search.section.messages";
        public const string SearchEmpty = "search.empty";

        // 사진 앱
        public const string PhotosAlbumCount = "photos.albumCount";
        public const string PhotosBack = "photos.back";
        public const string PhotosEmpty = "photos.empty";
        public const string PanelTitleAlbums = "panel.title.albums";
        public const string PanelPhotoCount = "panel.row.photoCount";
        public const string PanelPhotoCountValue = "panel.photoCountValue";
        public const string PanelTakenAt = "panel.row.takenAt";
        public const string RecordPrefixPhoto = "record.prefix.photo";

        // UI-04 공통
        public const string DeathTimeRange = "death.timeRange";
        public const string ListEmpty = "list.empty";
        public const string FieldDateTime = "field.datetime";
        public const string FieldKind = "field.kind";
        public const string PanelRecordTime = "panel.row.time";
        public const string PanelRecordCount = "panel.row.recordCount";
        public const string PanelRecordCountValue = "panel.recordCountValue";

        // 브라우저
        public const string BrowserTitle = "app.browser.title";
        public const string BrowserKindSearch = "browser.kind.search";
        public const string BrowserKindVisit = "browser.kind.visit";
        public const string BrowserPageUnavailable = "browser.pageUnavailable";
        public const string PanelTitleBrowser = "panel.title.browser";
        public const string RecordPrefixWeb = "record.prefix.web";

        // 지도
        public const string MapsTitle = "app.maps.title";
        public const string MapsSectionSearches = "maps.section.searches";
        public const string MapsSectionRoutes = "maps.section.routes";
        public const string MapsSectionShared = "maps.section.shared";
        public const string MapsSectionHistory = "maps.section.history";
        public const string MapsRouteFormat = "maps.routeFormat";
        public const string MapsSharedSent = "maps.sharedSent";
        public const string MapsSharedReceived = "maps.sharedReceived";
        public const string MapsPlaceUnknown = "maps.placeUnknown";
        public const string MapsFieldPlace = "maps.field.place";
        public const string MapsFieldFrom = "maps.field.from";
        public const string MapsFieldTo = "maps.field.to";
        public const string MapsFieldVia = "maps.field.via";
        public const string MapsFieldShared = "maps.field.shared";
        public const string MapsPeriodFormat = "maps.periodFormat";
        public const string PanelTitleMaps = "panel.title.maps";
        public const string RecordPrefixMap = "record.prefix.map";

        // 파일
        public const string FilesTitle = "app.files.title";
        public const string FilesLocationLocal = "files.location.local";
        public const string FilesLocationCloud = "files.location.cloud";
        public const string FilesLocationLinked = "files.location.linked";
        public const string FilesLocationRecovered = "files.location.recovered";
        public const string FilesItemCount = "files.itemCount";
        public const string FilesPreviewUnavailable = "files.previewUnavailable";
        public const string FilesFieldName = "files.field.name";
        public const string FilesFieldModified = "files.field.modified";
        public const string FilesFieldWhere = "files.field.where";
        public const string PanelTitleFiles = "panel.title.files";
        public const string RecordPrefixFile = "record.prefix.file";

        // 설정
        public const string SettingsTitle = "app.settings.title";
        public const string SettingsSectionCurrent = "settings.section.current";
        public const string SettingsSectionHistory = "settings.section.history";
        public const string SettingsChangeFormat = "settings.changeFormat";
        public const string SettingsFieldItem = "settings.field.item";
        public const string SettingsFieldBefore = "settings.field.before";
        public const string SettingsFieldAfter = "settings.field.after";
        public const string PanelTitleSettings = "panel.title.settings";
        public const string RecordPrefixSetting = "record.prefix.setting";

        // UI-05 기록 고르기·핀·처리 후보·작업 기록
        public const string ToolbarSelectStart = "toolbar.selectRecord";
        public const string ToolbarSelectEnd = "toolbar.selectEnd";
        public const string ToolbarPickHint = "toolbar.pickHint";
        public const string ToolbarPicked = "toolbar.picked";
        public const string PanelHeadingCurrent = "panel.current";
        public const string PanelHeadingPicked = "panel.heading.picked";
        public const string PanelHeadingWork = "panel.heading.work";
        public const string PanelHeadingRequest = "panel.heading.request";
        public const string ActionPin = "panel.action.pin";
        public const string ActionUnpin = "panel.action.unpin";
        public const string PanelViewed = "panel.row.viewed";
        public const string ViewedYes = "panel.viewed.yes";
        public const string ViewedNo = "panel.viewed.no";
        public const string ClassUnclassified = "class.unclassified";
        public const string ClassKeep = "class.keep";
        public const string ClassDelete = "class.delete";
        public const string PanelMissing = "panel.missing";
        public const string PanelMissingDuplicate = "panel.missingDuplicate";
        public const string PanelMissingBody = "panel.missingBody";
        public const string WorkItemMeta = "panel.pinnedMeta";
        public const string PanelWorkListHeader = "panel.workList";
        public const string PanelWorkEmpty = "panel.workEmpty";
        public const string PanelWorkCollapse = "panel.workCollapse";
        public const string PanelWorkExpand = "panel.workExpand";
        public const string SelectionMessageContext = "selection.messageContext";
        public const string ActionOpenSource = "panel.openSource";
        public const string ActionOpenSourceMessage = "panel.openSourceMessage";
        public const string SourceMissing = "panel.sourceMissing";
        public const string SourceDuplicate = "panel.sourceDuplicate";
        public const string SourceUnavailable = "panel.sourceUnavailable";

        // UI-06 비교
        public const string ActionCompareAdd = "panel.compareAdd";
        public const string ActionCompareRemove = "panel.compareRemove";
        public const string CompareFull = "panel.compareFull";
        public const string CompareBlockedMissing = "panel.compareMissing";
        public const string CompareBlockedDuplicate = "panel.compareDuplicate";
        public const string CompareBlockedUnavailable = "panel.compareUnavailable";
        public const string CompareSlotsHeader = "panel.compareSlots";
        public const string CompareSlotsOneMore = "panel.compareSlotsOneMore";
        public const string CompareSlotsReady = "panel.compareSlotsReady";
        public const string CompareSlotsFullNote = "panel.compareSlotsFullNote";
        public const string ListCompareAdd = "list.compareAdd";
        public const string ListCompareOn = "list.compareOn";
        public const string TutorialDoneLine = "tutorial.doneLine";
        public const string CompareSlotEmpty = "panel.compareSlotEmpty";
        public const string CompareSlotRemove = "panel.compareSlotRemove";
        public const string CompareRowKind = "compare.row.kind";
        public const string CompareKindFormat = "compare.kindFormat";
        public const string CompareAppWork = "compare.app.work";

        // 툴바 "○○ 선택됨"에 쓰는 기록 종류 이름
        public const string KindThread = "kind.thread";
        public const string KindMessage = "kind.message";
        public const string KindAttachment = "kind.attachment";
        public const string KindLocationShare = "kind.locationShare";
        public const string KindPhoto = "kind.photo";
        public const string KindBrowser = "kind.browser";
        public const string KindMap = "kind.map";
        public const string KindFile = "kind.file";
        public const string KindSetting = "kind.setting";
        public const string KindRequest = "kind.request";

        public static string KindKey(RecordKind kind)
        {
            switch (kind)
            {
                case RecordKind.Thread: return KindThread;
                case RecordKind.Attachment: return KindAttachment;
                case RecordKind.LocationShare: return KindLocationShare;
                case RecordKind.Photo: return KindPhoto;
                case RecordKind.Browser: return KindBrowser;
                case RecordKind.Map: return KindMap;
                case RecordKind.File: return KindFile;
                case RecordKind.Setting: return KindSetting;
                case RecordKind.Request: return KindRequest;
                default: return KindMessage;
            }
        }
        public const string SideRequests = "side.requests";
        public const string RecordPrefixRequest = "record.prefix.request";
        public const string TutorialDone = "tutorial.status.done";
        public const string TutorialActive = "tutorial.status.active";
        public const string TutorialWaiting = "tutorial.status.waiting";
        public const string TutorialLine = "tutorial.line";

        public static string ClassKey(Classification c) =>
            c == Classification.Keep ? ClassKeep : c == Classification.Delete ? ClassDelete : ClassUnclassified;

        public static string FilesLocationKey(Intersection.Data.RecordSource source)
        {
            switch (source)
            {
                case Intersection.Data.RecordSource.Cloud: return FilesLocationCloud;
                case Intersection.Data.RecordSource.Linked: return FilesLocationLinked;
                case Intersection.Data.RecordSource.Recovered: return FilesLocationRecovered;
                default: return FilesLocationLocal;
            }
        }

        // 우측 업무 기록
        public const string PanelTitleMessageList = "panel.title.messageList";
        public const string PanelTitleThread = "panel.title.thread";
        public const string PanelRecordId = "panel.row.recordId";
        public const string RecordCodeFormat = "record.codeFormat";
        public const string RecordPrefixThread = "record.prefix.thread";
        public const string PanelOwner = "panel.row.owner";
        public const string PanelSource = "panel.row.source";
        public const string PanelSourceFormat = "panel.sourceFormat";
        public const string PanelPeriod = "panel.row.period";
        public const string PanelIntegrity = "panel.row.integrity";
        public const string PanelThreadCount = "panel.row.threadCount";
        public const string PanelThreadCountValue = "panel.threadCountValue";

        // 출처
        public const string SourceLocal = "source.local";
        public const string SourceCloud = "source.cloud";
        public const string SourceLinked = "source.linked";
        public const string SourceRecovered = "source.recovered";

        public static readonly string[] All =
        {
            FormatTime, FormatStatusClock, FormatMonthDay, FormatFullDate, FormatWeekday,
            DateToday, DateYesterday, SeparatorTemplate,
            DeathBefore, DeathDayOf, DeathAfter, DeathWithTime, DeathRange,
            TopCaseStatus, TopIndexDone, TopIndexRunning,
            CaseStatusLocal, CaseStatusWaiting,
            PhoneDeviceLabel,
            PreviewImage, PreviewLocation, ChatBack, ChatBackUnread, AttachmentImage, AttachmentLocation, MessagesEmpty,
            SearchSectionThreads, SearchSectionMessages, SearchEmpty,
            PhotosAlbumCount, PhotosBack, PhotosEmpty, PanelTitleAlbums, PanelPhotoCount, PanelPhotoCountValue, PanelTakenAt,
            RecordPrefixPhoto,
            DeathTimeRange, ListEmpty, FieldDateTime, FieldKind, PanelRecordTime, PanelRecordCount, PanelRecordCountValue,
            BrowserTitle, BrowserKindSearch, BrowserKindVisit, BrowserPageUnavailable, PanelTitleBrowser, RecordPrefixWeb,
            MapsTitle, MapsSectionSearches, MapsSectionRoutes, MapsSectionShared, MapsSectionHistory, MapsRouteFormat,
            MapsSharedSent, MapsSharedReceived, MapsPlaceUnknown, MapsFieldPlace, MapsFieldFrom, MapsFieldTo, MapsFieldVia,
            MapsFieldShared, MapsPeriodFormat, PanelTitleMaps, RecordPrefixMap,
            FilesTitle, FilesLocationLocal, FilesLocationCloud, FilesLocationLinked, FilesLocationRecovered, FilesItemCount,
            FilesPreviewUnavailable, FilesFieldName, FilesFieldModified, FilesFieldWhere, PanelTitleFiles, RecordPrefixFile,
            SettingsTitle, SettingsSectionCurrent, SettingsSectionHistory, SettingsChangeFormat, SettingsFieldItem,
            SettingsFieldBefore, SettingsFieldAfter, PanelTitleSettings, RecordPrefixSetting,
            ToolbarSelectStart, ToolbarSelectEnd, ToolbarPickHint, ToolbarPicked,
            PanelHeadingCurrent, PanelHeadingPicked, PanelHeadingWork, PanelHeadingRequest, ActionPin, ActionUnpin,
            PanelViewed, ViewedYes, ViewedNo, ClassUnclassified, ClassKeep, ClassDelete,
            PanelMissing, PanelMissingDuplicate, PanelMissingBody, WorkItemMeta,
            PanelWorkListHeader, PanelWorkEmpty, PanelWorkCollapse, PanelWorkExpand, SelectionMessageContext, SideRequests,
            ActionOpenSource, ActionOpenSourceMessage, SourceMissing, SourceDuplicate, SourceUnavailable,
            ActionCompareAdd, ActionCompareRemove, CompareFull, CompareBlockedMissing, CompareBlockedDuplicate,
            CompareBlockedUnavailable, CompareSlotsHeader, CompareSlotsOneMore, CompareSlotsReady, CompareSlotsFullNote,
            ListCompareAdd, ListCompareOn, TutorialDoneLine, CompareSlotEmpty, CompareSlotRemove, CompareRowKind,
            CompareKindFormat, CompareAppWork,
            KindThread, KindMessage, KindAttachment, KindLocationShare, KindPhoto, KindBrowser, KindMap, KindFile,
            KindSetting, KindRequest,
            RecordPrefixRequest, TutorialDone, TutorialActive, TutorialWaiting, TutorialLine,
            PanelTitleMessageList, PanelTitleThread, PanelRecordId, RecordCodeFormat, RecordPrefixThread, PanelOwner, PanelSource, PanelSourceFormat,
            PanelPeriod, PanelIntegrity, PanelThreadCount, PanelThreadCountValue,
            SourceLocal, SourceCloud, SourceLinked, SourceRecovered,
        };

        public static string SourceKey(Intersection.Data.RecordSource source)
        {
            switch (source)
            {
                case Intersection.Data.RecordSource.Cloud: return SourceCloud;
                case Intersection.Data.RecordSource.Linked: return SourceLinked;
                case Intersection.Data.RecordSource.Recovered: return SourceRecovered;
                default: return SourceLocal;
            }
        }
    }
}
