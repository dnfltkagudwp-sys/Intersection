using System;
using Intersection.Data;

namespace Intersection.Core
{
    public enum RecordKind
    {
        Thread,
        Message,
        Attachment,
        LocationShare,
        Photo,
        Browser,
        Map,
        File,
        Setting,
        Request,
    }

    /// <summary>
    /// 선택·핀·분류·작업메모가 가리키는 기록. 불변 원본 ID만으로 만들며
    /// 배열 위치·화면 순서·표시 문자열은 쓰지 않는다. 같은 원본이라도 기기가 다르면 다른 업무 기록이다.
    /// </summary>
    [Serializable]
    public class RecordRef : UnityEngine.ISerializationCallbackReceiver
    {
        /// <summary>저장 파일에는 이름(kindName)으로 기록해 enum 순서가 바뀌어도 의미가 유지된다.</summary>
        [NonSerialized] public RecordKind kind;
        [UnityEngine.SerializeField] string kindName;
        [NonSerialized] bool kindKnown = true;

        public string deviceId;

        /// <summary>대상 원본 ID (대화방은 threadId, 메시지·첨부·위치 공유는 messageId, 그 외는 에셋 ID).</summary>
        public string recordId;

        /// <summary>메시지 계열의 소속 대화 ID (조회용).</summary>
        public string threadId;

        /// <summary>첨부의 이미지 자산 ID.</summary>
        public string mediaAssetId;

        /// <summary>저장 파일의 종류 이름을 이 버전에서 해석할 수 있는지.</summary>
        public bool IsKnownKind => kindKnown;

        public void OnBeforeSerialize() => kindName = kindKnown ? kind.ToString() : kindName;

        public void OnAfterDeserialize() => kindKnown = Enum.TryParse(kindName, out kind);

        public string Key =>
            !kindKnown ? $"?{kindName}:{deviceId}:{recordId}"
            : kind == RecordKind.Attachment
                ? $"{kind}:{deviceId}:{recordId}:{mediaAssetId}"
                : $"{kind}:{deviceId}:{recordId}";

        public static RecordRef ForThread(CaseData device, ThreadData thread) =>
            new RecordRef { kind = RecordKind.Thread, deviceId = device.Id, recordId = thread.Id, threadId = thread.Id };

        /// <summary>메시지 종류에 따라 개별 메시지·첨부·위치 공유 참조를 만든다.</summary>
        public static RecordRef ForMessage(CaseData device, ThreadData thread, MessageData message)
        {
            var kind = message.attachment == AttachmentKind.Image ? RecordKind.Attachment
                : message.attachment == AttachmentKind.Location ? RecordKind.LocationShare
                : RecordKind.Message;
            return new RecordRef
            {
                kind = kind,
                deviceId = device.Id,
                recordId = message.Id,
                threadId = thread.Id,
                mediaAssetId = kind == RecordKind.Attachment ? message.mediaAssetId ?? string.Empty : null,
            };
        }

        public static RecordRef ForPhoto(PhotoData photo) =>
            new RecordRef { kind = RecordKind.Photo, deviceId = photo.device != null ? photo.device.Id : null, recordId = photo.Id };

        public static RecordRef ForRecord(DeviceRecord record)
        {
            var kind = record is BrowserRecord ? RecordKind.Browser
                : record is MapRecord ? RecordKind.Map
                : record is FileRecord ? RecordKind.File
                : RecordKind.Setting;
            return new RecordRef { kind = kind, deviceId = record.device != null ? record.device.Id : null, recordId = record.Id };
        }

        public static RecordRef ForRequest(WorkRequestData request) =>
            new RecordRef { kind = RecordKind.Request, deviceId = request.device != null ? request.device.Id : null, recordId = request.Id };

        /// <summary>튜토리얼 대상처럼 에셋 참조로 지정된 기록. 지원하지 않는 에셋이면 null.</summary>
        public static RecordRef ForAsset(ContentAsset asset)
        {
            switch (asset)
            {
                case PhotoData p: return ForPhoto(p);
                case DeviceRecord r: return ForRecord(r);
                case WorkRequestData q: return ForRequest(q);
                default: return null;
            }
        }
    }
}
