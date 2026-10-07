using TMPro;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>색·글꼴·크기·아이콘 글리프. 아래 초기값은 테마 에셋을 처음 만들 때의 기본값이다.</summary>
    [CreateAssetMenu(menuName = "Intersection/UI/Theme", fileName = "Theme")]
    public class UITheme : ScriptableObject
    {
        [Header("Fonts")]
        public TMP_FontAsset regularFont;
        public TMP_FontAsset mediumFont;
        public TMP_FontAsset boldFont;
        [Tooltip("ID·해시·기술 로그 등 고정폭이 필요한 값에만 사용")]
        public TMP_FontAsset monoFont;

        [Header("Sprites")]
        public Sprite roundedSprite;
        public Sprite circleSprite;
        public Sprite avatarSprite;
        public Sprite mutedIcon;
        [Tooltip("이미지가 없는 사진 자리에 표시하는 일반 그림 기호")]
        public Sprite photoGlyph;
        public Sprite searchGlyph;
        public Sprite pageGlyph;
        public Sprite pinGlyph;
        public Sprite routeGlyph;
        public Sprite historyGlyph;
        public Sprite folderGlyph;
        public Sprite documentGlyph;
        [Tooltip("실제 지도 이미지가 없을 때 쓰는 중립 지도 자리 표시 (현실 지형 없음)")]
        public Sprite mapPlaceholder;

        [Header("Work program")]
        public Color background = Hex("12161B");
        public Color panel = Hex("171C22");
        public Color panelRaised = Hex("1E252D");
        public Color border = Hex("2A323C");
        public Color text = Hex("E4E8EC");
        public Color subText = Hex("8C96A1");
        public Color accent = Hex("7DB6C6");
        public Color accentSoft = Hex("1F3540");
        public Color stage = Hex("0D1014");

        [Header("Phone")]
        public Color phoneFrame = Hex("2B3036");
        public Color phoneScreen = Hex("000000");
        public Color phoneText = Hex("FFFFFF");
        public Color phoneSubText = Hex("8E8E93");
        public Color phoneSeparator = Hex("2C2C2E");
        public Color phoneAccent = Hex("0A84FF");
        public Color phoneField = Hex("1C1C1E");
        public Color bubbleOutgoing = Hex("30D158");
        public Color bubbleOutgoingText = Hex("FFFFFF");
        public Color bubbleIncoming = Hex("26262A");
        public Color bubbleIncomingText = Hex("FFFFFF");
        public Color attachmentPlaceholder = Hex("3A3A3C");
        public Color unreadDot = Hex("0A84FF");
        [Tooltip("이미지가 없는 사진의 자리 표시 기본색")]
        public Color photoPlaceholder = Hex("2C2C2E");
        public Color photoPlaceholderGlyph = Hex("5A5A5E");

        [Header("Phone sizes (1920x1080 기준 px)")]
        public float phoneTitleSize = 32f;
        public float phoneRowTitleSize = 18f;
        public float phoneRowPreviewSize = 16f;
        public float phoneMetaSize = 14f;
        public float bubbleTextSize = 19f;
        public float bubbleTimeSize = 12f;
        [Range(0.4f, 0.95f)] public float bubbleMaxWidthRatio = 0.72f;
        public Vector2 bubblePadding = new Vector2(14f, 9f);
        public Vector2 imageAttachmentSize = new Vector2(190f, 140f);
        public Vector2 locationAttachmentSize = new Vector2(210f, 120f);
        public float bubbleGroupGap = 3f;
        public float bubbleSenderGap = 12f;
        [Range(2, 6)] public int photoGridColumns = 4;
        public float photoGridSpacing = 2f;

        [Header("Glyphs")]
        public string backGlyph = "‹";
        public string chevronGlyph = "›";
        public string attachGlyph = "+";

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
