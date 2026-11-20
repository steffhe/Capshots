using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
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
[InitializeOnLoad]
public static class CIWebGLCodeOptimization
{
    private const string Argument = "-webglCodeOptimization";

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

    private static void Apply(string value)
    {
        Type settings = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("UnityEditor.WebGL.UserBuildSettings"))
            .FirstOrDefault(type => type != null);
        PropertyInfo property = settings?.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.Static);
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
