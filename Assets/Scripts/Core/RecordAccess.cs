using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    public enum AccessState
    {
        /// <summary>현재 단계에서 원본을 열 수 있음.</summary>
        Accessible,
        /// <summary>원본을 찾지 못함.</summary>
        Missing,
        /// <summary>같은 ID가 여러 원본에 있어 하나로 정할 수 없음.</summary>
        Duplicate,
        /// <summary>원본은 있지만 현재 접근 단계에서 열 수 없음 (기기 접근 전·연동/복구 전).</summary>
        Unavailable,
    }

    /// <summary>
    /// 모든 기록의 접근 판정. 앱 목록·검색·작업 기록의 원본 열기·비교 추가·삭제 대기 조회·진행 조건이 같은 판정을 쓴다.
    /// 판정은 앱 화면과 같은 조회(DeviceQuery·PhotoQuery·RecordQuery)로 하므로, 화면에 나오지 않는 기록은 어디서도 열리지 않는다.
    /// 저장된 과거 핀·비교 이력이 있어도 현재 단계를 우회하지 못한다.
    /// </summary>
    public static class RecordAccess
    {
        public static AccessState Check(RecordRef r, ContentDatabase db, ProgressStage stage, UIText text, out ResolvedRecord res)
        {
            res = RecordResolver.Resolve(r, db, text);
            if (!res.found)
                return res.duplicate ? AccessState.Duplicate : AccessState.Missing;
            var device = res.device;
            if (!device.IsAvailable(stage))
                return AccessState.Unavailable;
            string id = r.recordId;
            bool visible;
            switch (r.kind)
            {
                case RecordKind.Thread:
                    visible = DeviceQuery.Threads(db, device, stage).Any(e => e.thread.Id == id);
                    break;
                case RecordKind.Message:
                case RecordKind.Attachment:
                case RecordKind.LocationShare:
                    visible = DeviceQuery.Threads(db, device, stage).Any(e => e.thread.Id == r.threadId);
                    break;
                case RecordKind.Photo:
                    visible = PhotoQuery.DevicePhotos(db, device, stage).Any(p => p.Id == id);
                    break;
                case RecordKind.Browser:
                    visible = RecordQuery.For(db.browser, device, stage).Any(x => x.Id == id);
                    break;
                case RecordKind.Map:
                    visible = RecordQuery.For(db.maps, device, stage).Any(x => x.Id == id);
                    break;
                case RecordKind.File:
                    visible = RecordQuery.For(db.files, device, stage).Any(x => x.Id == id);
                    break;
                case RecordKind.Setting:
                    visible = RecordQuery.For(db.settings, device, stage).Any(x => x.Id == id);
                    break;
                case RecordKind.Request:
                    visible = db.requests.Any(q => q != null && q.Id == id && q.device == device && stage >= q.availableFrom);
                    break;
                default:
                    visible = false;
                    break;
            }
            return visible ? AccessState.Accessible : AccessState.Unavailable;
        }

        public static AccessState Check(RecordRef r, ContentDatabase db, ProgressStage stage, UIText text) =>
            Check(r, db, stage, text, out _);

        /// <summary>이 기기의 로컬 자료가 인덱싱까지 끝나 검색할 수 있는지.</summary>
        public static bool Searchable(CaseData device, ProgressStage stage) =>
            device != null && device.IsAvailable(stage) && stage >= device.localIndexedFrom;
    }
}
