using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>
    /// 삭제 대기 목록의 한 항목. WorkStore의 삭제 후보 분류에서 계산하며 따로 저장하지 않는다.
    /// 원본을 실제로 지우지 않는다. 핀·메모·열람 상태와 무관하다.
    /// </summary>
    public class DeletionQueueEntry
    {
        public RecordRef target;
        public AccessState state;
        public ResolvedRecord resolved;
    }

    /// <summary>
    /// 이후 최종 작업 화면이 쓸 삭제 대기 조회. 삭제 후보를 해제하면 다음 조회에서 바로 빠진다.
    /// 원본 누락·ID 중복·현재 접근 불가를 구분해 돌려주며, 어떤 경우에도 원본 데이터를 바꾸지 않는다.
    /// </summary>
    public static class DeletionQueue
    {
        public static List<DeletionQueueEntry> Compute(WorkStore work, ContentDatabase db, ProgressStage stage, UIText text) =>
            work.All
                .Where(e => e.classification == Classification.Delete && e.target != null)
                .OrderBy(e => e.target.Key, System.StringComparer.Ordinal)
                .Select(e =>
                {
                    var state = RecordAccess.Check(e.target, db, stage, text, out var res);
                    return new DeletionQueueEntry { target = e.target, state = state, resolved = res };
                })
                .ToList();
    }
}
