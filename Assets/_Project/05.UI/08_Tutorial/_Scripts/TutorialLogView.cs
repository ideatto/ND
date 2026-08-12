using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>Displays one tutorial chapter as a compact checklist.</summary>
    public sealed class TutorialLogView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private List<TMP_Text> stepTexts = new List<TMP_Text>();
        [SerializeField] private Color pendingColor = Color.white;
        [SerializeField] private Color currentColor = new Color32(255, 220, 92, 255);
        [SerializeField] private Color completedColor = new Color32(75, 155, 255, 255);
        [SerializeField] private string pendingPrefix = "○ ";
        [SerializeField] private string currentPrefix = "▶ ";
        [SerializeField] private string completedPrefix = "✓ ";

        private string[] labels = Array.Empty<string>();

        public void Configure(string title, IReadOnlyList<string> stepLabels)
        {
            if (titleText != null)
                titleText.text = title ?? string.Empty;

            int count = stepLabels?.Count ?? 0;
            labels = new string[count];
            for (int index = 0; index < count; index++)
                labels[index] = stepLabels[index] ?? string.Empty;

            Render(0);
        }

        public void Render(int completedStepCount)
        {
            int normalizedCompleted = Mathf.Clamp(completedStepCount, 0, labels.Length);
            for (int index = 0; index < stepTexts.Count; index++)
            {
                TMP_Text field = stepTexts[index];
                if (field == null)
                    continue;

                bool hasStep = index < labels.Length;
                field.gameObject.SetActive(hasStep);
                if (!hasStep)
                    continue;

                if (index < normalizedCompleted)
                {
                    field.text = completedPrefix + labels[index];
                    field.color = completedColor;
                }
                else if (index == normalizedCompleted)
                {
                    field.text = currentPrefix + labels[index];
                    field.color = currentColor;
                }
                else
                {
                    field.text = pendingPrefix + labels[index];
                    field.color = pendingColor;
                }
            }
        }

        public void RenderIndependent(IReadOnlyList<bool> completedStates)
        {
            for (int index = 0; index < stepTexts.Count; index++)
            {
                TMP_Text field = stepTexts[index];
                if (field == null)
                    continue;

                bool hasStep = index < labels.Length;
                field.gameObject.SetActive(hasStep);
                if (!hasStep)
                    continue;

                bool completed = completedStates != null
                    && index < completedStates.Count
                    && completedStates[index];
                field.text = (completed ? completedPrefix : currentPrefix) + labels[index];
                field.color = completed ? completedColor : currentColor;
            }
        }
    }
}
