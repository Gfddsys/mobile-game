using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>Physics layers used by the game (layer 8 is an unnamed user layer; name it "Rail" if you like).</summary>
    public static class Layers
    {
        public const int Rail = 8;
        public const int RailMask = 1 << Rail;
    }

    public enum HazardType { Cone, Pothole, Sign, Cable, Bird, Boulder }

    /// <summary>
    /// Marks an obstacle. Ground hazards hurt the board/wheels, Air hazards hurt
    /// the turtle's head (and can be dodged by ducking with Pitch Down).
    /// Heavy hazards cannot be smashed through, even with Super Boost.
    /// </summary>
    public class Hazard : MonoBehaviour
    {
        public HazardType type;
        public bool isAir;
        public bool heavy;
        public bool closeCallCounted;
        public string DisplayName => type.ToString();
    }

    public enum PickupType { Coin, Part, Magnet, Fuel, Shield }

    public class Pickup : MonoBehaviour
    {
        public PickupType type;
        public bool collected;
        float baseY;
        float phase;
        bool magnetized;

        void Start() { baseY = transform.position.y; phase = Random.value * 6f; }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Player != null && gm.Player.Alive && gm.Player.MagnetActive &&
                (type == PickupType.Coin || type == PickupType.Part))
            {
                Vector3 target = gm.Player.transform.position + Vector3.up * 0.45f;
                float dist = Vector2.Distance(transform.position, target);
                if (dist < 8.5f) magnetized = true;
                if (magnetized)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, 24f * Time.deltaTime);
                    return;
                }
            }

            var p = transform.position;
            p.y = baseY + Mathf.Sin(Time.time * 4f + phase) * 0.06f;
            transform.position = p;
        }
    }

    /// <summary>Grind rail. Wheels touching it earn grind score.</summary>
    public class Rail : MonoBehaviour { }

    /// <summary>Marker for terrain colliders so the player can tell ground from other stuff.</summary>
    public class GroundSurface : MonoBehaviour { }

    /// <summary>Destroys the object once the camera has left it far behind.</summary>
    public class Despawner : MonoBehaviour
    {
        public static float CutoffX = float.NegativeInfinity;

        void Update()
        {
            if (transform.position.x < CutoffX) Destroy(gameObject);
        }
    }

    /// <summary>Moves a bird left and flaps its wings once the player is nearby, tracking ground height so it never drifts into impossible heights.</summary>
    public class BirdFlyer : MonoBehaviour
    {
        public float speed = 2.3f;
        public float wakeDistance = 26f;
        SpriteRenderer sr;
        float t;
        float hoverHeight = 1.9f;
        bool initialized;

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.World != null)
            {
                float gy = GameManager.Instance.World.HeightAt(transform.position.x);
                hoverHeight = Mathf.Clamp(transform.position.y - gy, 1.68f, 2.25f);
                initialized = true;
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            if (sr != null) sr.sprite = SpriteLibrary.Get(((int)(t * 8f) & 1) == 0 ? "bird_0" : "bird_1");

            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.World == null) return;
            if (!initialized)
            {
                hoverHeight = Mathf.Clamp(transform.position.y - gm.World.HeightAt(transform.position.x), 1.68f, 2.25f);
                initialized = true;
            }

            float dx = transform.position.x - gm.Player.transform.position.x;
            if (dx > wakeDistance) return;

            var p = transform.position;
            p.x -= speed * Time.deltaTime;
            float targetY = gm.World.HeightAt(p.x) + hoverHeight;
            p.y = Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.position = p;
        }
    }

    /// <summary>A boulder sleeps until the player gets close, then rolls toward them.</summary>
    public class Boulder : MonoBehaviour
    {
        public float wakeDistance = 26f;
        Rigidbody2D rb;
        bool awake;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.simulated = false;
        }

        void Update()
        {
            if (awake || GameManager.Instance == null || GameManager.Instance.Player == null) return;
            float dx = transform.position.x - GameManager.Instance.Player.transform.position.x;
            if (dx < wakeDistance)
            {
                awake = true;
                rb.simulated = true;
                rb.linearVelocity = new Vector2(-2.5f, 0f);
                rb.angularVelocity = 90f;
            }
        }
    }
}
