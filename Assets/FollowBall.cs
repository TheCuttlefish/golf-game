using UnityEngine;
using UnityEngine.XR;

public class FollowBall : MonoBehaviour
{
    public Rigidbody ball;
    public float height = 0.46f;
    public float distance = 0.46f;
    public float followSpeed = 4;
    public float rotationSpeed = 4f;
    private Vector3 behind = Vector3.back;
    private float rotationResponse = 2.5f; // - 0.01 slow / 3++++ fast
    public float cameraOffsetY = 0f;
    public float lookOffsetY = 0f;



    //---------
    float new_height = 0.2f; 
    float new_distance = 0.46f;
    float new_cameraOffsetY = 0f;
    float new_lookOffsetY = 0.08f;

    private void Awake()
    {
        transform.LookAt(ball.position);
    }


    public void GameView()
    {
        new_height = 0.2f;
        new_distance = 0.46f;
        new_cameraOffsetY = 0f;
        new_lookOffsetY = 0.08f;
        
    }

    public void WinView()
    {
        new_height = 0.77f;
        new_distance = 0.85f;
        new_cameraOffsetY = 0.54f;
        new_lookOffsetY = 0.68f;
    }


    private void Update()
    {
        height -= (height - new_height) / 1.02f * Time.deltaTime;
        distance -= (distance - new_distance) / 0.02f * Time.deltaTime;
        cameraOffsetY -= (cameraOffsetY - new_cameraOffsetY) / 0.02f * Time.deltaTime;
        lookOffsetY -= (lookOffsetY - new_lookOffsetY) /1.02f * Time.deltaTime;
        
    }

    void LateUpdate()
    {
        Vector3 velocity = ball.linearVelocity;
        velocity.y = 0f;

        // Changed these lines:
        if (velocity.sqrMagnitude > 0.25f)
            behind = Vector3.RotateTowards( behind, -velocity.normalized,rotationResponse * Time.deltaTime, 0f);

        Vector3 targetPosition =
    ball.position + behind * distance
    + Vector3.up * (height + cameraOffsetY);

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        Vector3 lookTarget = ball.position + Vector3.up * lookOffsetY;
        Vector3 lookDirection = lookTarget - transform.position;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime)
            );
        }
    }
}