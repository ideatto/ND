using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialDialogueUI : MonoBehaviour, IPointerClickHandler
{
    public enum DialogueEmotion
    {
        Normal = 0,
        Happy = 1,
        Angry = 2,
        Sad = 3
    }

    [Serializable]
    private sealed class EmotionSpriteSet
    {
        public DialogueEmotion emotion = DialogueEmotion.Normal;
        public List<Sprite> sprites = new List<Sprite>();
    }

    [Serializable]
    public class DialogueLine
    {
        public string npcName;
        [TextArea(2, 5)] public string message;
        public DialogueEmotion emotion;
        [Tooltip("Used only when no sprite is configured for the selected emotion.")]
        public Sprite npcSprite;
        public Sprite illustrationSprite;
    }

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image panelImage;
    [SerializeField] private Image npcPortraitImage;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button nextButton;

    [Header("Modal Illustration")]
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private GameObject modalBlocker;
    [SerializeField] private GameObject illustrationPanel;
    [SerializeField] private Image illustrationImage;

    [Header("Dialogue")]
    [SerializeField] private List<DialogueLine> dialogueLines = new List<DialogueLine>();
    [SerializeField] private List<EmotionSpriteSet> emotionSpriteSets = new List<EmotionSpriteSet>();
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool hideWhenComplete = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onDialogueStarted;
    [SerializeField] private UnityEvent onDialogueCompleted;

    private int currentIndex = -1;
    private bool isPlaying;
    private Sprite previousEmotionSprite;

    public bool IsPlaying => isPlaying;
    public event Action DialogueCompleted;

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
        else if (!isPlaying)
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
        previousEmotionSprite = null;
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
            npcPortraitImage.sprite = SelectEmotionSprite(line.emotion) ?? line.npcSprite;
            npcPortraitImage.enabled = npcPortraitImage.sprite != null;
        }

        SetIllustration(line.illustrationSprite);
    }

    private Sprite SelectEmotionSprite(DialogueEmotion emotion)
    {
        EmotionSpriteSet set = emotionSpriteSets.Find(candidate => candidate.emotion == emotion);
        if (set == null || set.sprites == null)
            return null;

        var candidates = new List<Sprite>();
        foreach (Sprite sprite in set.sprites)
        {
            if (sprite != null)
                candidates.Add(sprite);
        }

        if (candidates.Count == 0)
            return null;

        int selectedIndex = UnityEngine.Random.Range(0, candidates.Count);
        if (candidates.Count > 1 && candidates[selectedIndex] == previousEmotionSprite)
        {
            int offset = UnityEngine.Random.Range(1, candidates.Count);
            selectedIndex = (selectedIndex + offset) % candidates.Count;
        }

        previousEmotionSprite = candidates[selectedIndex];
        return previousEmotionSprite;
    }

    private void CompleteDialogue()
    {
        isPlaying = false;

        if (hideWhenComplete)
        {
            SetVisible(false);
        }

        onDialogueCompleted?.Invoke();
        DialogueCompleted?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        if (visible && presentationRoot != null)
            presentationRoot.transform.SetAsLastSibling();

        if (modalBlocker != null)
            modalBlocker.SetActive(visible);

        if (!visible)
            SetIllustration(null);

        if (root != null)
        {
            root.SetActive(visible);
        }
    }

    private void SetIllustration(Sprite sprite)
    {
        if (illustrationImage != null)
        {
            illustrationImage.sprite = sprite;
            illustrationImage.enabled = sprite != null;
        }

        if (illustrationPanel != null)
            illustrationPanel.SetActive(sprite != null);
    }
}
