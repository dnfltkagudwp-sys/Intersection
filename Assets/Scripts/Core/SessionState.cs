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
