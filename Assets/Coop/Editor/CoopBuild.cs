using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class CoopBuild
{
    [MenuItem("Vaccine/Build Standalone EXE (Release)")]
    public static void BuildRelease() => Build(false);

    [MenuItem("Vaccine/Build Standalone EXE (Development)")]
    public static void BuildDev() => Build(true);

    public static void Build(bool development=true)
    {
        System.IO.Directory.CreateDirectory("Builds/SAS4-3D");
        var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/SAS4-3D/SAS4-3D.exe",target=BuildTarget.StandaloneWindows64,options=development?BuildOptions.Development:BuildOptions.None});
        System.IO.Directory.CreateDirectory("Artifacts/Coop");System.IO.File.WriteAllText("Artifacts/Coop/build.txt",r.summary.result+" / errors="+r.summary.totalErrors+" / warnings="+r.summary.totalWarnings+" / bytes="+r.summary.totalSize);
        if(r.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed: "+r.summary.result);
    }
}
