using UnityEngine;

public class ServerCollider : MonoBehaviour
{
    [SerializeField] PlayerCharacter playerCharacter;
    [SerializeField] float lerpSpeed = 5f;
    CapsuleCollider col;
    Rigidbody rb;
    Stance stance;

    public void Initialize(bool isServer)
    {
        if(!isServer)
        {
            gameObject.SetActive(false);
            return;
        } 

        col = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();

        SetCapsuleDimensions(playerCharacter.standHeight);
    }

    public void UpdateCollider(Stance _stance, Vector3 vel)
    {
        if(_stance != stance) {
            
            SetCapsuleDimensions(_stance == Stance.Crouch ? playerCharacter.crouchHeight : playerCharacter.standHeight);
            stance = _stance;
        }

        
        rb.linearVelocity = vel;
        
        rb.position = Vector3.Lerp(rb.position, playerCharacter.transform.position, Time.fixedDeltaTime * lerpSpeed);
        rb.rotation = playerCharacter.transform.rotation;
        
        //print(rb.linearVelocity);
    }
    
    public void SetCapsuleDimensions(float height)
    {
        col.height = height;
        col.center = new Vector3(0, height/2f, 0);
    }
}
