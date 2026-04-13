using UnityEngine;

public class CameraShack : MonoBehaviour
{
    private float _shakeTimer;
    private float _shakeDuration;
    private float _shakeIntensity;
    private Vector3 _currentShakeOffset = Vector3.zero;

    private void Update()
    {
        if (_shakeTimer > 0)
        {
            _shakeTimer -= Time.deltaTime;

            // 랜덤한 흔들림 적용
            float randomX = Random.Range(-_shakeIntensity, _shakeIntensity);
            float randomY = Random.Range(-_shakeIntensity, _shakeIntensity);

            _currentShakeOffset = new Vector3(randomX, randomY, 0);

            // 흔들림이 끝나면 offset 초기화
            if (_shakeTimer <= 0)
            {
                _currentShakeOffset = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 카메라 흔들림의 현재 offset을 반환합니다
    /// </summary>
    public Vector3 GetShakeOffset()
    {
        return _currentShakeOffset;
    }

    /// <summary>
    /// 카메라를 흔듭니다
    /// </summary>
    /// <param name="duration">흔드는 지속 시간</param>
    /// <param name="intensity">흔드는 강도</param>
    public void Shake(float duration = 0.6f, float intensity = 0.6f)
    {
        _shakeTimer = duration;
        _shakeDuration = duration;
        _shakeIntensity = intensity;
    }
}


