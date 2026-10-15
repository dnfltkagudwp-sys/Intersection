namespace Intersection.Data
{
    /// <summary>데이터 접근 구간. P5는 기획상 없다.</summary>
    public enum ProgressStage
    {
        P0 = 0,
        P1 = 1,
        P2 = 2,
        P3 = 3,
        P4 = 4,
        P6 = 6,
    }

    /// <summary>기록의 출처. 화면에는 출처 배지로만 표시한다.</summary>
    public enum RecordSource
    {
        Local,
        Cloud,
        Linked,
        Recovered,
    }

    public enum AttachmentKind
    {
        None,
        Image,
        Location,
    }

    /// <summary>휴대전화 앱 종류. 코드의 화면 라우팅에만 쓰고 표시명·순서는 앱 등록 데이터에서 읽는다.</summary>
    public enum AppKind
    {
        Messages,
        Photos,
        Browser,
        Maps,
        Files,
        Settings,
        /// <summary>읽기 전용 메일 기록(받은편지함·임시 보관함 등). 정수 순서 유지를 위해 끝에 추가한다.</summary>
        Mail,
    }
}
