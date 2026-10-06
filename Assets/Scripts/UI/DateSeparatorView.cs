using TMPro;
using UnityEngine;

namespace Intersection.UI
{
    public class DateSeparatorView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        public void Bind(string text) => label.text = text;
    }
}
