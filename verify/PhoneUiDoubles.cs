// Geometry/lifecycle doubles only: actual Unity Instantiate and rendering require in-game acceptance.
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
    }
    public class Transform
    {
        public Transform parent;
        public Vector3 localScale = new Vector3(2,2,2);
    }
    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition,sizeDelta,pivot=new Vector2(0.5f,0.5f);
        public Rect rect = new Rect {width=128.48f,height=20};
    }
    public class GameObject
    {
        public bool active;
        public void SetActive(bool value) { active=value; }
    }
    public static class Object
    {
        public static DialogChoiceBubble Instantiate(DialogChoiceBubble original, Transform parent)
            => new DialogChoiceBubble {transform=new RectTransform {parent=parent,localScale=original.transform.localScale}};
        public static void Destroy(GameObject value) {value.active=false;}
    }
    public static class Mathf
    {
        public static float Max(float a,float b) => System.Math.Max(a,b);
    }
}
public class DialogChoiceBubble
{
    public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
    public UnityEngine.Transform transform=new UnityEngine.RectTransform();
    public int index,type;
}
public class DialoguePhoneUIManager
{
    public Dialogue currentDialog;
    public List<DialogChoiceBubble> dialogChoiceBubbles = new List<DialogChoiceBubble>();
    public void DisplayChoice() { }
}
