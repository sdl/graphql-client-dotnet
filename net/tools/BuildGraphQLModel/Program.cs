﻿using System;
using System.Collections.Generic;
using System.Text;
using BuildGraphQLModel.Extensions;
using System.IO;
using Sdl.Web.GraphQLClient;
using Sdl.Web.GraphQLClient.Schema;

namespace BuildGraphQLModel
{
    /// <summary>
    /// Generates strongly typed model from graphQL schema.
    /// 
    /// Example: 
    ///   run with cmdline args: -e http://localhost:8081/udp/content -ns sdl.web -o model.cs
    /// 
    /// Notes:
    ///   Feel free to modify so you can detect the extension of the output (.cs or .java) and
    ///   adjust the generated code as required so you can generate the model for Java also.
    /// </summary>
    class Program
    {       
        static string Indent(int level) => new string('\t', level);

        static void EmitHeader(ref StringBuilder sb, string ns)
        {
            sb.AppendLine($"// This file was generated by a tool on {DateTime.Now}");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Newtonsoft.Json;");
            sb.AppendLine("using Newtonsoft.Json.Converters;\n");
            sb.AppendLine($"namespace {ns}");
        }

        static void EmitComment(ref StringBuilder sb, string comment, int indent)
        {
            if (string.IsNullOrEmpty(comment)) return;
            sb.AppendLine($"{Indent(indent)}/// <summary>");
            sb.AppendLine($"{Indent(indent)}/// {comment}");
            sb.AppendLine($"{Indent(indent)}/// </summary>");
        }

        static GraphQLSchemaTypeInfo RemapFieldType(GraphQLSchemaTypeInfo fieldType)
        {
            // Just remap itemType and namespaceId(s) from int to use our enum 
            // to make things a little nicer to work with.
            switch (fieldType.Name)
            {
                case "namespaceIds":
                    return new GraphQLSchemaTypeInfo
                    {
                        Kind = "LIST",
                        OfType = new GraphQLSchemaTypeInfo
                        {
                            Kind = "ENUM",
                            Name = "ContentNamespace"
                        }
                    };
                case "namespaceId":
                    return new GraphQLSchemaTypeInfo
                    {
                        Kind = "ENUM",
                        Name = "ContentNamespace"
                    };
                case "itemType":
                    return new GraphQLSchemaTypeInfo
                    {   
                        Kind = "ENUM",
                        Name = "Sdl.Web.PublicContentApi.ItemType"
                    };
                default:
                    return fieldType;
            }
        }

        static void EmitFields(ref StringBuilder sb, List<GraphQLSchemaField> fields, int indent, bool isPublic)
        {
            if (fields == null) return;
            foreach (var field in fields)
            {
                sb.AppendLine("");
                EmitComment(ref sb, field.Description, indent);
                field.Type = RemapFieldType(field.Type);
                sb.AppendLine(
                    $"{Indent(indent)}{(isPublic?"public ":"")}{field.Type.TypeName()} {field.Name.PascalCase()} {{ get; set; }}");
            }
        }

        static void EmitFields(ref StringBuilder sb, List<GraphQLSchemaEnum> enumValues, int indent)
        {
            if (enumValues == null) return;
            for (int i = 0; i < enumValues.Count - 1; i++)
            {
                sb.AppendLine("");
                EmitComment(ref sb, enumValues[i].Description, indent);
                sb.AppendLine($"{Indent(indent)}{enumValues[i].Name.PascalCase()},");
            }
            sb.AppendLine(
                $"\n{Indent(indent)}{enumValues[enumValues.Count - 1].Name.PascalCase()}");
        }

        static void GenerateClass(StringBuilder sb, GraphQLSchema schema, GraphQLSchemaType type, int indent)
        {
            if (type.Name.StartsWith("__")) return;
            if (type.Kind.Equals("SCALAR")) return;
            EmitComment(ref sb, type.Description, indent);
            if (type.Kind.Equals("ENUM"))
            {
                sb.AppendLine($"{Indent(indent)}[JsonConverter(typeof(StringEnumConverter))]");
            }
            sb.Append($"{Indent(indent)}public {type.EmitTypeDecl()}");
            if (type.Interfaces != null && type.Interfaces.Count > 0)
            {
                sb.Append(" : ");
                for (int i = 0; i < type.Interfaces.Count - 2; i++)
                {
                    sb.Append($"{type.Interfaces[i].TypeName()}, ");
                }
                sb.Append($"{type.Interfaces[type.Interfaces.Count - 1].TypeName()}");
            }
            sb.Append($"\n{Indent(indent)}{{");
            switch (type.Kind)
            {
                case "OBJECT":        
                    EmitFields(ref sb, type.Fields, indent + 1, true);
                    break;
                case "INPUT_OBJECT":
                    EmitFields(ref sb, type.InputFields, indent + 1, true);                   
                    break;
                case "INTERFACE":
                    if (type.PossibleTypes != null)
                    {
                        EmitFields(ref sb, type.Fields, indent + 1, false);                       
                    }
                    break;
                case "ENUM":                   
                    EmitFields(ref sb, type.EnumValues, indent + 1);
                    break;
                default:
                    System.Diagnostics.Trace.WriteLine("oops");
                    break;
            }
            sb.AppendLine($"{Indent(indent)}}}\n");
        }

        static void GenerateSchemaClasses(GraphQLSchema schema, string ns, string outputFile)
        {          
            StringBuilder sb = new StringBuilder();
            EmitHeader(ref sb, ns);          
            sb.AppendLine("{");
            foreach (var type in schema.Types)
                GenerateClass(sb, schema, type, 1);
            sb.AppendLine("}");
            string output = sb.ToString();
            if(File.Exists(outputFile)) File.Delete(outputFile);
            File.AppendAllText(outputFile, output);
        }
      
        static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("BuildGraphQLModel -ns <namespace> -e <endpoint> -o <output>");
                string endpoint = null;
                string outputFile = null;
                string ns = null;
                for (int i = 0; i < args.Length; i+=2)
                {
                    switch (args[i].ToLower())
                    {
                        case "-ns":
                            ns = args[i + 1];
                            break;
                        case "-e":
                            endpoint = args[i + 1];
                            break;
                        case "-o":
                            outputFile = args[i + 1];
                            break;
                    }
                }
                if (string.IsNullOrEmpty(endpoint))
                {
                    Console.WriteLine("Specify GraphQL endpoint address.");
                    return -1;
                }
                if (string.IsNullOrEmpty(outputFile))
                {
                    Console.WriteLine("Specify output file.");
                    return -1;
                }
                if (string.IsNullOrEmpty(ns))
                {
                    Console.WriteLine("Specify namespace.");
                    return -1;
                }
                GraphQLClient client = new GraphQLClient(endpoint);
                GraphQLSchema schema = client.Schema;
                GenerateSchemaClasses(schema, ns, outputFile);
            }
            catch(Exception ex)
            {
                Console.WriteLine("An error occured when generating classes.");
                Console.WriteLine(ex.Message);
                return -1;
            }
            return 0;
        }
    }
}
