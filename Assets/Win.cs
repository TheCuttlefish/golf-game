using UnityEngine;

public class Win : MonoBehaviour
{

    public GameObject confettiPrefab;
    private GameObject confettiInstance;
    public FollowBall mainCam;

    private void Awake()
    {

        confettiInstance = Instantiate(confettiPrefab,transform.position, Quaternion.identity);
        mainCam = Camera.main.GetComponent<FollowBall>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            confettiInstance.gameObject.SetActive(true);
            mainCam.WinView();
        }

    }


    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
           
            mainCam.GameView();
        }
    }


}
