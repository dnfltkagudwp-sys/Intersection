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

        /// <summary>단수 변형 키의 접미사. 수가 1이고 테이블에 "키.one"이 있으면 그 문구를 쓴다(예: 영어 "1 day"). 없으면 기본 키.</summary>
        public const string OneSuffix = ".one";

        /// <summary>수량 문구. count가 1이면 단수 변형(키.one)이 있을 때 그것을, 아니면 기본 문구를 채운다.</summary>
        public string FormatCount(string key, int count, params (string token, string value)[] args)
        {
            string chosen = count == 1 && table != null && table.Contains(key + OneSuffix) ? key + OneSuffix : key;
            return Format(chosen, args);
        }

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
