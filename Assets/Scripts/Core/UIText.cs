using System.Collections.Generic;
using Intersection.Data;
using UnityEngine;

namespace Intersection.Core
{
    /// <summary>문자열 테이블 조회와 {token} 치환, 조사 자동 선택.</summary>
    public class UIText
    {
        readonly StringTable table;
        readonly HashSet<string> warned = new HashSet<string>();

        public UIText(StringTable table)
        {
            this.table = table;
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (table != null && table.TryGet(key, out var value))
                return value;
            if (warned.Add(key))
                Debug.LogWarning($"[UIText] 문자열 테이블에 키가 없습니다: {key}");
            return "#" + key;
        }

        public string Format(string key, params (string token, string value)[] args) => Fill(Get(key), args);

        public static string Fill(string template, params (string token, string value)[] args)
        {
            if (template == null)
                return string.Empty;
            foreach (var (token, value) in args)
                template = template.Replace("{" + token + "}", value ?? string.Empty);
            return KoreanJosa.Resolve(template);
        }
    }
}
