using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    public static class IdentityShaderAudit
    {
        public static void Dump()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter")).First(t=>t!=null);
            var method=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="GetShaderText"&&m.GetParameters().Length==4);
            var text=(string)method.Invoke(null,new object[]{"Assets/StarTournament/Resources/TrooperIdentity.shadergraph",null,null,null});
            var destination=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.local/unity-evidence"));
            Directory.CreateDirectory(destination);File.WriteAllText(Path.Combine(destination,"identity-generated.shader"),text);
            foreach(var message in ShaderUtil.GetShaderMessages(Resources.Load<Shader>("TrooperIdentity")))Debug.Log(message.message);
        }
    }
}
