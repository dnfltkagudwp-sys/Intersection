using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 초반 업무 안내. 단계·체크리스트·완료율 없이, 요청을 읽은 뒤 대상 기록을 아직 보존하지 않았으면 짧은 안내 한 줄만 보여준다.
    /// 판정은 순서와 무관하게 현재 상태(요청 열람 + 대상의 현재 분류)로 한다. 요청 패널에는 대상의 현재 분류를 한 줄로 보여준다.
    /// 진행 작업의 시작 조건은 이 에셋이 아니라 작업 데이터(ProgressJobData)의 조건으로 따로 정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Work Guide", fileName = "Guide")]
    public class TutorialData : ContentAsset
    {
        [Tooltip("안내가 속한 의뢰 요청. 이 요청을 열람한 뒤에만 안내 줄을 보여준다.")]
        public WorkRequestData request;

        [Tooltip("보존할 대상 기록 (사진·기록 에셋). 에셋 참조라 이름·순서가 바뀌어도 연결이 유지된다.")]
        public ContentAsset target;

        [Tooltip("요청 패널 상태 줄에 쓰는 대상 이름 (예: 가족사진)")]
        public string targetLabel;

        [Tooltip("요청을 읽었고 대상이 아직 보존 상태가 아닐 때 하단에 보여주는 안내 한 줄")]
        public string hint;

        [Tooltip("이 단계부터는 안내 줄을 다시 띄우지 않는다 (재분류해도 돌아오지 않음). 요청 패널 상태 줄은 계속 현재 분류를 보여준다.")]
        public ProgressStage hintUntil = ProgressStage.P1;

        /// <summary>요청 패널 상태 줄의 대상 이름 (현재 언어).</summary>
        public string TargetLabel => ContentText.Get(Id, ContentText.Field.TargetLabel, targetLabel);

        /// <summary>하단 안내 한 줄 (현재 언어).</summary>
        public string Hint => ContentText.Get(Id, ContentText.Field.Hint, hint);

        public override System.Collections.Generic.IEnumerable<ContentField> ContentFields()
        {
            var list = new System.Collections.Generic.List<ContentField>();
            ContentText.Add(list, Id, ContentText.Field.TargetLabel, targetLabel);
            ContentText.Add(list, Id, ContentText.Field.Hint, hint);
            return list;
        }
    }
}
