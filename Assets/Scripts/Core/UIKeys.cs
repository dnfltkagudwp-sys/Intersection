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

        // 우측 업무 기록
        public const string PanelTitleMessageList = "panel.title.messageList";
        public const string PanelTitleThread = "panel.title.thread";
        public const string PanelRecordId = "panel.row.recordId";
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
            PanelTitleMessageList, PanelTitleThread, PanelRecordId, PanelOwner, PanelSource, PanelSourceFormat,
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
