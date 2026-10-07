namespace Intersection.Core
{
    /// <summary>
    /// 업무 패널에 보이는 중립 기록 코드. 증거 여부와 무관하게 모든 기록이 같은 형식을 가진다.
    /// 불변 원본 ID와 기기 ID에서 계산하므로 순서·이름이 바뀌어도 유지되고,
    /// 같은 원본이라도 기기마다 다른 코드가 나와 두 기기 기록의 연결을 미리 드러내지 않는다.
    /// </summary>
    public static class RecordCode
    {
        public static string Compute(string recordId, string deviceId)
        {
            // FNV-1a 32bit: 실행 환경과 무관하게 항상 같은 값.
            uint hash = 2166136261;
            foreach (char c in (recordId ?? string.Empty) + "/" + (deviceId ?? string.Empty))
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("X8");
        }

        public static string Format(UIText text, string prefixKey, string recordId, string deviceId) =>
            text.Format(UIKeys.RecordCodeFormat, ("prefix", text.Get(prefixKey)), ("code", Compute(recordId, deviceId)));
    }
}
