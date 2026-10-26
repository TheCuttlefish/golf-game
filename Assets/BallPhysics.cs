using UnityEngine;


public class BallPhysics : MonoBehaviour
{
    private Rigidbody rb;
    void Awake() // this fucntion is called before the first frame
    {
        rb = GetComponent<Rigidbody>(); // get rigidbody
    }

    private void OnCollisionStay(Collision collision) // when object is constantly colliding with another object
    {

        //slow down - if movement lower than 0.2f just slow down anyway!
        if (rb.linearVelocity.magnitude < .1f)
        {
            Vector3 velNoY = new Vector3(0, rb.linearVelocity.y, 0);// ingore Y (when we slow down we only want to slow down on X and Z)
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, velNoY, 1f - Mathf.Exp(-Time.fixedDeltaTime / 0.321f)); // slow down curve
        }

        //this gets rid of the jitter when the ball cannot slow down on a small slope (and keeps rolling / jittering)
        if (rb.linearVelocity.magnitude < .02f) rb.linearVelocity = -rb.linearVelocity; // if extremly small movement - slow it down by revesting veolcity

        



    }

}
