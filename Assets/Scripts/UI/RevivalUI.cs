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

public void Show() { gameObject.SetActive(true); SoundManager.Instance?.PlayRevivalPrompt(); }

		
		public void Hide()
		{
			gameObject.SetActive(false);
		}

		private void LateUpdate()
		{
			transform.rotation = Camera.main.transform.rotation;
		}
	}


