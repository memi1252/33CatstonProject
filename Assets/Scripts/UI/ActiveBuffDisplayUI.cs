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
    public int maxSlots = 20;

    private readonly List<GameObject> _slots = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    public void AddBuff(Sprite icon, string buffName)
    {
        if (_slots.Count >= maxSlots) return;
        if (buffIconSlotPrefab == null || slotParent == null) return;

        var slot = Instantiate(buffIconSlotPrefab, slotParent);

        var img = slot.GetComponentInChildren<Image>();
        if (img != null && icon != null) img.sprite = icon;

        var txt = slot.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = "";

        _slots.Add(slot);
    }

    public void Clear()
    {
        foreach (var s in _slots)
            if (s != null) Destroy(s);
        _slots.Clear();
    }
}
