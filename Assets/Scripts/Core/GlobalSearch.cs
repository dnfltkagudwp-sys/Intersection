using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>전역 검색 결과 한 건. 표시 정보와 원본을 여는 불변 참조만 담는다(증거 ID·판정 없음).</summary>
    public class SearchHit
    {
        public RecordRef target;
        public CaseData device;
        /// <summary>원래 앱. 의뢰 요청은 휴대전화 앱이 아니라 null.</summary>
        public AppKind? app;
        public string title;
        /// <summary>검색어와 일치한 표시 문구 (제목·본문·날짜 등 화면에 보이는 값 그대로).</summary>
        public string matchText;
        public int matchIndex;
        public int matchLength;
        public RecordSource source;
        public string serviceKey;
        public bool hasTime;
        public RelativeTime time;
        public int sortKey;
    }

    /// <summary>
    /// 상단 전역 검색. 현재 단계에서 접근 가능하고 인덱싱이 끝난 모든 의뢰의 원본 기록을 매번 현재 데이터에서 다시 계산한다.
    /// 검색 대상은 화면에 실제로 보이는 값(대화 상대·대화방 제목·말풍선 문구·파일명·브라우저 제목·장소명·설정 값·요청 본문·
    /// caseDate 기준 표시 날짜·"사망 N일 전")뿐이다. 증거 참조·작가 메모·내부 ID·제작용 표기·작업메모는 넣지 않는다.
    /// 검색은 새 자료를 만들거나 접근 단계를 바꾸지 않는다.
    /// </summary>
    public static class GlobalSearch
    {
        class Doc
        {
            public SearchHit hit;
            public IEnumerable<string> fields;
        }

        public static List<SearchHit> Run(ContentDatabase db, ProgressStage stage, string query, UIText text, PhoneTime time)
        {
            var result = new List<SearchHit>();
            string q = (query ?? string.Empty).Trim();
            if (q.Length == 0 || db == null)
                return result;
            foreach (var doc in Documents(db, stage, text, time))
            {
                foreach (var field in doc.fields)
                {
                    if (!Find(field, q, out int index, out int length))
                        continue;
                    doc.hit.matchText = field;
                    doc.hit.matchIndex = index;
                    doc.hit.matchLength = length;
                    result.Add(doc.hit);
                    break;
                }
            }
            return result;
        }

        /// <summary>검색할 수 있는 모든 기록 (검증 도구가 접근 판정과 대조할 때도 쓴다).</summary>
        public static IEnumerable<SearchHit> Searchable(ContentDatabase db, ProgressStage stage, UIText text, PhoneTime time) =>
            Documents(db, stage, text, time).Select(d => d.hit);

        static IEnumerable<Doc> Documents(ContentDatabase db, ProgressStage stage, UIText text, PhoneTime time)
        {
            foreach (var device in db.cases.Where(c => RecordAccess.Searchable(c, stage)).OrderBy(c => c.order))
            {
                foreach (var q in db.requests.Where(x => x != null && x.device == device && stage >= x.availableFrom).OrderBy(x => x.order))
                {
                    yield return new Doc
                    {
                        hit = Hit(RecordRef.ForRequest(q), device, null, q.title, RecordSource.Local, null, null, q.order),
                        fields = new[] { q.title, q.body },
                    };
                }

                foreach (var entry in DeviceQuery.Threads(db, device, stage))
                {
                    var thread = entry.thread;
                    yield return new Doc
                    {
                        hit = Hit(RecordRef.ForThread(device, thread), device, AppKind.Messages, entry.displayName,
                            entry.state.source, entry.state.serviceKey, entry.last.time, entry.last.sortKey),
                        fields = new[] { entry.displayName }.Concat(DeviceQuery.ParticipantNames(thread, device)),
                    };
                    foreach (var m in DeviceQuery.Ordered(thread))
                    {
                        var r = RecordRef.ForMessage(device, thread, m);
                        string title = r.kind == RecordKind.LocationShare
                            ? (string.IsNullOrEmpty(m.attachmentLabel) ? text.Get(UIKeys.MapsPlaceUnknown) : m.attachmentLabel)
                            : entry.displayName;
                        yield return new Doc
                        {
                            hit = Hit(r, device, AppKind.Messages, title, entry.state.source, entry.state.serviceKey, m.time, m.sortKey),
                            fields = new[] { DeviceQuery.DisplayText(m, text) }.Concat(Dates(m.time, time)),
                        };
                    }
                }

                foreach (var p in PhotoQuery.DevicePhotos(db, device, stage))
                {
                    yield return new Doc
                    {
                        hit = Hit(RecordRef.ForPhoto(p), device, AppKind.Photos, p.fileName, p.source, p.serviceKey, p.takenAt, p.sortKey),
                        fields = new[] { p.fileName, Label(p.serviceKey, text) }.Concat(Dates(p.takenAt, time)),
                    };
                }

                foreach (var b in RecordQuery.For(db.browser, device, stage))
                    yield return RecordDoc(b, device, AppKind.Browser, b.title, time, b.title, b.url);
                foreach (var m in RecordQuery.For(db.maps, device, stage))
                    yield return RecordDoc(m, device, AppKind.Maps, RecordResolver.MapTitle(m, text), time,
                        m.placeLabel, m.routeFrom, m.routeTo, m.routeNote);
                foreach (var f in RecordQuery.For(db.files, device, stage))
                    yield return RecordDoc(f, device, AppKind.Files, f.FileName, time, f.FileName, f.FolderPath);
                foreach (var s in RecordQuery.For(db.settings, device, stage))
                {
                    string item = RecordQuery.SettingItemLabel(s, device);
                    yield return RecordDoc(s, device, AppKind.Settings, item, time, item, s.categoryLabel, s.valueBefore, s.valueAfter);
                }
            }
        }

        static Doc RecordDoc(DeviceRecord r, CaseData device, AppKind app, string title, PhoneTime time, params string[] fields) =>
            new Doc
            {
                hit = Hit(RecordRef.ForRecord(r), device, app, title, r.source, null, r.time, r.sortKey),
                fields = fields.Concat(Dates(r.time, time)),
            };

        static SearchHit Hit(RecordRef target, CaseData device, AppKind? app, string title, RecordSource source, string serviceKey,
            RelativeTime? t, int sortKey) =>
            new SearchHit
            {
                target = target,
                device = device,
                app = app,
                title = title,
                source = source,
                serviceKey = serviceKey,
                hasTime = t.HasValue,
                time = t ?? default,
                sortKey = sortKey,
            };

        /// <summary>caseDate 기준 표시 날짜와 업무 프로그램의 "사망 N일 전".</summary>
        static IEnumerable<string> Dates(RelativeTime t, PhoneTime time) => time.SearchDates(t).Append(time.SinceDeath(t));

        static string Label(string key, UIText text) => string.IsNullOrEmpty(key) ? null : text.Get(key);

        /// <summary>대소문자를 가리지 않고 찾는다. "10월31일"처럼 띄어쓰기를 생략해도 찾는다.</summary>
        static bool Find(string haystack, string query, out int index, out int length)
        {
            index = -1;
            length = 0;
            if (string.IsNullOrEmpty(haystack))
                return false;
            index = haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                length = query.Length;
                return true;
            }
            string q = query.Replace(" ", string.Empty);
            if (q.Length == 0)
                return false;
            // 공백을 무시하고 맞춘 뒤 원문 위치로 되돌린다.
            var map = new List<int>();
            var compact = new System.Text.StringBuilder();
            for (int i = 0; i < haystack.Length; i++)
            {
                if (haystack[i] == ' ')
                    continue;
                map.Add(i);
                compact.Append(haystack[i]);
            }
            int c = compact.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase);
            if (c < 0)
                return false;
            index = map[c];
            length = map[c + q.Length - 1] - index + 1;
            return true;
        }
    }
}
