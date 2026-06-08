using UnityEngine;
using Fusion;
using RetroArsenal;
using UnityEngine.VFX;

namespace Projectiles
{
	// Common weapon base class for all basic examples
	public abstract class WeaponBase : NetworkBehaviour
	{
		// PUBLIC MEMBERS

		public Transform FireTransform => _fireTransform;

		// PRIVATE MEMBERS

		[SerializeField]
		private Transform _fireTransform;
		[SerializeField]
		private AudioClip _fireClip;
		[SerializeField]
		private Transform _fireSoundSourcesRoot;

		private AudioSource[] _fireSoundSources;
		public GameObject attackScope;
		public Animator Animator;
		public LineRenderer LineRenderer;
		public ParticleSystem ParticleEffect;
		public VisualEffect VisualEffect;

		protected virtual void Awake()
		{
			Animator = GetComponent<Animator>();
			TryGetComponent(out LineRenderer);
			//TryGetComponentInChildren(gameObject, out ParticleEffect);
			if (attackScope != null)
			{
				attackScope.transform.localPosition = Vector3.zero;
			}
		}
		
		public static bool TryGetComponentInChildren<T>(GameObject obj, out T component) where T : Component
		{
			component = obj.GetComponentInChildren<T>();
			return component != null;
		}

		// PUBLIC METHODS

		public abstract void Fire(float damage, float criticalDamage);

		// PROTECTED METHODS

		protected void PlayFireEffect()
		{
			// In multipeer mode fire sounds are played only for visible runner
			if (Runner.GetVisible() == false)
				return;

			if (_fireSoundSources == null)
			{
				_fireSoundSources = _fireSoundSourcesRoot.GetComponentsInChildren<AudioSource>();
			}

			// Find free audio source and play fire sound
			for (int i = 0; i < _fireSoundSources.Length; i++)
			{
				var source = _fireSoundSources[i];

				if (source.isPlaying == true)
					continue;

				source.clip = _fireClip;
				source.Play();
				return;
			}

			Debug.LogWarning("No free fire sound source", gameObject);
		}
	}
}
