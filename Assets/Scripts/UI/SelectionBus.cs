using System;
using Intersection.Core;

namespace Intersection.UI
{
    /// <summary>
    /// 선택 모드 상태를 화면 요소(SelectableRecord)에 알린다.
    /// 상태의 원본은 AppShell과 WorkStore이며, 여기에는 표시에 필요한 값만 둔다.
    /// </summary>
    public static class SelectionBus
    {
        public static bool Active { get; private set; }
        public static string SelectedKey { get; private set; }
        public static Func<string, bool> IsPinned = _ => false;
        public static Action<RecordRef> SelectRequested;

        public static event Action Changed;

        public static void Set(bool active, string selectedKey)
        {
            Active = active;
            SelectedKey = active ? selectedKey : null;
            Changed?.Invoke();
        }

        public static void Notify() => Changed?.Invoke();

        public static void Reset()
        {
            Active = false;
            SelectedKey = null;
            IsPinned = _ => false;
            SelectRequested = null;
        }
    }
}
