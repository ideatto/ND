using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialDialogueUI : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    public class DialogueLine
    {
        public string npcName;
        [TextArea(2, 5)] public string message;
        public Sprite npcSprite;
    }

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image panelImage;
    [SerializeField] private Image npcPortraitImage;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button nextButton;

    [Header("Dialogue")]
    [SerializeField] private List<DialogueLine> dialogueLines = new List<DialogueLine>();
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool hideWhenComplete = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onDialogueStarted;
    [SerializeField] private UnityEvent onDialogueCompleted;

    private int currentIndex = -1;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(ShowNextLine);
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartDialogue();
        }
        else
        {
            SetVisible(false);
        }
    }

    public void StartDialogue()
    {
        if (dialogueLines.Count == 0)
        {
            Debug.LogWarning("TutorialDialogueUI has no dialogue lines.", this);
            return;
        }

        isPlaying = true;
        currentIndex = -1;
        SetVisible(true);
        onDialogueStarted?.Invoke();
        ShowNextLine();
    }

    public void StartDialogue(List<DialogueLine> lines)
    {
        dialogueLines = lines ?? new List<DialogueLine>();
        StartDialogue();
    }

    public void ShowNextLine()
    {
        if (!isPlaying)
        {
            return;
        }

        currentIndex++;

        if (currentIndex >= dialogueLines.Count)
        {
            CompleteDialogue();
            return;
        }

        ApplyLine(dialogueLines[currentIndex]);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ShowNextLine();
    }

    private void ApplyLine(DialogueLine line)
    {
        if (npcNameText != null)
        {
            npcNameText.text = line.npcName;
        }

        if (messageText != null)
        {
            messageText.text = line.message;
        }

        if (npcPortraitImage != null)
        {
            npcPortraitImage.sprite = line.npcSprite;
            npcPortraitImage.enabled = line.npcSprite != null;
        }
    }

    private void CompleteDialogue()
    {
        isPlaying = false;

        if (hideWhenComplete)
        {
            SetVisible(false);
        }

        onDialogueCompleted?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
        }
    }
}
