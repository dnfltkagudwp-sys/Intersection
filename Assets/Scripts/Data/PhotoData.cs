using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 기기 갤러리의 사진 한 장. 표시 순서는 촬영 시각과 sortKey로 정해지며 파일·에셋 순서와 무관하다.
    /// 실제 이미지가 정해지지 않았으면 image를 비워 두고, 화면은 중립 자리 표시를 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Photo", fileName = "Photo")]
    public class PhotoData : ContentAsset
    {
        [Tooltip("이 사진이 들어 있는 기기(의뢰)")]
        public CaseData device;

        [Tooltip("기기에 저장된 파일명. 휴대전화 화면에는 표시하지 않고 업무 패널에만 표시한다.")]
        public string fileName;

        [Tooltip("사망일 기준 촬영(저장) 시각")]
        public RelativeTime takenAt;

        [Tooltip("같은 시각 사진 사이의 표시 순서. 작을수록 먼저 표시된다.")]
        public int sortKey;

        [Tooltip("스크린샷이면 켠다 (스크린샷 앨범에 자동으로 들어간다)")]
        public bool isScreenshot;

        public RecordSource source = RecordSource.Local;

        [Tooltip("확정된 생성 경로 문자열 키 (예: service.camera / service.messageSave). 미확정이면 비운다.")]
        public string serviceKey;

        [Tooltip("이 기기에서 접근 가능한 첫 단계")]
        public ProgressStage availableFrom = ProgressStage.P0;

        [Tooltip("증거 마스터표 ID (예: A01). 화면에 표시하지 않는다. 일반 사진은 비운다.")]
        public string evidenceRef;

        [Tooltip("같은 이미지 자산을 공유하는 기록끼리 같은 값 (예: 메시지 첨부와 갤러리 저장본)")]
        public string mediaAssetId;

        [Tooltip("실제 이미지. 비어 있으면 중립 자리 표시로 보인다.")]
        public Texture2D image;

        [Tooltip("무결성·복구 상태 문자열 키 (예: integrity.original)")]
        public string integrityKey;

        [TextArea(1, 4), Tooltip("작가용 장면 메모. 화면에 표시하지 않는다.")]
        public string authorNote;
    }
}
