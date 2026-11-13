using Pvz.Core;
using TMPro;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>End-of-shift statistics shown above the counter.</summary>
    public sealed class ShiftSummaryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text body;

        public bool IsVisible => root != null && root.activeSelf;
        public string Text => body != null ? body.text : string.Empty;

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void Show(ShiftStats stats, int ordersTotal, string recordLine = null)
        {
            var grade = ShiftRating.Grade(stats.OrdersCompleted, ordersTotal, stats.WrongHandovers, stats.OrdersAbandoned);
            if (body != null)
            {
                body.text =
                    "<b>СМЕНА ЗАВЕРШЕНА</b>\n" +
                    $"Обслужено клиентов: {stats.OrdersCompleted}/{ordersTotal}\n" +
                    $"Ошибочных выдач: {stats.WrongHandovers}\n" +
                    $"Неверных сканирований: {stats.WrongScans}\n" +
                    (stats.BoxesReceived > 0 ? $"Принято из поставки: {stats.BoxesReceived}\n" : string.Empty) +
                    (stats.BagsGiven > 0 ? $"Выдано пакетов: {stats.BagsGiven}\n" : string.Empty) +
                    (stats.OrdersAbandoned > 0 ? $"Ушли без заказа: {stats.OrdersAbandoned}\n" : string.Empty) +
                    $"Среднее время выдачи: {stats.AverageServiceSeconds:0.0} с\n" +
                    $"Очки: {stats.Score}\n" +
                    $"Рейтинг смены: <b>{GradeText(grade)}</b>" +
                    (string.IsNullOrEmpty(recordLine) ? string.Empty : $"\n{recordLine}");
            }
            if (root != null) root.SetActive(true);
        }

        public static string GradeText(ShiftGrade grade)
        {
            switch (grade)
            {
                case ShiftGrade.Excellent: return "Отлично";
                case ShiftGrade.Good: return "Хорошо";
                case ShiftGrade.Satisfactory: return "Удовлетворительно";
                default: return "Нужно потренироваться";
            }
        }

        internal void Configure(GameObject panelRoot, TMP_Text text)
        {
            root = panelRoot;
            body = text;
        }
    }
}
