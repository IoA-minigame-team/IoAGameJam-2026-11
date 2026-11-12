using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class InkButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,ISelectHandler,IDeselectHandler
{
    [SerializeField] Image paper;
    [SerializeField] Text caption;
    bool selected,hovered;
    public void Configure(Image image,Text label){paper=image;caption=label;}
    public void OnPointerEnter(PointerEventData data){hovered=true;Refresh();}
    public void OnPointerExit(PointerEventData data){hovered=false;Refresh();}
    public void OnSelect(BaseEventData data){selected=true;Refresh();}
    public void OnDeselect(BaseEventData data){selected=false;Refresh();}
    public void OnPointerClick(PointerEventData data){if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);}
    void Refresh(){if(paper==null||caption==null)return;bool active=selected||hovered;paper.color=active?new Color(.08f,.08f,.08f):new Color(.95f,.95f,.95f);caption.color=active?Color.white:new Color(.08f,.08f,.08f);}
    void OnDisable(){selected=hovered=false;Refresh();}
}
