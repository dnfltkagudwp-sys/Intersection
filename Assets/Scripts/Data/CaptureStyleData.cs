using TMPro;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 캡처 화면의 공통 디자인 시스템(한 앱의 글꼴·색·크기·간격). 같은 앱의 캡처는 모두 같은 스타일 에셋을 쓰고,
    /// 캡처마다 좌표를 따로 맞추지 않는다. 서비스명·로고는 두지 않는다. 런타임에는 쓰지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Production/Capture Style", fileName = "CaptureStyle")]
    public class CaptureStyleData : ScriptableObject
    {
        [Header("캔버스")]
        public int width = 1080;
        public int height = 2340;
        [Tooltip("상태바 시각 형식 문자열 키 (게임 휴대전화 상태바와 같은 키)")]
        public string clockFormatKey = "format.statusClock";
        [Tooltip("계정명과 상대 시간 사이 구분자")]
        public string metaSeparator = " · ";

        [Header("글꼴 (한글은 Pretendard 계열, 고정폭 쓰지 않음)")]
        public TMP_FontAsset regularFont;
        public TMP_FontAsset mediumFont;
        public TMP_FontAsset boldFont;

        [Header("글자 크기 (px) — 계정명·제목 > 본문 > 댓글 > 날짜·보조")]
        public float accountSize = 50;
        public float titleSize = 52;
        public float bodySize = 50;
        public float commentSize = 46;
        public float metaSize = 40;
        [Tooltip("수치·버튼·탭처럼 본문보다 시선을 덜 끄는 표시")]
        public float labelSize = 42;
        public float statusSize = 38;
        [Tooltip("줄 간격 (TMP em의 1/100 단위). 한 문장이 길어 줄바꿈될 때의 간격")]
        public float lineSpacing = 16;

        [Header("간격 (px)")]
        [Tooltip("좌우 여백")]
        public float padding = 36;
        [Tooltip("같은 덩어리 안 요소 사이")]
        public float tightGap = 10;
        [Tooltip("덩어리 사이")]
        public float gap = 26;
        [Tooltip("게시물·목록 항목 사이 (얇은 구분선과 함께)")]
        public float sectionGap = 40;

        [Header("영역 크기 (px)")]
        public float statusBarHeight = 100;
        public float navHeight = 124;
        public float headerAvatarSize = 76;
        public float profileAvatarSize = 196;
        public float commentAvatarSize = 68;
        public float listIconSize = 88;
        public float iconSize = 60;
        [Tooltip("선형 아이콘 선 굵기 (아이콘 크기 대비)")]
        [Range(0.02f, 0.2f)] public float iconStroke = 0.075f;
        public float buttonHeight = 84;
        public float fieldHeight = 96;
        public float cornerRadius = 18;
        public float dividerThickness = 2;
        [Tooltip("안내 화면 글 묶음의 세로 위치 (남는 높이 대비, 0 위 ~ 1 아래)")]
        [Range(0, 1)] public float noticeAnchor = 0.38f;

        [Header("색")]
        public Color background = Color.white;
        public Color text = new Color32(0x14, 0x15, 0x17, 0xFF);
        [Tooltip("보조 글자. 흰 바탕에서 충분히 읽히는 대비")]
        public Color muted = new Color32(0x56, 0x5A, 0x60, 0xFF);
        public Color link = new Color32(0x1E, 0x4F, 0x9C, 0xFF);
        public Color icon = new Color32(0x2A, 0x2C, 0x30, 0xFF);
        public Color divider = new Color32(0xE6, 0xE7, 0xEA, 0xFF);
        public Color field = new Color32(0xF1, 0xF2, 0xF4, 0xFF);
        public Color button = new Color32(0xEF, 0xF0, 0xF2, 0xFF);
        public Color buttonText = new Color32(0x1C, 0x1D, 0x20, 0xFF);
        public Color statusBar = Color.white;
        public Color statusText = new Color32(0x14, 0x15, 0x17, 0xFF);
        [Tooltip("계정과 무관한 중립 프로필 자리(보는 사람 자신의 입력 영역 등)")]
        public Color avatarPlaceholder = new Color32(0xE2, 0xE3, 0xE6, 0xFF);
        [Tooltip("프로필 사진 크게 보기의 어두운 덮개")]
        public Color viewerOverlay = new Color(0, 0, 0, 0.86f);
        public Color viewerText = Color.white;

        [Header("게시자 가림 스티커 (둥근 스마일, 브랜드·OS 이모지 아님)")]
        public Color stickerFill = new Color32(0xF3, 0xEA, 0xD8, 0xFF);
        [Tooltip("아주 약한 테두리")]
        public Color stickerRim = new Color32(0xD9, 0xCD, 0xB6, 0xFF);
        [Tooltip("눈·입")]
        public Color stickerInk = new Color32(0x4E, 0x44, 0x3B, 0xFF);
        [Tooltip("화면에 붙인 느낌의 아주 약한 그림자 불투명도")]
        [Range(0, 0.5f)] public float stickerShadow = 0.16f;

        [Header("계정 프로필 이미지 (계정명으로 정해지는 추상 무늬)")]
        [Tooltip("낮은 채도 색. 계정명 해시로 2~3개를 골라 단순한 도형으로 조합한다. 같은 계정은 어느 캡처에서나 같은 이미지.")]
        public Color[] avatarPalette =
        {
            new Color32(0xC9, 0xD3, 0xD6, 0xFF), new Color32(0xD8, 0xCF, 0xC4, 0xFF), new Color32(0xC7, 0xCE, 0xC0, 0xFF),
            new Color32(0xD5, 0xC9, 0xCF, 0xFF), new Color32(0xB9, 0xC2, 0xC9, 0xFF), new Color32(0xCF, 0xC5, 0xB5, 0xFF),
            new Color32(0xBF, 0xC8, 0xBC, 0xFF), new Color32(0xC6, 0xBE, 0xC8, 0xFF),
        };

        [Header("가져오기")]
        [Tooltip("캡처 텍스처 밉맵. 게임 화면처럼 크게 축소해 볼 때의 선명도를 비교해 정한다.")]
        public bool mipmaps = true;

        [Header("화면 구성")]
        [Tooltip("이 앱 화면의 기본 하단 고정 영역 (캡처 원본에서 바꿀 수 있다)")]
        public CaptureBottomBar defaultBottom = CaptureBottomBar.Navigation;

        [Header("모양")]
        public Sprite roundedSprite;
        public Sprite circleSprite;
    }
}
