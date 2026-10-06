using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 화면 문구·날짜 형식 문자열 테이블. 코드와 프리팹에는 키만 두고 문구는 여기서 읽는다.
    /// 문구 안의 {token}은 코드가 채우고, "와/과" 같은 조사 표기는 앞 글자 받침에 맞춰 자동 선택된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/UI/String Table", fileName = "Strings")]
    public class StringTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;

            [TextArea(1, 4)]
            public string value;
        }

        public List<Entry> entries = new List<Entry>();

        Dictionary<string, string> map;

        public bool TryGet(string key, out string value)
        {
            if (map == null)
                Build();
            return map.TryGetValue(key, out value);
        }

        public bool Contains(string key) => TryGet(key, out _);

        void Build()
        {
            map = new Dictionary<string, string>();
            foreach (var e in entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.key))
                    map[e.key] = e.value ?? string.Empty;
            }
        }

        void OnEnable() => map = null;

        void OnValidate() => map = null;
    }
}
