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
        /// <summary>Something that is over and no longer matters (e.g. a customer who left the line).</summary>
        public static readonly Color Muted = new Color(0.55f, 0.59f, 0.65f);

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
        [Tooltip("Optional: the panel of the order list, hidden while the list is empty (never the board itself).")]
        [SerializeField] private GameObject ordersPanel;

        public string Orders => orders != null ? orders.text : string.Empty;

        /// <summary>The order list (rich text); its panel is hidden when the list is empty.</summary>
        public void ShowOrders(string text)
        {
            if (orders == null) return;
            orders.text = text ?? string.Empty;
            if (ordersPanel != null && ordersPanel != gameObject && !transform.IsChildOf(ordersPanel.transform))
                ordersPanel.SetActive(!string.IsNullOrEmpty(orders.text));
        }

        internal void ConfigureOrders(TMP_Text orderList, GameObject panel)
        {
            orders = orderList;
            ordersPanel = panel;
        }

        /// <summary>A free text on the bottom line (before the shift: the career).</summary>
        public void ShowFooter(string text)
        {
            if (score != null) score.text = text ?? string.Empty;
        }

        public string Footer => score != null ? score.text : string.Empty;

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
