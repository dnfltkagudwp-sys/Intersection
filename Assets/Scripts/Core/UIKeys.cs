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
