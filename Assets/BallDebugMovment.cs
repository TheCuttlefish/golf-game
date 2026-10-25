using UnityEngine;
using UnityEngine.SceneManagement;

public class BallDebugMovment : MonoBehaviour
{
    private Rigidbody rb;
    private Transform cameraTransform;

    //movement variables
    private bool moveForward;
    private bool moveBack;
    private bool hover;
    private bool turnLeft;
    private bool turnRight;

    void Awake() // this fucntion is called before the first frame
    {
        rb = GetComponent<Rigidbody>(); // get rigidbody
        cameraTransform = Camera.main.transform; // save ref to camera transform
    }

    void Update() //update every frame/ game calculations and inputs
    {

        //change world time scale
        if (Input.GetKeyDown(KeyCode.Alpha1)) Time.timeScale = 0.6f;
        if (Input.GetKeyDown(KeyCode.Alpha2)) Time.timeScale = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) Time.timeScale = 5;

        //input for movement
        hover = Input.GetKey(KeyCode.Space);   // hover / jump
        moveForward = Input.GetKey(KeyCode.W); // forward
        moveBack = Input.GetKey(KeyCode.S);    // back
        turnLeft = Input.GetKey(KeyCode.A);    // left
        turnRight = Input.GetKey(KeyCode.D);   // right

        // reset scene & tiem scale
        if (Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

    }

    
    private void FixedUpdate() // physics calculationd
    {
        //calculate directions from camera transform
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f; // ingnore Y
        forward.Normalize();

        Vector3 right = cameraTransform.right;
        right.y = 0f;// ingnore Y
        right.Normalize();

        //move forward
        if (moveForward) rb.AddForce(forward * 10.2f, ForceMode.Acceleration);

        //move back
        if (moveBack) rb.AddForce(forward * -10.2f, ForceMode.Acceleration);

        if (turnLeft) rb.AddForce(right * -7f, ForceMode.Acceleration);
        if (turnRight) rb.AddForce(right * 7f, ForceMode.Acceleration);

        // hover / jump
        if (hover) rb.AddForce(Vector3.up * 20f, ForceMode.Acceleration);

    }
    

}
