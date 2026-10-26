using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerMovementExample : MonoBehaviour
{

    private Rigidbody rb;
    private Transform cameraTransform;
    public Slider powerUI;
    
    float power;
    bool readyToShoot = true;
    float powerSpeed = 2;

    bool shoot = false;
    bool canShoot=false;


    void Awake()
    {

        rb = GetComponent<Rigidbody>(); // get rigidbody
        cameraTransform = Camera.main.transform; // save ref to camera transform
        powerUI = GameObject.Find("power-slider").GetComponent<Slider>();
    }

    public Vector3 oldPos;
    float timer;

    int waitingCounter = 0;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // reload current scene

        if(transform.position.y < - 10) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // reset if too low



        if (readyToShoot)
        {
            power = (Mathf.Cos(Time.time * powerSpeed) + 1)/2;
            powerUI.value = power;
        }


        if(timer == 0) oldPos = transform.position;
        timer += Time.deltaTime;


       
        if (timer > 0.1f)
        {
            if (transform.position == oldPos)
            {
                waitingCounter++;
            }
            else
            {
                waitingCounter = 0;
            }
            timer = 0;
        }

      
        //previous solution
        //if (rb.angularVelocity.magnitude < 0.01f) // if ball is not moving you can aim and shoot!
        if(waitingCounter > 3)//check 3 times x 0.1 seconds - if the pos is the same
        {
           
            canShoot = true;
            powerUI.GetComponent<CanvasGroup>().alpha = 1;

        }

        if (canShoot)
        {
            //orbit camera
            Camera.main.GetComponent<FollowBall>().AimAngle(Input.GetAxis("Horizontal") );


            // press space to shoot
             shoot = Input.GetKeyDown(KeyCode.Space);
            //calculate directions from camera transform
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f; // ingnore Y
            forward.Normalize();
            //move forward
            if (shoot) rb.AddForce(forward * 0.5f * power, ForceMode.Impulse);

            if (Input.GetKeyUp(KeyCode.Space))
            {
                Camera.main.GetComponent<FollowBall>().StoppedAiming();
                powerUI.GetComponent<CanvasGroup>().alpha = 0;
                waitingCounter = 0; // reset counter
                canShoot=false;
            }

        }
    }

 
    }
