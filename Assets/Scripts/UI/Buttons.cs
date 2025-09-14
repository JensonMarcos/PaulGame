using UnityEngine;  
using System.Collections;  
using UnityEngine.EventSystems;  
using UnityEngine.UI;
using TMPro;
 
public class Buttons : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler {
 
    public TextMeshProUGUI theText;
    public Image theImage, icon;
    [SerializeField] private bool inverse= false, toggle = false, isToggled = false;
    //[SerializeField] Launcher launcher;
    [SerializeField] int roomNum;
    Color selected, notselected;

    void Start() {
        if(theImage == null) theImage = GetComponent<Image>();
        if(theText == null) theText = GetComponentInChildren<TextMeshProUGUI>();
        selected = inverse ? Color.blue : Color.white;
        notselected = inverse ? Color.white : Color.blue;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if(toggle) return;

        //deslected
        theImage.color = notselected;
        theText.color = selected;
        if(icon != null) icon.color = selected;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if(!toggle) return;
        //slected toggle
        if(!isToggled) {
            //launcher.SelectLevel(roomNum);
            isToggled = true;
            theImage.color = selected;
            theText.color = notselected;
            if(icon != null) icon.color = notselected;
        }
        
    }

    public void DeSelect() {
        isToggled = false;
        theImage.color = notselected;
        theText.color = selected;
        if(icon != null) icon.color = selected;

    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if(toggle) return;
        //slected
        theImage.color = selected;
        theText.color = notselected;
        if(icon != null) icon.color = notselected;
    }
 
    public void OnPointerExit(PointerEventData eventData)
    {
        if(toggle) return;
        //deslected
        theImage.color = notselected;
        theText.color = selected;
        if(icon != null) icon.color = selected;
    }

}