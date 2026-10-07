using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 한 기기에 남은 기록 한 건(브라우저·지도·파일·설정)의 공통 필드.
    /// 표시 순서는 시각과 sortKey로 정하며 에셋·배열 순서와 무관하다.
    /// </summary>
    public abstract class DeviceRecord : ContentAsset
    {
        [Tooltip("이 기록이 남은 기기(의뢰)")]
        public CaseData device;

        [Tooltip("사망일 기준 기록 시각")]
        public RelativeTime time;

        [Tooltip("같은 시각 기록 사이의 표시 순서. 작을수록 먼저.")]
        public int sortKey;

        public RecordSource source = RecordSource.Local;

        [Tooltip("이 기기에서 접근 가능한 첫 단계 (예: A 클라우드 자료는 P2)")]
        public ProgressStage availableFrom = ProgressStage.P0;

        [Tooltip("증거 마스터표 ID. 화면에 표시하지 않는다. 일반 기록은 비운다.")]
        public string evidenceRef;

        [Tooltip("무결성·복구 상태 문자열 키 (예: integrity.original)")]
        public string integrityKey;

        [TextArea(1, 4), Tooltip("작가용 메모. 화면에 표시하지 않는다.")]
        public string authorNote;
    }
}
