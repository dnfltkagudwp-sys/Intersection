using Intersection.Core;
using TMPro;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>고정 라벨용. 프리팹에는 키만 두고 실행 시 문자열 테이블에서 문구를 채운다.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] string key;

        public string Key
        {
            get => key;
            set => key = value;
        }

        public void Apply(UIText text) => GetComponent<TMP_Text>().text = text.Get(key);
    }
}
