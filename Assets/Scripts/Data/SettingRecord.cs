using UnityEngine;

namespace Intersection.Data
{
    public enum MuteChange
    {
        None,
        Mute,
        Unmute,
    }

    /// <summary>
    /// 설정 변경 기록 한 건. 같은 settingKey의 가장 마지막 변경이 현재 설정이 된다.
    /// 대화 알림 설정은 linkedThread로 대화 원본을 가리키며, 표시명은 기기 주소록에서 계산한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Records/Setting", fileName = "Setting")]
    public class SettingRecord : DeviceRecord
    {
        [Tooltip("같은 설정 항목끼리 묶는 내부 키 (화면에 표시하지 않는다)")]
        public string settingKey;

        [Tooltip("설정 분류 표시명 (예: 메시지)")]
        public string categoryLabel;

        [Tooltip("설정 항목 표시명. {contact}는 linkedThread의 이 기기 기준 대화 상대 표시명으로 바뀐다.")]
        public string itemLabel;

        [Tooltip("대화별 설정이면 대상 대화")]
        public ThreadData linkedThread;

        public string valueBefore;
        public string valueAfter;

        [Tooltip("대화 알림을 바꾸는 기록이면 지정. 검증 도구가 대화의 알림 끔 상태와 맞는지 확인한다.")]
        public MuteChange muteChange;
    }
}
