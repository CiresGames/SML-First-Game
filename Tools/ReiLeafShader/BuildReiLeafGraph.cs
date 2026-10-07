using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class ReiLeafGraphBuilder
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    const string Dir="Assets/MantaFlight/Trees/Shaders";
    static object graph;
    static object properties;
    static Type T(string name) { return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null); }
    static object New(string name) { return Activator.CreateInstance(T(name),true); }
    static object Get(object o,string p) { return o.GetType().GetProperty(p,F).GetValue(o); }
    static void Set(object o,string p,object v) {
        var prop=o.GetType().GetProperty(p,F);
        if(prop!=null) { if(prop.PropertyType.IsEnum && v is string) v=Enum.Parse(prop.PropertyType,(string)v); prop.SetValue(o,v); }
        else { var field=o.GetType().GetField(p,F); field.SetValue(o,v); }
    }
    static object Call(object o,string name,params object[] args) {
        var type=o as Type ?? o.GetType();
        foreach(var m in type.GetMethods(F).Where(x=>x.Name==name && !x.IsGenericMethodDefinition)) {
            var ps=m.GetParameters(); if(ps.Length<args.Length || ps.Skip(args.Length).Any(p=>!p.IsOptional)) continue;
            if(ps.Take(args.Length).Where((p,i)=>args[i]!=null && !p.ParameterType.IsInstanceOfType(args[i])).Any()) continue;
            var all=ps.Select((p,i)=>i<args.Length?args[i]:p.DefaultValue).ToArray();
            try {return m.Invoke(o is Type?null:o,all);} catch(TargetInvocationException e){throw e.InnerException;}
        }
        throw new Exception("No method "+type.Name+"."+name);
    }
    static object Node(string type,string name,float x,float y) {
        var n=New("UnityEditor.ShaderGraph."+type); Set(n,"name",name);
        Call(graph,"AddNode",n); Call(n,"ChangeVersion",Get(n,"latestVersion"));
        var draw=Get(n,"drawState"); Set(draw,"position",new Rect(x,y,210,150)); Set(draw,"expanded",true); Set(n,"drawState",draw); return n;
    }
    static void Group(string title,params object[] nodes) {
        var group=New("UnityEditor.ShaderGraph.GroupData");Set(group,"title",title);Call(graph,"CreateGroup",group);
        foreach(var node in nodes) Call(graph,"SetGroup",node,group);
    }
    static void Wire(object a,int output,object b,int input) {Call(graph,"Connect",Call(a,"GetSlotReference",output),Call(b,"GetSlotReference",input));}
    static object Prop(string type,string title,string reference,object value,float x,float y) {
        var p=New("UnityEditor.ShaderGraph.Internal."+type+"ShaderProperty");
        Set(p,"displayName",title); Set(p,"overrideReferenceName",reference); Set(p,"generatePropertyBlock",true);
        if(reference=="_One") Set(p,"generatePropertyBlock",false);
        if(type=="Vector1" && reference!="_One") {
            Set(p,"floatType","Slider");
            var range=new Vector2(0,1);
            if(reference=="_ShadowEdge" || reference=="_LitEdge") range=new Vector2(-1,1);
            if(reference=="_RimPower") range=new Vector2(.1f,8);
            if(reference=="_VariationScale") range=new Vector2(.1f,30);
            Set(p,"rangeValues",range);
        }
        if(type=="Texture2D") Set(Get(p,"value"),"texture",value); else Set(p,"value",value);
        Call(graph,"AddGraphInput",p);Call(properties,"InsertItemIntoCategory",p);
        var n=Node("PropertyNode",title,x,y); Set(n,"property",p); return n;
    }
    static object Float(string title,string reference,float value,float x,float y) {return Prop("Vector1",title,reference,value,x,y);}
    static object ColorP(string title,string reference,Color value,float x,float y) {return Prop("Color",title,reference,value,x,y);}
    static object Math2(string type,string title,object a,int ai,object b,int bi,float x,float y) {
        var n=Node(type,title,x,y); Wire(a,ai,n,0);Wire(b,bi,n,1); return n;
    }
    static object Lerp(string title,object a,int ai,object b,int bi,object t,int ti,float x,float y) {
        var n=Math2("LerpNode",title,a,ai,b,bi,x,y);Wire(t,ti,n,2);return n;
    }
    static void CustomSlot(object node,int id,string name,bool output,bool vector) {
        var type=T("UnityEditor.ShaderGraph."+(vector?"Vector3":"Vector1")+"MaterialSlot");
        var ctor=type.GetConstructors(F).First(c=>c.GetParameters().Length>4);
        var ps=ctor.GetParameters(); var args=new object[ps.Length];
        args[0]=id;args[1]=name;args[2]=name;args[3]=Enum.Parse(ps[3].ParameterType,output?"Output":"Input");args[4]=vector?(object)Vector3.zero:0f;
        for(int i=5;i<ps.Length;i++) args[i]=ps[i].DefaultValue;
        Call(node,"AddSlot",ctor.Invoke(args));
    }
    public static string Build() {
        Directory.CreateDirectory(Dir);
        File.Copy("Tools/ReiLeafShader/ReiMainLight.hlsl",Dir+"/ReiMainLight.hlsl",true);
        AssetDatabase.ImportAsset(Dir+"/ReiMainLight.hlsl",ImportAssetOptions.ForceSynchronousImport);
        graph=New("UnityEditor.ShaderGraph.GraphData"); Set(graph,"path","MantaFlight");
        Call(graph,"AddContexts");
        properties=New("UnityEditor.ShaderGraph.CategoryData");Set(properties,"name","Rei foliage");Call(graph,"AddCategory",properties);
        var target=New("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
        Call(target,"TrySetActiveSubTarget",T("UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget"));
        Set(target,"alphaClip",true); Set(target,"renderFace","Both"); Set(target,"castShadows",true);Set(target,"receiveShadows",true);
        var targets=Array.CreateInstance(T("UnityEditor.ShaderGraph.Target"),1); targets.SetValue(target,0);
        var names=new[]{"BaseColor","Alpha","AlphaClipThreshold"};
        var descriptorType=T("UnityEditor.ShaderGraph.BlockFieldDescriptor"); var descriptors=Array.CreateInstance(descriptorType,3);
        var fields=T("UnityEditor.ShaderGraph.BlockFields+SurfaceDescription");
        for(int i=0;i<3;i++) descriptors.SetValue(fields.GetField(names[i],F).GetValue(null),i);
        Call(graph,"InitializeOutputs",targets,descriptors);
        var mask=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Tree 1/textures/TreeLeaves01.png");
        var texture=Prop("Texture2D","Leaf Texture","_LeafTexture",mask,-1500,-750);
        var sample=Node("SampleTexture2DNode","Leaf color and cutout",-1200,-750);Wire(texture,0,sample,1);
        var red=Float("Mask from Red (1) or Alpha (0)","_MaskFromRed",1,-1500,-350);
        var alpha=Lerp("Choose Rei mask or RGBA atlas",sample,7,sample,4,red,0,-850,-650);
        var cutoff=Float("Cutout Threshold","_Cutoff",.45f,-550,-750);
        var tintA=ColorP("Leaf Color","_LeafColor",new Color(.20f,.48f,.055f,1),-1500,100);
        var tintB=ColorP("Leaf Variation","_LeafVariation",new Color(.48f,.68f,.11f,1),-1500,330);
        var uv=Node("UVNode","UV detail",-1800,650);
        var noise=Node("NoiseNode","Soft leaf color variation",-1200,650);Wire(uv,0,noise,0);
        var noiseScale=Float("Variation Scale","_VariationScale",5,-1500,850);Wire(noiseScale,0,noise,1);
        var palette=Lerp("Two painted leaf tones",tintA,0,tintB,0,noise,2,-850,160);
        var useRGB=Float("Use Texture Color (atlas)","_UseTextureColor",0,-1200,-50);
        var baseCol=Lerp("Palette or atlas color",palette,3,sample,0,useRGB,0,-550,60);
        var position=Node("PositionNode","World Position",-1500,1200);
        var light=Node("CustomFunctionNode","URP Main Light + Shadows",-1150,1200);
        Set(light,"sourceType","File");Set(light,"functionName","ReiMainLight");Set(light,"functionSource",AssetDatabase.AssetPathToGUID(Dir+"/ReiMainLight.hlsl"));Set(light,"functionSourceUsePragmas",true);
        CustomSlot(light,0,"Position",false,true);CustomSlot(light,1,"Direction",true,true);CustomSlot(light,2,"Color",true,true);CustomSlot(light,3,"Shadow",true,false);Wire(position,0,light,0);
        var normal=Node("NormalVectorNode","Imported soft crown normals",-1150,1550);
        var dot=Math2("DotProductNode","Facing the sun",normal,0,light,1,-800,1250);
        var edgeA=Float("Shadow Edge","_ShadowEdge",-.12f,-850,1700);var edgeB=Float("Lit Edge","_LitEdge",.55f,-850,1920);
        var ramp=Node("SmoothstepNode","Soft toon light ramp",-480,1250);Wire(edgeA,0,ramp,0);Wire(edgeB,0,ramp,1);Wire(dot,2,ramp,2);
        var shadowStrength=Float("Receive Shadow Strength","_ShadowStrength",.85f,-850,2140);
        var one=Float("Light Floor Reference","_One",1,-500,2140);
        var shadows=Lerp("Main-light shadow attenuation",one,0,light,3,shadowStrength,0,-150,1690);
        var lighting=Math2("MultiplyNode","Ramp with cast shadows",ramp,3,shadows,3,180,1250);
        var shadowTint=ColorP("Shadow Tint","_ShadowTint",new Color(.30f,.46f,.40f,1),-160,2100);
        var litTint=ColorP("Sunlit Tint","_LitTint",new Color(1.1f,1.05f,.88f,1),180,2100);
        var tone=Lerp("Cool shade / warm sunlight",shadowTint,0,litTint,0,lighting,2,520,1320);
        var shaded=Math2("MultiplyNode","Painted foliage shading",baseCol,3,tone,3,2400,400);
        var lightColorStrength=Float("Light Color Influence","_LightColorInfluence",.65f,520,1850);
        var sunColor=Lerp("Scene light color",one,0,light,2,lightColorStrength,0,850,1600);
        var illuminated=Math2("MultiplyNode","Follow scene light color",shaded,2,sunColor,3,2720,400);
        var view=Node("ViewDirectionNode","World View Direction",180,-450);
        var ndv=Math2("DotProductNode","View-facing normal",normal,0,view,0,520,-450);
        var abs=Node("AbsoluteNode","Two-sided rim",800,-450);Wire(ndv,2,abs,0);
        var inverse=Node("OneMinusNode","Silhouette rim",1080,-450);Wire(abs,1,inverse,0);
        var safeRim=Node("SaturateNode","Keep rim in 0-1 range",1360,-700);Wire(inverse,1,safeRim,0);
        var rimPower=Float("Rim Power","_RimPower",3,850,-150);
        var rim=Math2("PowerNode","Soft edge light",safeRim,1,rimPower,0,1360,-450);
        var rimStrength=Float("Rim Strength","_RimStrength",.10f,1120,-100);
        var rimAmount=Math2("MultiplyNode","Rim intensity",rim,2,rimStrength,0,1640,-450);
        var rimColor=ColorP("Rim Tint","_RimTint",new Color(.72f,.88f,.43f,1),1380,-150);
        var rimRGB=Math2("MultiplyNode","Tint rim",rimAmount,2,rimColor,0,1920,-450);
        var result=Math2("AddNode","Final Rei foliage",illuminated,2,rimRGB,2,3040,400);
        Group("01 / Texture and leaf cutout",texture,sample,red,alpha,cutoff);
        Group("02 / Painted color or atlas",tintA,tintB,uv,noise,noiseScale,palette,useRGB,baseCol);
        Group("03 / Sunlight and soft crown normals",position,light,normal,dot,edgeA,edgeB,ramp,shadowStrength,one,shadows,lighting,shadowTint,litTint,tone,lightColorStrength,sunColor);
        Group("04 / Soft silhouette rim",view,ndv,abs,inverse,safeRim,rimPower,rim,rimStrength,rimAmount,rimColor,rimRGB);
        Group("05 / Final color",shaded,illuminated,result);
        Set(Get(graph,"vertexContext"),"position",new Vector2(3400,0));
        Set(Get(graph,"fragmentContext"),"position",new Vector2(3400,400));
        var getNodes=graph.GetType().GetMethods(F).First(m=>m.Name=="GetNodes" && m.IsGenericMethodDefinition && m.GetParameters().Length==0);
        var blocks=(IEnumerable)getNodes.MakeGenericMethod(T("UnityEditor.ShaderGraph.BlockNode")).Invoke(graph,null);
        foreach(var block in blocks) {
            var descriptor=Get(block,"descriptor");var name=(string)Get(descriptor,"name");
            if(name=="BaseColor") Wire(result,2,block,0);
            if(name=="Alpha") Wire(alpha,3,block,0);
            if(name=="AlphaClipThreshold") Wire(cutoff,0,block,0);
        }
        Call(graph,"ValidateGraph");
        var serialized=(string)Call(T("UnityEditor.ShaderGraph.Serialization.MultiJson"),"Serialize",graph);
        File.WriteAllText(Dir+"/ReiLeaves.shadergraph",serialized);
        AssetDatabase.ImportAsset(Dir+"/ReiLeaves.shadergraph",ImportAssetOptions.ForceSynchronousImport);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Dir+"/ReiLeaves.shadergraph");
        if(shader==null) throw new Exception("Shader Graph did not import");
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/ReiLeaves_Green.mat");
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,Dir+"/ReiLeaves_Green.mat");}else mat.shader=shader;
        mat.SetTexture("_LeafTexture",mask);mat.SetFloat("_MaskFromRed",1);mat.SetFloat("_UseTextureColor",0);mat.enableInstancing=true;mat.doubleSidedGI=true;EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
        return "Created "+Dir+"/ReiLeaves.shadergraph and ReiLeaves_Green.mat; shader supported="+shader.isSupported;
    }
}
return ReiLeafGraphBuilder.Build();
