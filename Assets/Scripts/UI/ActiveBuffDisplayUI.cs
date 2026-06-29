using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HP바 아래에 수평으로 활성 버프 아이콘을 표시하는 UI.
/// slotParent에 Horizontal Layout Group을 붙이고, buffIconSlotPrefab을 연결한다.
/// </summary>
public class ActiveBuffDisplayUI : MonoBehaviour
{
    public static ActiveBuffDisplayUI Instance { get; private set; }

    [Header("슬롯 설정")]
    public GameObject buffIconSlotPrefab;
    public Transform slotParent;
    // 스테이지를 많이 진행하면 증강이 20개를 넘게 쌓일 수 있는데, 예전 기본값(20)을 넘기면
    // 조용히 무시되어 "증강이 몇 개는 안 보인다"는 버그로 이어졌다. 충분히 크게 올림.
    public int maxSlots = 60;

    private readonly List<GameObject> _slots = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    public void AddBuff(Sprite icon, string buffName) => AddBuff(icon, buffName, "");

    public void AddBuff(Sprite icon, string buffName, string description)
    {
        if (_slots.Count >= maxSlots)
        {
            Debug.LogWarning($"[ActiveBuffDisplayUI] maxSlots({maxSlots})를 넘어서 '{buffName}' 아이콘이 표시되지 않습니다.");
            return;
        }
        if (buffIconSlotPrefab == null || slotParent == null)
        {
            Debug.LogWarning($"[ActiveBuffDisplayUI] buffIconSlotPrefab 또는 slotParent가 비어있어 '{buffName}' 아이콘을 표시할 수 없습니다.");
            return;
        }

        var slot = Instantiate(buffIconSlotPrefab, slotParent);

        var img = slot.GetComponentInChildren<Image>();
        if (img != null && icon != null) img.sprite = icon;

        var txt = slot.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = "";

        // 마우스를 올리면 어떤 증강인지 툴팁으로 보여준다.
        var tooltip = slot.GetComponent<Starter.UI.UITooltipTrigger>();
        if (tooltip == null) tooltip = slot.AddComponent<Starter.UI.UITooltipTrigger>();
        tooltip.title = buffName;
        tooltip.body = description;

        _slots.Add(slot);
    }

    public void Clear()
    {
        foreach (var s in _slots)
            if (s != null) Destroy(s);
        _slots.Clear();
    }
}
