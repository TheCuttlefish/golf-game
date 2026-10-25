using UnityEngine;
using UnityEngine.XR;

public class FollowBall : MonoBehaviour
{
    public Rigidbody ball;

    //-- main values
    public float height = 0.46f;
    public float distance = 0.46f;
    public float cameraOffsetY = 0f;
    public float lookOffsetY = 0f;

    //-- values for delay!
    float new_height = 0.2f; 
    float new_distance = 0.46f;
    float new_cameraOffsetY = 0f;
    float new_lookOffsetY = 0.08f;

    //--other settings
    public float followSpeed = 4;
    public float rotationSpeed = 4f;
    private Vector3 behind = Vector3.back;
    private float rotationResponse = 2.5f; // - 0.01 slow / 3++++ fast

    private void Awake()
    {
        transform.LookAt(ball.position);
    }


    public void GameView() // saved values Game View Mode
    {
        new_height = 0.2f;
        new_distance = 0.46f;
        new_cameraOffsetY = 0f;
        new_lookOffsetY = 0.08f;
    }

    public void WinView() // saved values Win View Mode
    {
        new_height = 0.77f;
        new_distance = 0.85f;
        new_cameraOffsetY = 0.54f;
        new_lookOffsetY = 0.68f;
    }


    private void Update() //smoothly follow the new values with different increments
    {
        height -= ( height - new_height) / 1.02f * Time.deltaTime;
        distance -= ( distance - new_distance) / 0.02f * Time.deltaTime;
        cameraOffsetY -= (cameraOffsetY - new_cameraOffsetY) / 0.02f * Time.deltaTime;
        lookOffsetY -= ( lookOffsetY - new_lookOffsetY) /1.02f * Time.deltaTime;
    }

    void LateUpdate()
    {
        Vector3 velocity = ball.linearVelocity; // get velocity of the ball
        velocity.y = 0f; // ingore Y
        if (velocity.sqrMagnitude > 0.25f) // stop rotating when movement is slow! (prevetnts stange angles when ball is slowing down)
            behind = Vector3.RotateTowards( behind, -velocity.normalized,rotationResponse * Time.deltaTime, 0f);// look at ball (with smooth delay)

        Vector3 targetPosition = ball.position + behind * distance + Vector3.up * (height + cameraOffsetY); // taget pos = (ball position + dinstace + cameraY offset)

        transform.position = Vector3.Lerp( transform.position, targetPosition, followSpeed * Time.deltaTime); // smooth movement

        Vector3 lookTarget = ball.position + Vector3.up * lookOffsetY; // look targt + offset on Y
        Vector3 lookDirection = lookTarget - transform.position; // look direction = (look target - position)

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection); // direction
        transform.rotation = Quaternion.Slerp( transform.rotation, targetRotation, 1f - Mathf.Exp( -rotationSpeed * Time.deltaTime)); // slow smooth rotation over time
    }
}