// Host UI contract doubles. Unity rendering still needs in-game acceptance.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y) {this.x=x;this.y=y;}
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
    }
    public struct Rect
    {
        public float width,height;
        public float xMax=>width/2;
        public float yMax=>height/2;
    }
    public class Transform
    {
        public Transform parent;
        public Canvas Canvas;
        public GameObject gameObject;
        public Vector3 localScale = new Vector3(2,2,2);
        public void SetParent(Transform value,bool worldPositionStays) {parent=value;}
        public Vector3 TransformPoint(Vector3 value)=>value;
        public Vector3 InverseTransformPoint(Vector3 value)=>value;
        public T GetComponentInParent<T>() where T:class => (Canvas as T) ?? parent?.GetComponentInParent<T>();
    }
    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition,sizeDelta,pivot=new Vector2(0.5f,0.5f),anchorMin,anchorMax,offsetMin,offsetMax;
        public Rect rect = new Rect {width=128.48f,height=20};
    }
    public class GameObject
    {
        public bool active;
        public Transform transform;
        public GameObject(string name="",params Type[] components) {transform=new RectTransform{gameObject=this};}
        public void SetActive(bool value) {active=value;}
    }
    public static class Object
    {
        public static DialogChoiceBubble Instantiate(DialogChoiceBubble original, Transform parent)
        {
            var copy=new DialogChoiceBubble();copy.transform.parent=parent;
            copy.transform.localScale=original.transform.localScale;return copy;
        }
        public static void Destroy(GameObject value) {value.active=false;}
    }
    public class Canvas
    {
        public Transform transform=new RectTransform {rect=new Rect {width=1280,height=720}};
        public Canvas rootCanvas=>this;
    }
    public static class Mathf
    {
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static float Clamp(float n,float min,float max)=>Math.Max(min,Math.Min(max,n));
    }
}
namespace UnityEngine.UI
{
    public class ContentSizeFitter {public bool enabled=true;}
    public class LayoutGroup {public bool enabled=true;}
}
namespace TMPro
{
    public enum TextOverflowModes {Ellipsis}
    public enum TextAlignmentOptions {MidlineLeft}
    public class TextMeshProUGUI
    {
        public bool enableAutoSizing,enableWordWrapping;
        public float fontSize=12,fontSizeMin,fontSizeMax;
        public TextOverflowModes overflowMode;
        public TextAlignmentOptions alignment;
        public UnityEngine.RectTransform rectTransform=new UnityEngine.RectTransform();
    }
}
public class DialogChoiceBubble
{
    public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
    public UnityEngine.Transform transform=>gameObject.transform;
    public TMPro.TextMeshProUGUI TMP=new TMPro.TextMeshProUGUI();
    public int index,type;
    public UnityEngine.UI.ContentSizeFitter Fitter=new UnityEngine.UI.ContentSizeFitter();
    public UnityEngine.UI.LayoutGroup Layout=new UnityEngine.UI.LayoutGroup();
    public T[] GetComponentsInChildren<T>(bool includeInactive)
        => typeof(T)==typeof(UnityEngine.UI.ContentSizeFitter) ? new[]{(T)(object)Fitter} : new[]{(T)(object)Layout};
}
public class DialoguePhoneUIManager
{
    public Dialogue currentDialog;
    public UnityEngine.Canvas Canvas=new UnityEngine.Canvas();
    public List<DialogChoiceBubble> dialogChoiceBubbles = new List<DialogChoiceBubble>();
    // Native manager is NOT a child of Canvas. The actual choice panel is.
    public T GetComponentInParent<T>() where T:class => null;
    public void DisplayChoice() { }
}
