using System.Collections;
using UnityEngine;

namespace Gymageddon.Entities
{
    /// <summary>
    /// Attached at runtime to each damage-number GameObject.
    /// Drives the rise-and-fade animation on the popup's own MonoBehaviour so
    /// the coroutine is never stopped by <c>StopAllCoroutines()</c> called on
    /// the unit that spawned it.  Uses <see cref="Time.unscaledDeltaTime"/> so
    /// the animation runs even when <c>Time.timeScale</c> is 0 (paused).
    /// </summary>
    [AddComponentMenu("")]          // hide from Add Component menu
    internal sealed class DamagePopup : MonoBehaviour
    {
        private TextMesh _text;
        private Color    _startColor;
        private float    _duration;
        private float    _riseSpeed;

        /// <summary>
        /// Initialises and starts the popup animation immediately.
        /// Must be called once, right after the component is added.
        /// </summary>
        public void Init(TextMesh text, Color startColor, float duration, float riseSpeed)
        {
            _text       = text;
            _startColor = startColor;
            _duration   = duration;
            _riseSpeed  = riseSpeed;
            StartCoroutine(AnimateRoutine());
        }

        private IEnumerator AnimateRoutine()
        {
            float t = 0f;
            while (t < _duration && _text != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / _duration);
                transform.position += Vector3.up * (_riseSpeed * Time.unscaledDeltaTime);
                Color c = _startColor;
                c.a = 1f - k;
                _text.color = c;
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
