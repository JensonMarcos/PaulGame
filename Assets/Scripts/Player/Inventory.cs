using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Inventory : NetworkBehaviour
{
    public GameObject[] inventory = new GameObject[3];
    public GameObject[] clientInventory = new GameObject[3];
    public int activeGun;

    public Transform view, gunHolder, gunHolderThirdPerson;
    [SerializeField] float grabDistance, grabWidth, throwStength;
    [SerializeField] LayerMask itemLayer;

    public GameObject itemReal, itemGhost;

    Shooting shooting;

    void Start()
    {
        shooting = GetComponent<Shooting>();
    }

    void Update() {
        if(!IsOwner) return;

        // for (int i = 0; i < inventory.Length; i++) {
        //     if(inventory[i] == null) continue;
        //     inventory[i].transform.localPosition = transform.InverseTransformPoint(gunHolder.position);
        //     inventory[i].transform.localRotation = Quaternion.Inverse(transform.rotation) * gunHolder.rotation;
        // }


        //weapon switching
        int prevWeapon = activeGun;
        if(Input.GetAxis("Mouse ScrollWheel") < 0f) { //scrollwheel
            if(activeGun >= inventory.Length - 1) activeGun = 0;
            else activeGun++;
        }
        if(Input.GetAxis("Mouse ScrollWheel") > 0f) {
            if(activeGun <= 0) activeGun = inventory.Length - 1;
            else activeGun--;
        }
        for (int i = 0; i < inventory.Length; i++) //keys
        {
            if(Input.GetKeyDown((i+1).ToString())) {
                activeGun = i;
            }
        }
        if(prevWeapon != activeGun) select(activeGun);


        PickUp();
        if(Input.GetButtonDown("Drop") && inventory[activeGun] != null) {
            Drop(activeGun, true);
        }
    }

    void PickUp() {
        if(Physics.SphereCast(view.position, grabWidth, view.forward, out RaycastHit hit, grabDistance, itemLayer.value)) {
            if(Physics.Raycast(view.position, view.forward, out RaycastHit hit2, grabDistance, itemLayer.value)) {
                hit = hit2; //when 2 items, chose one at crosshair rather than closest
            }
            if(!Input.GetButtonDown("Interact")) {
                hit.transform.GetComponent<Item>().hovered = true;
                return;
            } 
            GameObject selectedItem = hit.transform.gameObject;
            int slot = selectedItem.GetComponent<Item>().data.type;

            // print(hit.transform.name); 
            // print(selectedItem.GetComponent<Item>().data.type);

            if(inventory[slot] != null) return;

            selectedItem.GetComponent<Item>().ItemPickupServerRpc(false, Vector3.zero, Vector3.zero);
            inventory[slot] = selectedItem;
            selectedItem.GetComponent<Item>().model.SetActive(false);

            GameObject playerItem = Instantiate(selectedItem.GetComponent<Item>().ghostItemPrefab, gunHolder); 
            playerItem.transform.position = gunHolder.transform.position;
            playerItem.transform.rotation = gunHolder.transform.rotation;
            clientInventory[slot] = playerItem;

            select(activeGun);

            if(itemGhost != null && itemGhost.GetComponent<GhostItem>().realItem == selectedItem) {
                Destroy(itemGhost);
                itemGhost = null;
            }
        }
    }

    void Drop(int i, bool thrown) {
            itemReal = inventory[i];
            itemReal.GetComponent<Item>().ItemPickupServerRpc(true, thrown ? (view.forward * throwStength) : Vector3.zero, GetComponent<Rigidbody>().linearVelocity);
            itemReal.GetComponent<Item>().UpdateAmmoServerRpc(itemReal.GetComponent<Item>().ammo);
            //itemReal.GetComponent<MeshRenderer>().enabled = true;

            itemGhost = clientInventory[i];
            itemGhost.transform.parent = null;
            itemGhost.GetComponent<BoxCollider>().enabled = true;
            itemGhost.GetComponent<Rigidbody>().isKinematic = false;
            itemGhost.GetComponent<Rigidbody>().linearVelocity = GetComponent<Rigidbody>().linearVelocity;
            itemGhost.GetComponent<Rigidbody>().AddForce(view.forward * throwStength, ForceMode.Impulse);

            itemGhost.GetComponent<GhostItem>().realItem = itemReal;



            inventory[i] = null;
            //Destroy(clientInventory[activeGun], 5f);
            clientInventory[i] = null;

            StartCoroutine(GhostDelay());
    }

    void select(int weaponIndex) {
        shooting.readyPull = false;
        shooting.gunAnim.switchAnim();
        for (int i = 0; i < inventory.Length; i++)
        {
            if(inventory[i] != null) {
                clientInventory[i].SetActive(i == weaponIndex);
                inventory[i].GetComponent<Item>().SelectServerRpc(i == weaponIndex);
            }
        }
    }

    IEnumerator GhostDelay() {
        yield return new WaitForSeconds(3);
        if(itemGhost == null) yield break;
        Destroy(itemGhost);
        itemReal.GetComponent<Item>().model.SetActive(true);
        itemGhost = null;
        itemReal = null;
    }

    public void DropAll() {
        for (int i = 0; i < inventory.Length; i++)
        {
            if(inventory[i] != null) {
                Drop(i, false);
            }
        }
    }
}
