using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    public class SearchResult
    {
        public ThreadEntry entry;

        /// <summary>메시지 결과일 때의 메시지. 대화 상대 결과면 null.</summary>
        public MessageData message;
    }

    /// <summary>
    /// 메시지 앱 내부 검색. 이 기기에서 지금 보이는 표시 정보(대화 상대·말풍선 문구·표시 날짜)만 찾는다.
    /// 내부 ID·증거 참조·작가 메모는 검색 대상이 아니며, 검색은 새 데이터를 만들거나 해금하지 않는다.
    /// </summary>
    public static class MessageSearch
    {
        public static void Run(List<ThreadEntry> entries, CaseData device, string query, UIText text, PhoneTime time,
            List<SearchResult> threadResults, List<SearchResult> messageResults)
        {
            threadResults.Clear();
            messageResults.Clear();
            string q = (query ?? string.Empty).Trim();
            if (q.Length == 0)
                return;

            foreach (var entry in entries)
            {
                // 목록과 같은 순서(최신순)를 유지한다.
                if (Matches(entry.displayName, q) || DeviceQuery.ParticipantNames(entry.thread, device).Any(n => Matches(n, q)))
                    threadResults.Add(new SearchResult { entry = entry });

                foreach (var m in DeviceQuery.Ordered(entry.thread))
                {
                    if (Matches(DeviceQuery.DisplayText(m, text), q) || time.SearchDates(m.time).Any(d => Matches(d, q)))
                        messageResults.Add(new SearchResult { entry = entry, message = m });
                }
            }

            messageResults.Sort((a, b) =>
            {
                int c = b.message.time.TotalMinutes.CompareTo(a.message.time.TotalMinutes);
                return c != 0 ? c : b.message.sortKey.CompareTo(a.message.sortKey);
            });
        }

        static bool Matches(string haystack, string query)
        {
            if (string.IsNullOrEmpty(haystack))
                return false;
            if (haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // "10월31일"처럼 띄어쓰기를 생략해도 찾을 수 있게 한다.
            return StripSpaces(haystack).IndexOf(StripSpaces(query), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string StripSpaces(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
