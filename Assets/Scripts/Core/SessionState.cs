using System.Collections.Generic;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>기기별 휴대전화 탐색 위치. 원본 데이터와 분리된 표시 상태다.</summary>
    public class DeviceNavigation
    {
        public AppKind app = AppKind.Messages;

        /// <summary>열려 있는 대화방의 불변 ID. null이면 대화 목록.</summary>
        public string openThreadId;

        /// <summary>대화방·목록별 스크롤 위치 (불변 ID 키, 목록은 빈 문자열).</summary>
        public readonly Dictionary<string, float> scroll = new Dictionary<string, float>();

        /// <summary>메시지 앱 내부 검색어. 기기마다 따로 유지된다.</summary>
        public string searchQuery = string.Empty;

        /// <summary>검색 결과에서 연 메시지의 불변 ID. 대화방을 처음 그릴 때 한 번만 사용한다.</summary>
        public string focusMessageId;

        /// <summary>사진 앱에서 열려 있는 앨범의 불변 ID. null이면 앨범 목록.</summary>
        public string photoAlbumId;

        /// <summary>한 장 보기로 열린 사진의 불변 ID. null이면 그리드.</summary>
        public string openPhotoId;

        /// <summary>사진 앱 앨범 목록의 스크롤 키 (앨범 그리드는 앨범 ID를 키로 쓴다).</summary>
        public const string AlbumListScrollKey = "photos/albums";

        /// <summary>브라우저·지도·파일·설정 앱에서 상세로 연 기록의 불변 ID (앱별).</summary>
        public readonly Dictionary<AppKind, string> openRecordId = new Dictionary<AppKind, string>();

        /// <summary>파일 앱에서 연 위치. null이면 위치 목록.</summary>
        public RecordSource? filesLocation;

        /// <summary>파일 앱 위치 안의 현재 폴더 경로 (최상위는 빈 문자열).</summary>
        public string filesPath = string.Empty;

        public string OpenRecord(AppKind app) => openRecordId.TryGetValue(app, out var id) ? id : null;

        public void SetOpenRecord(AppKind app, string id)
        {
            if (id == null)
                openRecordId.Remove(app);
            else
                openRecordId[app] = id;
        }

        /// <summary>목록 화면의 스크롤 키. 파일 앱은 위치·폴더마다 따로 저장한다.</summary>
        public string ListScrollKey(AppKind app) =>
            app == AppKind.Files
                ? "files/" + (filesLocation.HasValue ? filesLocation.Value.ToString() : "") + "/" + filesPath
                : "list/" + app;
    }

    /// <summary>현재 진행 단계와 선택된 의뢰, 기기별 탐색 상태.</summary>
    public class SessionState
    {
        readonly Dictionary<string, DeviceNavigation> navigation = new Dictionary<string, DeviceNavigation>();

        public SessionState(ProgressStage stage)
        {
            Stage = stage;
        }

        public ProgressStage Stage { get; set; }
        public CaseData CurrentCase { get; set; }

        public DeviceNavigation Nav(CaseData device)
        {
            if (device == null)
                return new DeviceNavigation();
            if (!navigation.TryGetValue(device.Id, out var nav))
            {
                nav = new DeviceNavigation();
                navigation[device.Id] = nav;
            }
            return nav;
        }
    }
}
