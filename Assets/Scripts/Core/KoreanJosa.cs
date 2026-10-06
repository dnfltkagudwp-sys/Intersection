using System.Text.RegularExpressions;

namespace Intersection.Core
{
    /// <summary>
    /// "번호와/과", "정유진이/가"처럼 쓴 조사 쌍을 앞 글자 받침에 맞게 하나로 고른다.
    /// 이름이 데이터에서 바뀌어도 문장이 자연스럽게 유지되도록 한다.
    /// </summary>
    public static class KoreanJosa
    {
        static readonly Regex Pattern = new Regex(@"(\S)(은/는|는/은|이/가|가/이|을/를|를/을|와/과|과/와|으로/로|로/으로)");

        public static string Resolve(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('/') < 0)
                return text;
            return Pattern.Replace(text, m =>
            {
                char prev = m.Groups[1].Value[m.Groups[1].Value.Length - 1];
                return m.Groups[1].Value + Pick(prev, m.Groups[2].Value);
            });
        }

        static string Pick(char prev, string pair)
        {
            bool batchim = HasBatchim(prev, out bool rieul);
            switch (pair)
            {
                case "은/는": case "는/은": return batchim ? "은" : "는";
                case "이/가": case "가/이": return batchim ? "이" : "가";
                case "을/를": case "를/을": return batchim ? "을" : "를";
                case "와/과": case "과/와": return batchim ? "과" : "와";
                default: return batchim && !rieul ? "으로" : "로";
            }
        }

        static bool HasBatchim(char c, out bool rieul)
        {
            rieul = false;
            if (c >= 0xAC00 && c <= 0xD7A3)
            {
                int jong = (c - 0xAC00) % 28;
                rieul = jong == 8;
                return jong != 0;
            }
            switch (c)
            {
                // 영 일 삼 육 칠 팔
                case '0': case '3': case '6': return true;
                case '1': case '7': case '8': rieul = true; return true;
                default: return false;
            }
        }
    }
}
