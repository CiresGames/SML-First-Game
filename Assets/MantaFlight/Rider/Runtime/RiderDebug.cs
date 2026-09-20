using UnityEngine;
using TMPro;
namespace MantaFlight.Rider
{
    public sealed class RiderDebug : MonoBehaviour
    {
        public RiderController rider;
        TextMeshProUGUI text,buttonText;
        GameObject canvas;
        void Start()
        {
            canvas=new GameObject("Rider debug",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=15;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var panel=new GameObject("Rider status",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(canvas.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-20,-20);rect.sizeDelta=new Vector2(470,250);
            panel.GetComponent<UnityEngine.UI.Image>().color=new Color(.025f,.07f,.10f,.88f);
            text=Label(panel.transform,new Vector2(12,-10),new Vector2(446,178),17);
            var button=new GameObject("Invert wingsuit pitch",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));button.transform.SetParent(panel.transform,false);
            var br=button.GetComponent<RectTransform>();br.anchorMin=br.anchorMax=br.pivot=new Vector2(0,1);br.anchoredPosition=new Vector2(12,-196);br.sizeDelta=new Vector2(446,40);
            button.GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.3f,.35f);
            button.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>rider.Input.UserSettings.SetInvertPitch(!rider.Input.UserSettings.InvertPitch));
            buttonText=Label(button.transform,new Vector2(8,-8),new Vector2(430,30),17);
        }
        TextMeshProUGUI Label(Transform parent,Vector2 position,Vector2 size,int fontSize)
        {
            var g=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(parent,false);var t=g.GetComponent<TextMeshProUGUI>();
            t.fontSize=fontSize;t.color=Color.white;t.raycastTarget=false;
            var rt=t.rectTransform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;return t;
        }
        void Update()
        {
            if(text==null)return;
            text.text=$"RIDER  {rider.State}   |   MANTA {rider.mount.Mode}\nSpeed {rider.Motor.Velocity.magnitude:0.0} m/s   Height {rider.Height:0.0} m\nGlide available: {rider.GlideAvailable}   Air {rider.AirTime:0.00}s\nStall {rider.Glide.Stalled}   AoA {rider.Glide.AngleOfAttack:0} deg\nDismount {rider.mount.CanDismount(out _)}   Scoop {rider.CanScoop}\nF: mount/step off  J: jump off  K: drop\nSpace: jump  G: glide  Q: roll  H: call/cancel";
            buttonText.text="Wingsuit pitch inverted: "+rider.Input.UserSettings.InvertPitch+"   [I]";
        }
        void OnDestroy(){if(canvas!=null)Destroy(canvas);}
        void OnDrawGizmosSelected()
        {
            if(rider==null || rider.settings==null)return;
            Gizmos.color=rider.GlideAvailable?Color.green:Color.red;
            Gizmos.DrawLine(transform.position,transform.position+Vector3.down*rider.settings.glide.minimumDeployHeight);
            Gizmos.color=Color.cyan;Gizmos.DrawRay(transform.position,rider.Motor!=null?rider.Motor.Velocity:Vector3.zero);
        }
    }
}
