using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
// using Nekara.Core;
using Nekara.Client;
using NekaraManaged.Client;

/*
 * Useful References:
 *   - http://www.tugberkugurlu.com/archive/compiling-c-sharp-code-into-memory-and-executing-it-with-roslyn
 *   - https://github.com/dotnet/roslyn/wiki/Getting-Started-C%23-Syntax-Transformation
 */

namespace Nekara.Models
{
    public class NekaraSyntaxRewriter : CSharpSyntaxRewriter
    {
        class Visitor : CSharpSyntaxVisitor<IEnumerable<SyntaxNode>>
        {
            private static SyntaxKind[] NeedContextSwitch = new SyntaxKind[]
            {
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.LocalDeclarationStatement
            };

            public override IEnumerable<SyntaxNode> DefaultVisit(SyntaxNode node)
            {
                return new[] { node };
            }
            public override IEnumerable<SyntaxNode> VisitExpressionStatement(ExpressionStatementSyntax node)
            {
                if (NeedContextSwitch.Contains(node.Expression.Kind()))
                {
                    return new SyntaxNode[]
                       {
                   ParseStatement("RuntimeEnvironment.Client.Api.ContextSwitch();\n"),
                   node
                       };
                }
                else return new SyntaxNode[] { node };
            }

            public override IEnumerable<SyntaxNode> VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                return new SyntaxNode[] { node };
            }
        }

        //private readonly SemanticModel SemanticModel;
        private readonly Visitor visitor = new Visitor();

        public NekaraSyntaxRewriter()
        {
            //this.SemanticModel = semanticModel;
        }

        public override SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list)
        {
            Console.WriteLine("Visiting Syntax List {0} : {1}", typeof(TNode).Name, list.Count);
            var result = List(list.SelectMany(visitor.Visit).Cast<TNode>());

            return base.VisitList(result);
        }

        /*public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            Console.WriteLine("Visiting Assignment Expression Specifically.");
            // return base.VisitAssignmentExpression(node);
            // return node.RemoveNode(node, SyntaxRemoveOptions.KeepExteriorTrivia);
            return node;
        }*/
    }

    public class NekaraCompiler
    {
        
        private SyntaxTree Parse(string source)
        {
            return CSharpSyntaxTree.ParseText(source);
        }

        private SyntaxTree Augment(SyntaxTree Ast)
        {
            var writer = new NekaraSyntaxRewriter();
            var original = Ast.GetRoot();

            SyntaxNode newRoot = writer.Visit(original);

            original.ReplaceNode(original, newRoot);

            Console.WriteLine(newRoot.ToFullString());

            return Parse(newRoot.ToFullString());
        }

        public Assembly Compile(string source)
        {
            var Ast = Augment(Parse(source));

            // compile
            var libRoot = Path.GetDirectoryName(typeof(object).Assembly.Location);
            MetadataReference[] references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(libRoot, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(Path.Combine(libRoot, "System.Console.dll")),
                MetadataReference.CreateFromFile(typeof(ITestingService).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TestMethodAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location)
                //MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
            };

            CSharpCompilation compilation = CSharpCompilation.Create(
                "Instrumented",
                syntaxTrees: new[] { Ast },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // generate
            var ms = new MemoryStream();
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diag => diag.IsWarningAsError || diag.Severity == DiagnosticSeverity.Error);
                foreach (var diag in failures)
                {
                    Console.Error.WriteLine("{0}: {1}", diag.Id, diag.GetMessage());
                }
                throw new AggregateException(failures.Select(diag => new Exception(diag.GetMessage())));
            }
            else
            {
                ms.Seek(0, SeekOrigin.Begin);
                Assembly assembly = Assembly.Load(ms.ToArray());
                return assembly;
            }
        }
    }
}