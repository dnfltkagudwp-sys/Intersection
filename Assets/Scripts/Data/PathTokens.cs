using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Intersection.Data
{
    /// <summary>
    /// 파일 경로·파일명 안의 날짜 토큰 {folderDate:N} (caseDate 기준 N일, 형식은 GameConfig.folderDateFormat).
    /// 결과는 기기에 실제로 저장된 경로라서 언어와 무관하다(고정 문화권, 문자열 테이블을 쓰지 않음).
    /// 파일 표시 문구의 {date:N}(TextTokens, 화면용 짧은 월/일)과 이름·형식을 공유하지 않는다.
    /// 폴더 목록·경로 표시·파일 상세·검색·원본 열기·비교가 모두 같은 Expand 결과를 쓰며, 원래 토큰 문자열은 어디에도 보이지 않는다.
    /// </summary>
    public static class PathTokens
    {
        public const string FolderDateToken = "folderDate";

        /// <summary>파일은 사망 당일까지 만들어질 수 있고, 과거는 10년까지.</summary>
        public const int MinOffset = -3650;
        public const int MaxOffset = 0;

        public const string DefaultFormat = "yyMMdd";

        static readonly Regex Token = new Regex(@"\{([^{}]*)\}", RegexOptions.Compiled);

        static DateTime? caseDate;
        static string format = DefaultFormat;

        /// <summary>현재 caseDate·형식. GameConfig가 로드·변경될 때와 실행·검증 시작 때 맞춘다.</summary>
        public static void Configure(DateTime date, string dateFormat)
        {
            caseDate = date.Date;
            format = IsValidFormat(dateFormat) ? dateFormat : DefaultFormat;
        }

        public static bool IsConfigured => caseDate.HasValue;

        public static bool HasTokens(string s) => !string.IsNullOrEmpty(s) && (s.IndexOf('{') >= 0 || s.IndexOf('}') >= 0);

        /// <summary>형식 문자열이 날짜로 바꿀 수 있고 경로 구분자·중괄호를 만들지 않는지.</summary>
        public static bool IsValidFormat(string f)
        {
            if (string.IsNullOrWhiteSpace(f))
                return false;
            try
            {
                string sample = new DateTime(2000, 1, 2).ToString(f, CultureInfo.InvariantCulture);
                return sample.Length > 0 && sample.IndexOfAny(new[] { '/', '\\', '{', '}' }) < 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>올바른 {folderDate:N}만 바꾸고, 잘못된 토큰은 원문 그대로 둔다(검증 도구가 오류로 잡는다). 설정 전에는 바꾸지 않는다.</summary>
        public static string Expand(string s)
        {
            if (!caseDate.HasValue || !HasTokens(s))
                return s;
            return Token.Replace(s, m => TryParse(m.Groups[1].Value, out int n, out _)
                ? caseDate.Value.AddDays(n).ToString(format, CultureInfo.InvariantCulture)
                : m.Value);
        }

        /// <summary>문법 오류 목록: 닫히지 않은 괄호·단독 중괄호, 알 수 없는 토큰 이름, 숫자가 아니거나 범위 밖의 값.</summary>
        public static IEnumerable<string> Problems(string s)
        {
            if (!HasTokens(s))
                yield break;
            string rest = Token.Replace(s, string.Empty);
            if (rest.IndexOf('{') >= 0 || rest.IndexOf('}') >= 0)
                yield return "닫히지 않았거나 짝이 맞지 않는 중괄호가 있습니다";
            foreach (Match m in Token.Matches(s))
            {
                if (!TryParse(m.Groups[1].Value, out _, out string problem))
                    yield return $"{m.Value}: {problem}";
            }
        }

        static bool TryParse(string body, out int offset, out string problem)
        {
            offset = 0;
            int colon = body.IndexOf(':');
            string name = colon < 0 ? body : body.Substring(0, colon);
            if (name != FolderDateToken)
            {
                problem = "경로·파일명에는 {folderDate:N}만 쓸 수 있습니다(대소문자 구분, 공백 없음)";
                return false;
            }
            string value = colon < 0 ? string.Empty : body.Substring(colon + 1);
            if (value.Length == 0 || value.Trim() != value
                || !int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out offset))
            {
                problem = "날짜 값이 정수가 아닙니다";
                return false;
            }
            if (offset < MinOffset || offset > MaxOffset)
            {
                problem = $"날짜 값이 범위({MinOffset}~{MaxOffset}) 밖입니다";
                return false;
            }
            problem = null;
            return true;
        }
    }
}
