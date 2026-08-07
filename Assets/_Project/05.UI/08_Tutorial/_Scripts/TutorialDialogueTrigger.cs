using System.Collections.Generic;
using UnityEngine;

public class TutorialDialogueTrigger : MonoBehaviour
{
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private List<TutorialDialogueUI.DialogueLine> dialogueLines = new List<TutorialDialogueUI.DialogueLine>();

    private void OnMouseDown()
    {
        if (dialogueUI == null)
        {
            Debug.LogWarning("TutorialDialogueTrigger needs a TutorialDialogueUI reference.", this);
            return;
        }

        dialogueUI.StartDialogue(dialogueLines);
    }
}
