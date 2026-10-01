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
        public string DisplayName => type.ToString();
    }

    public enum PickupType { Coin, Part }

    public class Pickup : MonoBehaviour
    {
        public PickupType type;
        float baseY;
        float phase;

        void Start() { baseY = transform.position.y; phase = Random.value * 6f; }

        void Update()
        {
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

    /// <summary>Moves a bird left and flaps its wings.</summary>
    public class BirdFlyer : MonoBehaviour
    {
        public float speed = 3.5f;
        SpriteRenderer sr;
        float t;

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        void Update()
        {
            transform.position += Vector3.left * (speed * Time.deltaTime);
            t += Time.deltaTime;
            if (sr != null) sr.sprite = SpriteLibrary.Get(((int)(t * 8f) & 1) == 0 ? "bird_0" : "bird_1");
        }
    }

    /// <summary>A boulder sleeps until the player gets close, then rolls toward them.</summary>
    public class Boulder : MonoBehaviour
    {
        public float wakeDistance = 34f;
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
                rb.linearVelocity = new Vector2(-4f, 0f);
                rb.angularVelocity = 120f;
            }
        }
    }
}
