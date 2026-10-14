using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 한 언어의 콘텐츠 번역 테이블. 키는 `콘텐츠 불변 ID:필드 키`(예: `{파일 ID}:fileName`, `{파일 ID}:visibleText:{블록 ID}`,
    /// `{대화 ID}:message:{메시지 ID}:text`)다. 값이 없거나 비어 있는 키는 에셋의 한국어 원문으로 대체된다.
    /// 기본 언어(한국어)는 테이블이 없다. 데이터 에셋의 원문·불변 ID·저장 키는 바꾸지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Content Locale Table", fileName = "Content_en")]
    public class ContentLocaleTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;

            [TextArea(1, 8)]
            public string value;
        }

        [Tooltip("언어 코드 (예: en). 기본 언어(ko)는 테이블을 만들지 않는다.")]
        public string language;

        public List<Entry> entries = new List<Entry>();

        [NonSerialized] Dictionary<string, string> map;

        public bool TryGet(string key, out string value)
        {
            if (map == null)
                Build();
            return map.TryGetValue(key, out value);
        }

        void Build()
        {
            map = new Dictionary<string, string>();
            foreach (var e in entries)
            {
                // 중복 키는 검증 도구가 오류로 잡는다. 실행 중에는 먼저 나온 값을 쓴다.
                if (e != null && !string.IsNullOrEmpty(e.key) && !map.ContainsKey(e.key))
                    map[e.key] = e.value;
            }
        }

        void OnValidate() => map = null;

        void OnEnable() => map = null;
    }
}
