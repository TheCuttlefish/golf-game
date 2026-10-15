using UnityEngine;
using UnityEngine.SceneManagement;

public class BallPhysics : MonoBehaviour
{
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
       
    }

    bool moveForward = false;
    bool moveBack = false;
    // Update is called once per frame
    void Update()
    {


        if (Input.GetKey(KeyCode.Space))
            rb.AddForce(Vector3.up * 4f, ForceMode.Acceleration);



        if (Input.GetKeyDown(KeyCode.Alpha1)) Time.timeScale = 0.6f;
        if (Input.GetKeyDown(KeyCode.Alpha2)) Time.timeScale = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) Time.timeScale = 5;



    

        moveForward = Input.GetKey(KeyCode.W);
        // rb.AddForce(forward.normalized * 1.2f, ForceMode.Acceleration);

        moveBack = Input.GetKey(KeyCode.S);
        



        Vector3 right = Camera.main.transform.right;
        right.y = 0f;
        right.Normalize();

        if (Input.GetKey(KeyCode.A))
            rb.AddForce(-right * 1f, ForceMode.Acceleration);

        if (Input.GetKey(KeyCode.D))
            rb.AddForce(right * 1f, ForceMode.Acceleration);



        if (Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

    }


    private void FixedUpdate()
    {
        if (moveForward)
        {
            // forward
            Vector3 forward = Camera.main.transform.forward;
            forward.y = 0f;
            rb.AddForce(forward.normalized * 10.2f, ForceMode.Acceleration);
        }


        if (moveBack)
        {

            // forward
            Vector3 forward = Camera.main.transform.forward;
            forward.y = 0f;
            rb.AddForce(forward.normalized * -10.2f, ForceMode.Acceleration);

        }
    }


   

    private void OnCollisionStay(Collision collision)
    {


        //slow down
       
        if (rb.linearVelocity.magnitude < .2f)
        {
            Vector3 velNoY = new Vector3(0, rb.linearVelocity.y, 0);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, velNoY, 1f - Mathf.Exp(-Time.fixedDeltaTime / 0.321f));
        }
            //if(rb.linearVelocity.magnitude < 0.01f) rb.isKinematic = true;

        
    }


}
