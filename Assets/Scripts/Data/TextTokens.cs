using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Intersection.Data
{
    /// <summary>
    /// 표시 문구 안의 토큰. 지금은 파일 표시 문구의 {date:N}(caseDate 기준 N일, 화면에는 짧은 월/일)만 지원한다.
    /// 실제 날짜 계산은 표시 단계(PhoneTime)가 맡고, 여기서는 찾기·바꾸기·문법 검사만 한다.
    /// 화면 렌더링과 전역 검색은 같은 Expand 결과를 쓴다.
    /// </summary>
    public static class TextTokens
    {
        public const string DateToken = "date";

        /// <summary>고인이 사망 뒤 날짜를 적을 수는 없으므로 0(사망 당일)까지, 과거는 10년까지.</summary>
        public const int MinDateOffset = -3650;
        public const int MaxDateOffset = 0;

        static readonly Regex Token = new Regex(@"\{([^{}]*)\}", RegexOptions.Compiled);

        public static bool HasTokens(string s) => !string.IsNullOrEmpty(s) && (s.IndexOf('{') >= 0 || s.IndexOf('}') >= 0);

        /// <summary>올바른 {date:N}만 바꾸고, 잘못된 토큰은 원문 그대로 둔다(검증 도구가 오류로 잡는다).</summary>
        public static string Expand(string s, Func<int, string> date)
        {
            if (date == null || !HasTokens(s))
                return s;
            return Token.Replace(s, m => TryParseDate(m.Groups[1].Value, out int n, out _) ? date(n) : m.Value);
        }

        /// <summary>문법 오류 목록: 닫히지 않은 괄호, 알 수 없는 토큰, 숫자가 아니거나 범위 밖의 날짜.</summary>
        public static IEnumerable<string> Problems(string s)
        {
            if (!HasTokens(s))
                yield break;
            string rest = Token.Replace(s, string.Empty);
            if (rest.IndexOf('{') >= 0 || rest.IndexOf('}') >= 0)
                yield return "닫히지 않았거나 짝이 맞지 않는 중괄호가 있습니다";
            foreach (Match m in Token.Matches(s))
            {
                if (!TryParseDate(m.Groups[1].Value, out _, out string problem))
                    yield return $"{m.Value}: {problem}";
            }
        }

        static bool TryParseDate(string body, out int offset, out string problem)
        {
            offset = 0;
            int colon = body.IndexOf(':');
            string name = (colon < 0 ? body : body.Substring(0, colon)).Trim();
            if (name != DateToken)
            {
                problem = "지원하지 않는 토큰입니다(지원: {date:N})";
                return false;
            }
            string value = colon < 0 ? string.Empty : body.Substring(colon + 1).Trim();
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out offset))
            {
                problem = "날짜 값이 정수가 아닙니다";
                return false;
            }
            if (offset < MinDateOffset || offset > MaxDateOffset)
            {
                problem = $"날짜 값이 범위({MinDateOffset}~{MaxDateOffset}) 밖입니다";
                return false;
            }
            problem = null;
            return true;
        }
    }
}
