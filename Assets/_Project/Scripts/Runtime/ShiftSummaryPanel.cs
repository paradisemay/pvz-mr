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

        public void Show(ShiftStats stats, int ordersTotal)
        {
            var grade = ShiftRating.Grade(stats.OrdersCompleted, ordersTotal, stats.WrongHandovers);
            if (body != null)
            {
                body.text =
                    "<b>СМЕНА ЗАВЕРШЕНА</b>\n" +
                    $"Обслужено клиентов: {stats.OrdersCompleted}/{ordersTotal}\n" +
                    $"Ошибочных выдач: {stats.WrongHandovers}\n" +
                    $"Неверных сканирований: {stats.WrongScans}\n" +
                    $"Среднее время выдачи: {stats.AverageServiceSeconds:0.0} с\n" +
                    $"Очки: {stats.Score}\n" +
                    $"Рейтинг смены: <b>{GradeText(grade)}</b>";
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
