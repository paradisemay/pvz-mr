using TMPro;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Big display above the counter: current order, hints and score.</summary>
    public sealed class CounterBoard : MonoBehaviour
    {
        public static readonly Color Neutral = new Color(0.92f, 0.94f, 0.97f);
        public static readonly Color Good = new Color(0.35f, 0.9f, 0.45f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.35f);
        public static readonly Color Accent = new Color(1f, 0.8f, 0.3f);

        [SerializeField] private TMP_Text headline;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text score;

        public string Headline => headline != null ? headline.text : string.Empty;
        public string Status => status != null ? status.text : string.Empty;

        public void ShowHeadline(string text, Color color)
        {
            if (headline == null) return;
            headline.text = text;
            headline.color = color;
        }

        public void ShowStatus(string text, Color color)
        {
            if (status == null) return;
            status.text = text;
            status.color = color;
        }

        [Tooltip("Optional: the order list next to the board.")]
        [SerializeField] private TMP_Text orders;

        public string Orders => orders != null ? orders.text : string.Empty;

        /// <summary>The order list (rich text); hidden when empty.</summary>
        public void ShowOrders(string text)
        {
            if (orders == null) return;
            orders.text = text ?? string.Empty;
            var panel = orders.transform.parent;
            if (panel != null && panel != transform) panel.gameObject.SetActive(!string.IsNullOrEmpty(orders.text));
        }

        internal void ConfigureOrders(TMP_Text orderList) => orders = orderList;

        /// <param name="clock">Time left in the shift («3:12»), or null when the shift has no time limit.</param>
        public void ShowScore(int points, int served, int total, string clock = null)
        {
            if (score == null) return;
            score.text = $"Очки: {points}    Выдано: {served}/{total}" + (clock != null ? $"    Время: {clock}" : string.Empty);
        }

        internal void Configure(TMP_Text headlineText, TMP_Text statusText, TMP_Text scoreText)
        {
            headline = headlineText;
            status = statusText;
            score = scoreText;
        }
    }
}
