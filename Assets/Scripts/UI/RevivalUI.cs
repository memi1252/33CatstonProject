using System;
using UnityEngine;
using UnityEngine.UI;
using Starter.Platformer;
using TMPro;

/// <summary>
/// 플레이어 부활 UI 관리
/// </summary>
public class RevivalUI : MonoBehaviour
{
	[Header("UI References")]
	public Image progressBar;
	public TextMeshProUGUI revivalHintText;
	public GameObject keyImage;

	/// <summary>
	/// 다른 플레이어를 부활시킬 수 있을 때 표시합니다 (E키 안내)
	/// </summary>
	public void Show(string deadPlayerName)
	{
		gameObject.SetActive(true);
		SoundManager.Instance?.PlayRevivalPrompt();
		if (keyImage != null) keyImage.SetActive(true);
		if (revivalHintText != null)
		{
			revivalHintText.text = $"{deadPlayerName}을(를) 살리려면 E키를 누르세요";
			revivalHintText.color = Color.white;
		}
	}

	/// <summary>
	/// 본인이 죽었을 때 표시합니다 (혼자서는 부활 불가 안내)
	/// </summary>
	public void PlayerDieShow()
	{
		gameObject.SetActive(true);
		if (keyImage != null) keyImage.SetActive(false);
		if (revivalHintText != null)
		{
			revivalHintText.text = "혼자서는 부활할수 없습니다!";
			revivalHintText.color = Color.red;
		}
	}

	/// <summary>
	/// 다른 플레이어가 죽었을 때(본인도 죽은 상태) 표시합니다
	/// </summary>
	public void OtherPlayerDie()
	{
		gameObject.SetActive(true);
		if (keyImage != null) keyImage.SetActive(false);
		if (revivalHintText != null)
		{
			revivalHintText.text = "죽은 상태로는 살릴수가 없습니다";
			revivalHintText.color = Color.red;
		}
	}

	public void Hide()
	{
		gameObject.SetActive(false);
	}

	/// <summary>
	/// 부활 진행 상태 바를 업데이트합니다
	/// </summary>
	public void SetProgress(float progress)
	{
		if (progressBar != null)
			progressBar.fillAmount = Mathf.Clamp01(progress);
	}

	private void LateUpdate()
	{
		if (Camera.main != null)
			transform.rotation = Camera.main.transform.rotation;
	}
}
