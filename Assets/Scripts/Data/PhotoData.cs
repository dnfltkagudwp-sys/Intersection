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

        [Tooltip("선택. 이 사진이 만들어진 원본 기록(사진·파일, 다른 기기·의뢰·출처도 가능). 콘텐츠 검증·제작 관리용이며 화면·검색·비교에 표시하지 않는다.")]
        public ContentAsset derivedFrom;

        [Tooltip("실제 이미지. 비어 있으면 중립 자리 표시로 보인다.")]
        public Texture2D image;

        [Tooltip("무결성·복구 상태 문자열 키 (예: integrity.original)")]
        public string integrityKey;

        [TextArea(1, 4), Tooltip("작가용 장면 메모. 화면에 표시하지 않는다.")]
        public string authorNote;

        [TextArea(2, 8), Tooltip("선택. 스크린샷·문서 사진·간판처럼 이 이미지 안에서 플레이어가 실제로 읽을 수 있는 글자의 정확한 전사본.\n" +
                                 "전역 검색만 이 문구를 쓰며 갤러리·상세·비교에는 따로 표시하지 않는다(런타임 OCR 없음).\n" +
                                 "실제 이미지가 없는 사진에는 채우지 않는다. 작가 메모·증거 ID·내부 ID·역할명·정답 설명을 넣지 않는다.")]
        public string visibleText;

        /// <summary>업무 패널·검색에 보이는 파일명. 카메라가 붙인 구조적 이름(IMG_0412.jpg)이라 번역하지 않고 원문 그대로다.</summary>
        public string FileName => fileName;

        /// <summary>이미지 안 글자 전사본 (현재 언어). 이미지가 언어별로 다르면 그 언어 이미지의 전사본을 번역 값으로 둔다.</summary>
        public string VisibleText => ContentText.Get(Id, ContentText.Field.VisibleText, visibleText);

        /// <summary>현지화 대상은 이미지 안 글자 전사본뿐이다(파일명은 구조적 이름).</summary>
        public override System.Collections.Generic.IEnumerable<ContentField> ContentFields()
        {
            var list = new System.Collections.Generic.List<ContentField>();
            ContentText.Add(list, Id, ContentText.Field.VisibleText, visibleText);
            return list;
        }

        /// <summary>이미지에 적힌 글자 한 줄씩 (현재 언어, 전역 검색 대상, 빈 줄 제외).</summary>
        public System.Collections.Generic.IEnumerable<string> VisibleLines()
        {
            string source = VisibleText;
            if (string.IsNullOrWhiteSpace(source))
                yield break;
            foreach (var line in source.Replace("\r", string.Empty).Split('\n'))
            {
                string l = line.Trim();
                if (l.Length > 0)
                    yield return l;
            }
        }
    }
}
