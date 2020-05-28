﻿using CppSharp;
using System.Collections.Generic;
using System.Linq;
using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Generators.CSharp;
using CppSharp.Passes;
using CppSharp.Types;
using ASTContext = CppSharp.AST.ASTContext;
using Declaration = CppSharp.AST.Declaration;
using Namespace = CppSharp.AST.Namespace;
using Type = CppSharp.AST.Type;

namespace CodeGenerator
{
    public class CodeGen : ILibrary
    {
        public void Setup(Driver driver)
        {
            const string libraryName = "cimgui.dll";
            string[] imguiDirectories = new[]
            {
                "./",
            };

            var options = driver.Options;
            options.GeneratorKind = GeneratorKind.CSharp;
            options.GenerateDefaultValuesForArguments = true;
            options.GenerateSequentialLayout = true;

            var module = options.AddModule(libraryName);
            module.OutputNamespace = "DearImguiSharp";
            module.Defines.Add("CIMGUI_DEFINE_ENUMS_AND_STRUCTS");
            foreach (var directory in imguiDirectories)
                module.IncludeDirs.Add(directory);

            module.Headers.Add("cimgui.h");
            module.Headers.Add("cimgui_impl.h");
        }

        public void SetupPasses(Driver driver)
        {
            driver.Context.TranslationUnitPasses.RenameDeclsUpperCase(RenameTargets.Property);
            driver.Context.TranslationUnitPasses.RenameDeclsUpperCase(RenameTargets.Class);
            driver.Context.TranslationUnitPasses.AddPass(new FunctionToInstanceMethodPass());
            driver.Context.TranslationUnitPasses.AddPass(new CheckDuplicatedNamesPass());
            driver.Context.TranslationUnitPasses.AddPass(new CheckFlagEnumsPass());
            driver.Context.TranslationUnitPasses.AddPass(new FixDefaultParamValuesOfOverridesPass());
            driver.Context.TranslationUnitPasses.AddPass(new HandleDefaultParamValuesPass());
            driver.Context.TranslationUnitPasses.AddPass(new MakeProtectedNestedTypesPublicPass());
            driver.Context.TranslationUnitPasses.AddPass(new SpecializationMethodsWithDependentPointersPass());
            driver.Context.GeneratorOutputPasses.AddPass(new RenameOutputPass());
        }

        public void Postprocess(Driver driver, ASTContext ctx)
        {
            // ImVectorImTextureID has a pointer type typedef'd to void*
            // This causes the type to expand to void**, but C# sets the field as IntPtr.
            // C# has no implicit cast for this.
            // We implement this property ourselves in the other project :3
            IgnoreProperty("ImVectorImTextureID", "Data", ctx);
        }

        public void Preprocess(Driver driver, ASTContext ctx)
        {
            foreach (var decl in ctx.FindDecl<Declaration>("GImGui"))
                decl.Ignore = true;
            
            RemovePrefix(ctx);
        }

        private void RemovePrefix(ASTContext ctx)
        {
            foreach (var unit in ctx.TranslationUnits)
            {
                foreach (Declaration decl in unit.Declarations)
                {
                    if (decl.GetType().Name == "Function")
                    {
                        decl.Name = GetNameWithoutPrefix(decl.Name);
                    }
                }

                RecursiveRemovePrefix(unit.Namespaces, ctx);
            }
        }

        private void RecursiveRemovePrefix(IEnumerable<Namespace> namespaces, ASTContext ctx)
        {
            foreach (var ns in namespaces)
            {
                RecursiveRemovePrefix(ns.Namespaces, ctx);
                foreach (var cls in ns.Classes)
                {
                    cls.Name = GetNameWithoutPrefix(cls.Name);
                }
            }
        }

        private string GetNameWithoutPrefix(string name) => name.TrimStart("ig".ToCharArray());
        private void IgnoreProperty(string className, string propertyName, ASTContext ctx)
        {
            var cls = ctx.FindCompleteClass(className);
            var dataProperty = cls.Properties.First(x => x.OriginalName == propertyName);
            dataProperty.Ignore = true;
        }
    }

    public class RenameOutputPass : GeneratorOutputPass
    {
        public override void VisitGeneratorOutput(GeneratorOutput output)
        {
            var blocks = output.Outputs.SelectMany(i => i.FindBlocks(BlockKind.Unknown));
            foreach (var block in blocks)
            {
                block.Text.StringBuilder.Replace("cimgui_impl", "ImGui");
                block.Text.StringBuilder.Replace("cimgui", "ImGui");
            }
        }
    }
}
