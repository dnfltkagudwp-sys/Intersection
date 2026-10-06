using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>한 기기에서 보이는 대화 목록의 한 행. 모든 값은 원본 데이터에서 계산된다.</summary>
    public class ThreadEntry
    {
        public ThreadData thread;
        public DeviceThreadState state;
        public MessageData last;
        public string displayName;
    }

    /// <summary>원본 데이터에서 기기별 표시 정보를 계산한다. 결과를 저장하지 않는다.</summary>
    public static class DeviceQuery
    {
        public static List<ThreadEntry> Threads(ContentDatabase db, CaseData device, ProgressStage stage)
        {
            var result = new List<ThreadEntry>();
            if (db == null || device == null)
                return result;
            foreach (var thread in db.threads)
            {
                if (thread == null)
                    continue;
                var state = thread.StateFor(device);
                if (state == null || stage < state.availableFrom)
                    continue;
                var last = Ordered(thread).LastOrDefault();
                if (last == null)
                    continue;
                result.Add(new ThreadEntry
                {
                    thread = thread,
                    state = state,
                    last = last,
                    displayName = DisplayName(thread, device),
                });
            }
            return result
                .OrderByDescending(e => e.last.time.TotalMinutes)
                .ThenByDescending(e => e.last.sortKey)
                .ToList();
        }

        public static List<MessageData> Ordered(ThreadData thread) =>
            thread.AllMessages.OrderBy(m => m.time.TotalMinutes).ThenBy(m => m.sortKey).ToList();

        /// <summary>대화방 제목: 단체방은 방 제목, 1:1은 이 기기의 주소록 저장명 또는 미저장 표시.</summary>
        public static string DisplayName(ThreadData thread, CaseData device)
        {
            if (thread.IsGroup)
                return thread.groupTitle;
            var other = thread.participants.FirstOrDefault(p => p != null && p != device.owner);
            return ContactName(other, device);
        }

        public static string ContactName(PersonData person, CaseData device)
        {
            var saved = device.SavedNameOf(person);
            if (!string.IsNullOrEmpty(saved))
                return saved;
            if (!string.IsNullOrEmpty(device.unsavedContactLabel))
                return device.unsavedContactLabel;
            return person != null ? person.phoneNumber : string.Empty;
        }

        /// <summary>목록 미리보기. 원본 본문·첨부 유형에서 만들며 잘린 문장을 따로 저장하지 않는다.</summary>
        public static string Preview(MessageData message, UIText text)
        {
            if (message == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(message.body))
                return message.body.Replace("\r", "").Replace('\n', ' ');
            switch (message.attachment)
            {
                case AttachmentKind.Image: return text.Get(UIKeys.PreviewImage);
                case AttachmentKind.Location: return text.Get(UIKeys.PreviewLocation);
                default: return string.Empty;
            }
        }

        public static int UnreadExcept(List<ThreadEntry> entries, ThreadData except) =>
            entries.Where(e => e.thread != except).Sum(e => e.state.unreadCount);
    }
}
