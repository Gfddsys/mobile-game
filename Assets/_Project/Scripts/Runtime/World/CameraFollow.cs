using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Side-scrolling camera: keeps the turtle in the left third of the screen,
    /// zooms out with speed and supports screen shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public float baseSize = 6.2f;
        public float maxExtraSize = 3.2f;

        Camera cam;
        Transform target;
        float shake;
        float velY;
        float size;
        Vector3 pos;

        public Camera Cam => cam;
        public float ViewHeight => cam.orthographicSize * 2f;
        public float ViewWidth => ViewHeight * cam.aspect;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = baseSize;
            size = baseSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PixelCanvas.C("#5ec2ff");
            pos = new Vector3(0, 2, -10);
            transform.position = pos;
        }

        public void SetTarget(Transform t, bool snap)
        {
            target = t;
            if (snap && t != null) Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            size = baseSize;
            cam.orthographicSize = size;
            pos = Desired(target.position);
            velY = 0f;
            transform.position = pos;
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        Vector3 Desired(Vector3 t)
        {
            float halfW = size * cam.aspect;
            return new Vector3(t.x + halfW * 0.38f, t.y + size * 0.22f, -10f);
        }

        void LateUpdate()
        {
            if (target == null) return;

            float speed = 0f;
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null) speed = rb.linearVelocity.magnitude;
            float targetSize = baseSize + Mathf.Clamp((speed - 9f) * 0.14f, 0f, maxExtraSize);
            size = Mathf.Lerp(size, targetSize, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime));
            cam.orthographicSize = size;

            var want = Desired(target.position);
            pos.x = Mathf.Lerp(pos.x, want.x, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
            pos.y = Mathf.SmoothDamp(pos.y, want.y, ref velY, 0.22f, Mathf.Infinity, Time.unscaledDeltaTime);

            var offset = Vector3.zero;
            if (shake > 0.001f)
            {
                offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shake;
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2.5f);
            }
            transform.position = pos + offset;
        }
    }
}
