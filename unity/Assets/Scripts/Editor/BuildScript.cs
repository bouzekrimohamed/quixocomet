using System.Linq;
using UnityEditor;

namespace QuixoUnity.Build
{
    public static class BuildScript
    {
        public static void BuildWindows()
        {
            string output = "Builds/Windows/QuixoQomet.exe";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildOutput")
                {
                    output = args[i + 1];
                    break;
                }
            }

            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                target = BuildTarget.StandaloneWindows64,
                locationPathName = output,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception($"Build Windows echoue: {report.summary.result}");
            }
        }
    }
}
