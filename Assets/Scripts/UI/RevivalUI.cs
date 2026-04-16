	/// 부활 UI를 표시합니다 (가능 상태)
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

	

		private bool _isVisible = false;
		private float _targetAlpha = 0f;

		private void Awake()
		{
		}

		/// <summary>
		/// 부활 UI를 표시합니다
		/// </summary>
		public void Show(string deadPlayerName)
		{
			_isVisible = true;
			_targetAlpha = 1f;
			keyImage.SetActive(true);
			if (revivalHintText != null)
			{
				revivalHintText.text = $"{deadPlayerName}을(를) 살리려면 E키를 누르세요";
				revivalHintText.color = Color.white;
			}
		}

		public void PlayerDieShow()
		{
			_isVisible = true;
			_targetAlpha = 1f;
			keyImage.SetActive(false);
			if (revivalHintText != null)
			{
				revivalHintText.text = "혼자서는 부활할수 없습니다!";
				revivalHintText.color = Color.red;
			}
		}

		public void OtherPlayerDie()
		{
			_isVisible = true;
			_targetAlpha = 1f;
			keyImage.SetActive(false);
			if (revivalHintText != null)
			{
				revivalHintText.text = "죽은 상태로는 살릴수가 없습니다";
				revivalHintText.color = Color.red;
			}
		}

		/// <summary>
		/// 부활 UI를 숨깁니다
		/// </summary>
		public void Hide()
		{
			_isVisible = false;
			_targetAlpha = 0f;
		}

		/// <summary>
		/// 진행 상태 바를 업데이트합니다
		/// </summary>
		public void SetProgress(float progress)
		{
			if (progressBar != null)
				progressBar.fillAmount = Mathf.Clamp01(progress);
		}

		private void LateUpdate()
		{
			transform.rotation = Camera.main.transform.rotation;
		}
	}


