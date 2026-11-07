using UnityEngine;
using UnityEngine.EventSystems;
namespace SASCoop
{
    public sealed class OperatorHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public MainMenu menu;
        public int role;
        public void OnPointerEnter(PointerEventData e)
        {
            SquadDiorama.HoveredRole = role;
            menu.roleDetail.text = RoleLoadout.Descriptions[role];
        }
        public void OnPointerExit(PointerEventData e)
        {
            SquadDiorama.HoveredRole = -1;
            menu.roleDetail.text = RoleLoadout.Descriptions[RoleLoadout.Selected];
        }
        public void OnPointerClick(PointerEventData e)
        {
            menu.roles[role].onClick.Invoke();
        }
    }
}
