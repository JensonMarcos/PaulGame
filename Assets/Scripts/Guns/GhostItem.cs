using UnityEngine;

public class GhostItem : MonoBehaviour
{
    Rigidbody rb;
    public GameObject realItem;
    [SerializeField] float speed = 1.5f, increaseSpeed = 10f;
    public Transform muzzleTrans;
    public Transform rhand, lhand;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update() {
        if(realItem == null) return;
        //rb.AddForce(((realItem.transform.position - transform.position).normalized * speed) - rb.linearVelocity, ForceMode.VelocityChange);
        rb.MovePosition(Vector3.Lerp(transform.position, realItem.transform.position, speed * Time.deltaTime));
        rb.MoveRotation(Quaternion.Lerp(transform.rotation, realItem.transform.rotation, speed * Time.deltaTime));
        speed += speed*increaseSpeed * Time.deltaTime;

        if(Vector3.Distance(transform.position, realItem.transform.position) < 0.1f) {
            realItem.GetComponent<Item>().model.SetActive(true);
        }

        if(realItem.GetComponent<Item>().model.activeSelf) {
            Destroy(gameObject);
        }
    }

    // void OnCollisionEnter(Collision collision) {
    //     if(rb.isKinematic == true) return;
    //     if(collision.gameObject.layer != LayerMask.NameToLayer("Default")) return;
    //     realItem.GetComponent<MeshRenderer>().enabled = true;
    //     Destroy(gameObject);
    // }
}
