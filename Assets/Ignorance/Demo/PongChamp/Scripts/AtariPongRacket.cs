using UnityEngine;
using Mirror;

namespace Ignorance.Examples.PongChamp
{
    public class AtariPongRacket : NetworkBehaviour
    {
        public float speed = 1500;
        private Rigidbody2D rigidbody2d;

        private void Awake()
        {
            rigidbody2d = GetComponent<Rigidbody2D>();
        }

        // need to use FixedUpdate for rigidbody
        void FixedUpdate()
        {
            // only let the local player control the racket.
            // don't control other player's rackets
            if (isLocalPlayer)
                rigidbody2d.velocity = new Vector2(0, Input.GetAxisRaw("Vertical")) * speed * Time.fixedDeltaTime;
        }
    }
}
