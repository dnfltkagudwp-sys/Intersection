using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 인물 원본. 역할 키(A/B/C/D…)는 내부 식별용이며 화면에는 표시하지 않는다.
    /// 화면의 연락처명은 각 기기(CaseData)의 주소록 저장명에서 가져온다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Person", fileName = "Person")]
    public class PersonData : ContentAsset
    {
        [Tooltip("내부 기획 역할 키(A/B/C/D/E 등). 화면에 노출하지 않는다.")]
        public string roleKey;

        [Tooltip("인물 이름(시제품 가명 포함). 의뢰 표시명·기기 소유자 이름에 쓰인다.")]
        public string fullName;

        [Tooltip("전화번호. 저장되지 않은 상대의 표시 문구가 비어 있으면 대신 표시된다.")]
        public string phoneNumber;
    }
}
