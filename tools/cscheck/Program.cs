// Syntax check of every C# script (no Unity needed): fails on any parse error.
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

int bad = 0, n = 0;
foreach (var syms in new[] { new[] { "UNITY_ANDROID", "PA_UGS" }, new[] { "UNITY_IOS", "PA_UGS" }, new string[0] })
{
    var opts = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: syms);
    foreach (var f in Directory.GetFiles(args[0], "*.cs", SearchOption.AllDirectories))
    {
        n++;
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(f), opts, f);
        foreach (var d in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)) { Console.WriteLine(string.Join(",", syms) + ": " + d); bad++; }
    }
}
Console.WriteLine($"{n} files, {bad} syntax errors");
return bad == 0 ? 0 : 1;
