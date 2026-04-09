using System;
using UnityEngine;
using UnityEngine.UI;
using Starter.Platformer;


	/// <summary>
	/// 플레이어 부활 UI 관리
	/// </summary>
	public class RevivalUI : MonoBehaviour
	{
		[Header("UI References")]
		public Image progressBar;
		public Text revivalHintText;
		public CanvasGroup canvasGroup;

		[Header("Settings")]
		public float fadeSpeed = 5f;

		private bool _isVisible = false;
		private float _targetAlpha = 0f;

		private void Awake()
		{
			if (canvasGroup == null)
				canvasGroup = GetComponent<CanvasGroup>();

			if (canvasGroup != null)
				canvasGroup.alpha = 0f;
		}

		/// <summary>
		/// 부활 UI를 표시합니다
		/// </summary>
		public void Show(string deadPlayerName)
		{
			_isVisible = true;
			_targetAlpha = 1f;

			if (revivalHintText != null)
				revivalHintText.text = $"{deadPlayerName}을(를) 살리려면 E키를 누르세요";
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

		private void Update()
		{
			// 페이드 효과
			if (canvasGroup != null)
			{
				canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
			}
		}

		private void LateUpdate()
		{
			transform.rotation = Camera.main.transform.rotation;
		}
	}


