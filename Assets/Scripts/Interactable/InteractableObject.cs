using TMPro;
using UnityEngine;


// 注意这里：继承 MonoBehaviour 的同时，实现 IInteractable 接口


public class InteractableObject : MonoBehaviour
{

    

    public float promptRange = 3f;
    public float promptHeight = 2.2f;
    public float promptOffsetX = 0f;
    public string promptText = "[E]";
    public TMP_FontAsset promptFont;
    public bool promptWorldSpace = false;
    private Transform promptCanvas;
    private Transform player;

    void Awake()
    {
        OnBeforeCreatePrompt();
        CreatePrompt();
        player = GameObject.FindGameObjectWithTag(Tag.PLAYER).transform;
    }

    protected virtual void OnBeforeCreatePrompt() { }

    void CreatePrompt()
    {
        GameObject canvasGo = new GameObject("InteractPrompt");
        canvasGo.transform.SetParent(transform);
        Vector3 lossy = transform.lossyScale;
        canvasGo.transform.localPosition = new Vector3(
            promptOffsetX / lossy.x,
            promptHeight / lossy.y,
            0
        );
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(canvasGo.transform);
        textGo.transform.localPosition = Vector3.zero;
        textGo.transform.localScale = Vector3.one;
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = promptText;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        if (promptFont == null)
            promptFont = TMP_Settings.defaultFontAsset;
        if (promptFont != null)
            tmp.font = promptFont;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;

        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(10, 2);

        promptCanvas = canvasGo.transform;
        // 补偿父物体缩放，保证标签世界大小一致
        promptCanvas.localScale = new Vector3(
            0.008f / lossy.x,
            0.008f / lossy.y,
            0.008f / lossy.z
        );
        canvasGo.SetActive(false);
    }

    void Update()
    {
        if (player == null || promptCanvas == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        promptCanvas.gameObject.SetActive(dist <= promptRange);
    }

    void LateUpdate()
    {
        if (promptCanvas != null && promptCanvas.gameObject.activeSelf)
            promptCanvas.rotation = Camera.main.transform.rotation;
    }

    public void OnClick(UnityEngine.AI.NavMeshAgent agent) { }

    public void TriggerInteract()
    {
        Interact();
    }

    public virtual void Interact()
    {
        print("Interacting with Interactable Object");
    }
}
