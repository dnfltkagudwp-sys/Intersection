using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>RecordRef를 원본 데이터에서 찾은 결과. 업무 패널 표시에 필요한 값만 담는다.</summary>
    public class ResolvedRecord
    {
        public bool found;
        /// <summary>같은 ID가 여러 원본에 있어 대상을 하나로 정할 수 없음.</summary>
        public bool duplicate;
        public CaseData device;
        public string title;
        public string body;
        public string prefixKey;
        public RecordSource source;
        public string serviceKey;
        public bool hasTime;
        public RelativeTime time;
        public bool hasEnd;
        public RelativeTime endTime;
        public string integrityKey;
    }

    /// <summary>
    /// 저장된 참조를 원본 데이터에서 정확히 같은 ID로만 찾는다.
    /// 없거나 중복이면 found=false로 돌려주며, 다른 기록으로 대체하지 않는다.
    /// </summary>
    public static class RecordResolver
    {
        public static ResolvedRecord Resolve(RecordRef r, ContentDatabase db, UIText text)
        {
            var result = new ResolvedRecord { prefixKey = PrefixFor(r.kind) };
            if (!r.IsKnownKind)
                return Missing(result);
            result.device = Single(db.cases.Where(c => c != null && c.Id == r.deviceId), result);
            if (result.device == null)
                return result;

            switch (r.kind)
            {
                case RecordKind.Thread:
                {
                    var t = Single(db.threads.Where(x => x != null && x.Id == r.recordId), result);
                    var state = t?.StateFor(result.device);
                    if (state == null)
                        return Missing(result);
                    var ordered = DeviceQuery.Ordered(t);
                    Fill(result, text.Format(UIKeys.PanelTitleThread, ("name", DeviceQuery.DisplayName(t, result.device))),
                        state.source, state.serviceKey, state.integrityKey);
                    if (ordered.Count > 0)
                        SetTime(result, ordered[0].time, ordered[ordered.Count - 1].time);
                    return result;
                }
                case RecordKind.Message:
                case RecordKind.Attachment:
                case RecordKind.LocationShare:
                {
                    var t = Single(db.threads.Where(x => x != null && x.Id == r.threadId), result);
                    var state = t?.StateFor(result.device);
                    var m = t == null ? null : Single(t.AllMessages.Where(x => x.Id == r.recordId), result);
                    if (state == null || m == null)
                        return Missing(result);
                    string title = r.kind == RecordKind.LocationShare
                        ? (string.IsNullOrEmpty(m.attachmentLabel) ? text.Get(UIKeys.MapsPlaceUnknown) : m.attachmentLabel)
                        : DeviceQuery.DisplayText(m, text).Replace('\n', ' ');
                    Fill(result, title, state.source, state.serviceKey, state.integrityKey);
                    result.body = text.Format(UIKeys.SelectionMessageContext, ("name", DeviceQuery.DisplayName(t, result.device)));
                    SetTime(result, m.time, null);
                    return result;
                }
                case RecordKind.Photo:
                {
                    var p = Single(db.photos.Where(x => x != null && x.Id == r.recordId && x.device == result.device), result);
                    if (p == null)
                        return Missing(result);
                    Fill(result, p.fileName, p.source, p.serviceKey, p.integrityKey);
                    SetTime(result, p.takenAt, null);
                    return result;
                }
                case RecordKind.Browser:
                    return FromRecord(Single(db.browser.Where(x => x != null && x.Id == r.recordId && x.device == result.device), result),
                        result, b => b.title);
                case RecordKind.Map:
                {
                    var m = Single(db.maps.Where(x => x != null && x.Id == r.recordId && x.device == result.device), result);
                    var res = FromRecord(m, result, x => MapTitle(x, text));
                    if (m != null && m.kind == MapRecordKind.LocationHistory)
                        SetTime(res, m.time, m.endTime);
                    return res;
                }
                case RecordKind.File:
                    return FromRecord(Single(db.files.Where(x => x != null && x.Id == r.recordId && x.device == result.device), result),
                        result, f => f.FileName);
                case RecordKind.Setting:
                    return FromRecord(Single(db.settings.Where(x => x != null && x.Id == r.recordId && x.device == result.device), result),
                        result, s => RecordQuery.SettingItemLabel(s, result.device));
                case RecordKind.Request:
                {
                    var q = Single(db.requests.Where(x => x != null && x.Id == r.recordId && x.device == result.device), result);
                    if (q == null)
                        return Missing(result);
                    Fill(result, q.title, RecordSource.Local, null, null);
                    result.body = q.body;
                    return result;
                }
                default:
                    return Missing(result);
            }
        }

        public static string MapTitle(MapRecord r, UIText text)
        {
            string Place(string s) => string.IsNullOrEmpty(s) ? text.Get(UIKeys.MapsPlaceUnknown) : s;
            return r.kind == MapRecordKind.Route
                ? text.Format(UIKeys.MapsRouteFormat, ("from", Place(r.routeFrom)), ("to", Place(r.routeTo)))
                : Place(r.placeLabel);
        }

        public static string PrefixFor(RecordKind kind)
        {
            switch (kind)
            {
                case RecordKind.Photo: return UIKeys.RecordPrefixPhoto;
                case RecordKind.Browser: return UIKeys.RecordPrefixWeb;
                case RecordKind.Map:
                case RecordKind.LocationShare: return UIKeys.RecordPrefixMap;
                case RecordKind.File: return UIKeys.RecordPrefixFile;
                case RecordKind.Setting: return UIKeys.RecordPrefixSetting;
                case RecordKind.Request: return UIKeys.RecordPrefixRequest;
                default: return UIKeys.RecordPrefixThread;
            }
        }

        static ResolvedRecord FromRecord<T>(T record, ResolvedRecord result, System.Func<T, string> title) where T : DeviceRecord
        {
            if (record == null)
                return Missing(result);
            Fill(result, title(record), record.source, null, record.integrityKey);
            SetTime(result, record.time, null);
            return result;
        }

        static void Fill(ResolvedRecord r, string title, RecordSource source, string serviceKey, string integrityKey)
        {
            r.found = true;
            r.title = title;
            r.source = source;
            r.serviceKey = serviceKey;
            r.integrityKey = integrityKey;
        }

        static void SetTime(ResolvedRecord r, RelativeTime start, RelativeTime? end)
        {
            r.hasTime = true;
            r.time = start;
            r.hasEnd = end.HasValue;
            if (end.HasValue)
                r.endTime = end.Value;
        }

        static ResolvedRecord Missing(ResolvedRecord r)
        {
            r.found = false;
            return r;
        }

        /// <summary>정확히 하나일 때만 돌려준다. 둘 이상이면 중복으로 표시하고 null.</summary>
        static T Single<T>(IEnumerable<T> matches, ResolvedRecord result) where T : class
        {
            T first = null;
            foreach (var m in matches)
            {
                if (first != null)
                {
                    result.duplicate = true;
                    return null;
                }
                first = m;
            }
            return first;
        }
    }
}
