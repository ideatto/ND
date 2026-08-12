using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialChapter_", menuName = "ND/Tutorial/Chapter Data")]
    public sealed class TutorialChapterData : ScriptableObject
    {
        [Serializable]
        public sealed class StepPresentation
        {
            [SerializeField] private string stepId;
            [SerializeField, TextArea(1, 3)] private string label;

            public string StepId => stepId;
            public string Label => label;
        }

        [Header("Identity")]
        [SerializeField] private string chapterId;
        [SerializeField] private string title;
        [SerializeField, HideInInspector] private int presentationVersion;

        [Header("Presentation")]
        [SerializeField] private List<TutorialDialogueUI.DialogueLine> introDialogue = new();
        [SerializeField] private List<TutorialDialogueUI.DialogueLine> failureDialogue = new();
        [SerializeField] private List<StepPresentation> steps = new();
        [SerializeField] private List<TutorialDialogueUI.DialogueLine> completionDialogue = new();

        public string ChapterId => chapterId;
        public string Title => title;
        public int PresentationVersion => presentationVersion;
        public IReadOnlyList<TutorialDialogueUI.DialogueLine> IntroDialogue => introDialogue;
        public IReadOnlyList<TutorialDialogueUI.DialogueLine> FailureDialogue => failureDialogue;
        public IReadOnlyList<StepPresentation> Steps => steps;
        public IReadOnlyList<TutorialDialogueUI.DialogueLine> CompletionDialogue => completionDialogue;
    }
}
