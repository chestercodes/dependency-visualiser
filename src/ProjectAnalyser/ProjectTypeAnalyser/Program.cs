using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectTypeAnalyser
{
    class Program
    {
        static void Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();

            var paths = new[] {
                "C:/Dev/IdentityServer/IdentityServer3/source/Core/Core.csproj"
            };

            foreach(var path in paths)
            {
                var assembliesUsedInCode = new HashSet<string>();
                var assembliesReferenced = new HashSet<string>();

                using (var workspace = MSBuildWorkspace.Create())
                {
                    var project = workspace.OpenProjectAsync(path).Result;
                    var compilation = project.GetCompilationAsync().Result;
                    compilation.ReferencedAssemblyNames.ToList().ForEach(x =>
                    {
                        assembliesReferenced.Add(x.Name);
                    });

                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var sm = compilation.GetSemanticModel(tree);

                        foreach (var inv in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
                        {
                            var typeInfo = sm.GetTypeInfo(inv);
                            var nameOrNull = typeInfo.Type?.ContainingAssembly?.Name;
                            if(nameOrNull != null)
                            {
                                if(assembliesUsedInCode.Contains(nameOrNull) == false)
                                {
                                    assembliesUsedInCode.Add(nameOrNull);
                                }
                            }
                        }
                    }
                }

                foreach(var x in assembliesUsedInCode)
                {
                    assembliesReferenced.Remove(x);
                }
                var thing = 0;
            }
        }
    }
}
