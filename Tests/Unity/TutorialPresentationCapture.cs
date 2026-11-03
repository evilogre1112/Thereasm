using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Capture the real, settled Play Mode lesson without changing its authored scene.
public static class TutorialPresentationCapture
{
    struct Original {public Canvas canvas;public RenderMode mode;public Camera camera;public float plane;}
    public static void Capture(TutorialController guide)
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
        if(guide.Stage!=TutorialStage.ChooseCur&&guide.Stage!=TutorialStage.PlaceVillager&&guide.Stage!=TutorialStage.Curse&&guide.Stage!=TutorialStage.Shoot&&guide.Stage!=TutorialStage.HeartIntro&&guide.Stage!=TutorialStage.BudgetReference&&guide.Stage!=TutorialStage.BudgetSwapReference&&guide.Stage!=TutorialStage.BudgetSkills&&guide.Stage!=TutorialStage.BudgetOpponentIntro&&guide.Stage!=TutorialStage.BudgetOpponentPlace&&guide.Stage!=TutorialStage.EndVillageTurn)return;
        var camera=new GameObject("Tutorial validation camera").AddComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-1000);camera.nearClipPlane=.1f;camera.farClipPlane=2000;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var target=new RenderTexture(1600,900,24);camera.targetTexture=target;var originals=new List<Original>();
        try
        {
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.isRootCanvas)
            {
                originals.Add(new Original{canvas=canvas,mode=canvas.renderMode,camera=canvas.worldCamera,plane=canvas.planeDistance});
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
            }
            Canvas.ForceUpdateCanvases();typeof(TutorialController).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,null);Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            string file="Tutorial-"+guide.Stage+"-"+Localizer.Language+".png";
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,file),image.EncodeToPNG());Object.DestroyImmediate(image);
        }
        finally
        {
            RenderTexture.active=null;
            foreach(var original in originals){original.canvas.renderMode=original.mode;original.canvas.worldCamera=original.camera;original.canvas.planeDistance=original.plane;}
            Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(target);Canvas.ForceUpdateCanvases();
        }
    }
}
