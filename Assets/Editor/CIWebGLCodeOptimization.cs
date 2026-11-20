using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Lets the build workflow pick the WebGL "Code Optimization" build setting, which decides how
// much Unity optimizes the game's C++ and WebAssembly code. Pull request test builds pass
//   -webglCodeOptimization BuildTimes
// to build faster ("Shorter Build Time"); releases don't pass it and keep the default.
//
// The setting is stored in Library, not in the project, so it can't be set in a project file.
// It runs when the editor loads, before the build starts. UnityEditor.WebGL is only there
// when the WebGL module is installed, so it's reached through reflection and this file still
// compiles for the tests and the Windows build.
//
// Every WebGL build also logs the setting it was built with when it finishes ("WebGL build
// used Code Optimization ..."), so the build log shows it for pull requests and releases alike.
[InitializeOnLoad]
public class CIWebGLCodeOptimization : IPostprocessBuildWithReport
{
    private const string Argument = "-webglCodeOptimization";

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        PropertyInfo property = FindProperty();
        bool passed = Array.IndexOf(Environment.GetCommandLineArgs(), Argument) >= 0;
        Debug.Log("CIWebGLCodeOptimization: WebGL build used Code Optimization " +
            (property != null
                ? property.GetValue(null) + " (options: " + string.Join(", ", Enum.GetNames(property.PropertyType)) + ")"
                : "(unknown: setting not found)") +
            "; " + Argument + (passed ? " was" : " wasn't") + " passed to Unity.");
    }

    static CIWebGLCodeOptimization()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, Argument);
        if (index < 0) return;
        if (index + 1 >= args.Length)
        {
            Debug.LogError(Argument + " needs a value, for example BuildTimes or RuntimeSpeed.");
            return;
        }
        Apply(args[index + 1]);
    }

    private static PropertyInfo FindProperty()
    {
        Type settings = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("UnityEditor.WebGL.UserBuildSettings"))
            .FirstOrDefault(type => type != null);
        return settings?.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.Static);
    }

    private static void Apply(string value)
    {
        PropertyInfo property = FindProperty();
        if (property == null)
        {
            Debug.LogError("CIWebGLCodeOptimization: UnityEditor.WebGL.UserBuildSettings.codeOptimization wasn't found. Is the WebGL module installed?");
            return;
        }

        object parsed;
        try
        {
            parsed = Enum.Parse(property.PropertyType, value);
        }
        catch (ArgumentException)
        {
            Debug.LogError("CIWebGLCodeOptimization: unknown value \"" + value + "\". Use one of: " + string.Join(", ", Enum.GetNames(property.PropertyType)));
            return;
        }

        property.SetValue(null, parsed);
        Debug.Log("CIWebGLCodeOptimization: WebGL Code Optimization is now " + property.GetValue(null));
    }
}
