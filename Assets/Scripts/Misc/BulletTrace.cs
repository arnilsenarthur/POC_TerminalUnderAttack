using UnityEngine;
namespace TUA.Misc
{
    public class BulletTrace : MonoBehaviour
    {
        [Header("Settings")]
        public float secondsPerUnit = 0.1f;
        [Header("References")]
        public LineRenderer lineRenderer;
        private float _elapsedTime;
        private Vector3 _start;
        private Vector3 _end;
        private float _lifetime;
        
        private void Update()
        {
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }
            var position = Vector3.Lerp(_start, _end, _elapsedTime / _lifetime);
            lineRenderer.SetPosition(0, position);
        }
        
        public void Initialize(Vector3 start, Vector3 end)
        {
            transform.position = start;
            _start = start;
            _end = end;
            _lifetime = Vector3.Distance(start, end) * secondsPerUnit;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            _elapsedTime = 0f;
        }
    }
}
