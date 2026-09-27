using UnityEngine;
using UnityEditor;
namespace MantaFlight.Editor
{
    public static class MantaAtmosphereBenchmark
    {
        public static string Run(string imagePath=null)
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode for a repeatable frozen scene.");
            var camera=Camera.main; var previous=camera.targetTexture; var active=RenderTexture.active;
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);
            var pixels=new Texture2D(1,1,TextureFormat.RGBA32,false);
            var times=new double[6];
            try
            {
                camera.targetTexture=target;
                for(int i=-2;i<6;i++)
                {
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1,1),0,0,false);
                    clock.Stop(); if(i>=0) times[i]=clock.Elapsed.TotalMilliseconds;
                }
                if(!string.IsNullOrEmpty(imagePath))
                {
                    var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                    image.ReadPixels(new Rect(0,0,1280,720),0,0,false);
                    System.IO.File.WriteAllBytes(imagePath,image.EncodeToPNG()); Object.DestroyImmediate(image);
                }
                System.Array.Sort(times);
                return "1280x720 frozen camera, 6 synchronous render+readback samples, median "+((times[2]+times[3])*.5).ToString("F2")+" ms; range "+times[0].ToString("F2")+"–"+times[5].ToString("F2")+" ms. Includes readback; not player FPS.";
            }
            finally
            {
                camera.targetTexture=previous; RenderTexture.active=active;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
