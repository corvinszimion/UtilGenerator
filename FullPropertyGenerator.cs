﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using UtilGenerator.Attributes;

namespace UtilGenerator
{
    [Generator]
    public class FullPropertyGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var syntaxTrees = context.Compilation.SyntaxTrees;

            #region INotifyPropertyChangedAttribute
            foreach (var syntaxTree in syntaxTrees)
            {
                var notifyClasses = syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
                                .Where(x => x.AttributeLists.Any(a => a.ToString().StartsWith($"[INotifyPropertyChanged"))).ToList();

                foreach (var notifyClass in notifyClasses)
                {
                    var usings = syntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>();
                    string usingsAsText = string.Join(Environment.NewLine, usings);
                    StringBuilder sourceBuilder = new StringBuilder();

                    string className = notifyClass.Identifier.ToString();

                    string source = $@"//<auto-generated/>
{usingsAsText}
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SourceGeneratorMVVMTest.ViewModels
{{
    public partial class {className} : INotifyPropertyChanged
    {{
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {{
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }}
        
        {BuildPropertyChangedMethods(notifyClass)}

        {BuildProperties(notifyClass)}
    }}
}}";

                    sourceBuilder.Append(source);

                    context.AddSource($"{className}.g.cs", sourceBuilder.ToString());
                }

            }
            #endregion
        }

        private object BuildPropertyChangedMethods(TypeDeclarationSyntax classDeclaration)
        {
            StringBuilder onPropertyChangingMethodBuilder = new StringBuilder();

            var fields = classDeclaration.ChildNodes()
                            .Where(x => x is FieldDeclarationSyntax field
                                    && field.AttributeLists.Any(a => a.ToString().StartsWith("[AutoNotify")))
                            .Select(a => a as FieldDeclarationSyntax);

            foreach (FieldDeclarationSyntax field in fields)
            {
                string fieldName = field.GetFieldName();
				string propertyName = FieldToPropertyName(fieldName);
                var propertyType = field.GetFieldType();

                string methodTemplate = $"partial void On{propertyName}Changing({propertyType} newValue);";

                onPropertyChangingMethodBuilder.AppendLine(methodTemplate);
            }
            return onPropertyChangingMethodBuilder.ToString();
        }

        private string BuildProperties(TypeDeclarationSyntax classDeclaration)
        {
            StringBuilder propertyBuilder = new StringBuilder();

            var fields = classDeclaration.ChildNodes()
                            .Where(x => x is FieldDeclarationSyntax field
                                    && field.AttributeLists.Any(a => a.ToString().StartsWith($"[AutoNotify")))
                            .Select(a => a as FieldDeclarationSyntax);

            foreach (FieldDeclarationSyntax field in fields)
            {
                string type = field.GetFieldType();
                string fieldName = field.GetFieldName();
                string propertyName = FieldToPropertyName(fieldName);

                string propertyTemplate = $@"
public {type} {propertyName}
{{
    get => {fieldName};
    set
    {{
        {fieldName} = value;
{BuildNotifyPropertyChangedCalls(field)}
        On{propertyName}Changing(value);
    }}
}}
";
                propertyBuilder.Append(propertyTemplate);
            }

            return propertyBuilder.ToString();
        }

        private object BuildNotifyPropertyChangedCalls(FieldDeclarationSyntax field)
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine("OnPropertyChanged();");

            var alsoNotifyAttributes = field.AttributeLists.SelectMany(x => x.Attributes.Select(a => a.ToString()))
                                          .Where(x => x.StartsWith("AlsoNotify")).ToList();

            foreach(var attribute in alsoNotifyAttributes)
            {
                string propertyName = AlsoNotifyAttribute.GetPropertyName(attribute);
                result.AppendLine($"OnPropertyChanged(nameof({propertyName}));");
            }

            return result.ToString();
        }

        private string FieldToPropertyName(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentNullException("Der Name des Fields darf nicht null oder leer sein");
            }
            if (fieldName.StartsWith("_"))
            {
                fieldName = fieldName.Substring(1);
            }

            string result = string.Concat(fieldName[0].ToString().ToUpper(), fieldName.Substring(1));

            return result;
        }

        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
#endif
        }
    }
}
